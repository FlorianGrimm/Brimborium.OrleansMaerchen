﻿//  ----------------------------------------------------------------------------------
//  Copyright Microsoft Corporation
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ----------------------------------------------------------------------------------

namespace Orleans.DurableTask.Core;

/// <summary>
/// Dispatcher for task activities to handle processing and renewing of work items
/// </summary>
public sealed class TaskActivityDispatcher {
    private readonly INameVersionObjectManager<TaskActivity> _ObjectManager;
    private readonly WorkItemDispatcher<TaskActivityWorkItem> _Dispatcher;
    private readonly IOrchestrationService _OrchestrationService;
    private readonly DispatchMiddlewarePipeline _DispatchPipeline;
    private readonly LogHelper _LogHelper;
    private readonly ErrorPropagationMode _ErrorPropagationMode;

    internal TaskActivityDispatcher(
        IOrchestrationService orchestrationService,
        INameVersionObjectManager<TaskActivity> objectManager,
        DispatchMiddlewarePipeline dispatchPipeline,
        LogHelper logHelper,
        ErrorPropagationMode errorPropagationMode) {
        this._OrchestrationService = orchestrationService ?? throw new ArgumentNullException(nameof(orchestrationService));
        this._ObjectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
        this._DispatchPipeline = dispatchPipeline ?? throw new ArgumentNullException(nameof(dispatchPipeline));
        this._LogHelper = logHelper;
        this._ErrorPropagationMode = errorPropagationMode;

        this._Dispatcher = new WorkItemDispatcher<TaskActivityWorkItem>(
            "TaskActivityDispatcher",
            item => item.Id,
            this.OnFetchWorkItemAsync,
            this.OnProcessWorkItemAsync) {
            AbortWorkItem = orchestrationService.AbandonTaskActivityWorkItemAsync,
            GetDelayInSecondsAfterOnFetchException = orchestrationService.GetDelayInSecondsAfterOnFetchException,
            GetDelayInSecondsAfterOnProcessException = orchestrationService.GetDelayInSecondsAfterOnProcessException,
            DispatcherCount = orchestrationService.TaskActivityDispatcherCount,
            MaxConcurrentWorkItems = orchestrationService.MaxConcurrentTaskActivityWorkItems,
            LogHelper = logHelper,
        };
    }

    /// <summary>
    /// Gets or sets flag whether to include additional details in error messages
    /// </summary>
    public bool IncludeDetails { get; set; }

    /// <summary>
    /// Starts the dispatcher to start getting and processing task activities
    /// </summary>
    public async Task StartAsync() {
        await this._Dispatcher.StartAsync();
    }

    /// <summary>
    /// Stops the dispatcher to stop getting and processing task activities
    /// </summary>
    /// <param name="forced">Flag indicating whether to stop gracefully or immediately</param>
    public async Task StopAsync(bool forced) {
        await this._Dispatcher.StopAsync(forced);
    }

    private Task<TaskActivityWorkItem?> OnFetchWorkItemAsync(TimeSpan receiveTimeout, CancellationToken cancellationToken) {
        return this._OrchestrationService.LockNextTaskActivityWorkItem(receiveTimeout, cancellationToken);
    }

    private async Task OnProcessWorkItemAsync(TaskActivityWorkItem workItem) {
        Task? renewTask = null;
        using var renewCancellationTokenSource = new CancellationTokenSource();

        TaskMessage taskMessage = workItem.TaskMessage;
        OrchestrationInstance orchestrationInstance = taskMessage.OrchestrationInstance;
        TaskScheduledEvent? scheduledEvent = null;
        Activity? diagnosticActivity = null;
        try {
            if (orchestrationInstance is null || string.IsNullOrWhiteSpace(orchestrationInstance.InstanceId)) {
                this._LogHelper.TaskActivityDispatcherError(
                    workItem,
                    $"The activity worker received a message that does not have any OrchestrationInstance information.");
                throw TraceHelper.TraceException(
                    TraceEventType.Error,
                    "TaskActivityDispatcher-MissingOrchestrationInstance",
                    new InvalidOperationException("Message does not contain any OrchestrationInstance information"));
            }

            if (taskMessage.Event.EventType != EventType.TaskScheduled) {
                this._LogHelper.TaskActivityDispatcherError(
                    workItem,
                    $"The activity worker received an event of type '{taskMessage.Event.EventType}' but only '{EventType.TaskScheduled}' is supported.");
                throw TraceHelper.TraceException(
                    TraceEventType.Critical,
                    "TaskActivityDispatcher-UnsupportedEventType",
                    new NotSupportedException($"Activity worker does not support event of type: {taskMessage.Event.EventType}"));
            }

            scheduledEvent = (TaskScheduledEvent)taskMessage.Event;

            // Distributed tracing: start a new trace activity derived from the orchestration's trace context.
            Activity? traceActivity = TraceHelper.StartTraceActivityForTaskExecution(scheduledEvent, orchestrationInstance);

            this._LogHelper.TaskActivityStarting(orchestrationInstance, scheduledEvent);

            if (scheduledEvent.Name is null) {
                string message = $"The activity worker received a {nameof(EventType.TaskScheduled)} event that does not specify an activity name.";
                this._LogHelper.TaskActivityDispatcherError(workItem, message);
                throw TraceHelper.TraceException(
                    TraceEventType.Error,
                    "TaskActivityDispatcher-MissingActivityName",
                    new InvalidOperationException(message));
            }

            this._LogHelper.TaskActivityStarting(orchestrationInstance, scheduledEvent);
            TaskActivity? taskActivity = this._ObjectManager.GetObject(scheduledEvent.Name, scheduledEvent.Version);

            if (workItem.LockedUntilUtc < DateTime.MaxValue) {
                // start a task to run RenewUntil
                renewTask = Task.Factory.StartNew(
                    () => this.RenewUntil(workItem, renewCancellationTokenSource.Token),
                    renewCancellationTokenSource.Token);
            }

            var dispatchContext = new DispatchMiddlewareContext();
            dispatchContext.SetProperty(taskMessage.OrchestrationInstance);
            dispatchContext.SetProperty(taskActivity);
            dispatchContext.SetProperty(scheduledEvent);

            // In transitionary phase (activity queued from old code, accessed in new code) context can be null.
            if (taskMessage.OrchestrationExecutionContext != null) {
                dispatchContext.SetProperty(taskMessage.OrchestrationExecutionContext);
            }

            // correlation
            CorrelationTraceClient.Propagate(() => {
                workItem.TraceContextBase?.SetActivityToCurrent();
                diagnosticActivity = workItem.TraceContextBase?.CurrentActivity;
            });

            ActivityExecutionResult? result;
            try {
                await this._DispatchPipeline.RunAsync(dispatchContext, async _ => {
                    if (taskActivity is null) {
                        // This likely indicates a deployment error of some kind. Because these unhandled exceptions are
                        // automatically retried, resolving this may require redeploying the app code so that the activity exists again.
                        // CONSIDER: Should this be changed into a permanent error that fails the orchestration? Perhaps
                        //           the app owner doesn't care to preserve existing instances when doing code deployments?
                        throw new TypeMissingException($"TaskActivity {scheduledEvent.Name} version {scheduledEvent.Version} was not found");
                    }

                    var context = new TaskContext(taskMessage.OrchestrationInstance);
                    context.ErrorPropagationMode = this._ErrorPropagationMode;

                    HistoryEvent? responseEvent;

                    try {
                        string? output = await taskActivity.RunAsync(context, scheduledEvent.Input);
                        responseEvent = new TaskCompletedEvent(-1, scheduledEvent.EventId, output);
                    } catch (Exception e) when (e is not TaskFailureException && !Utils.IsFatal(e) && !Utils.IsExecutionAborting(e)) {
                        // These are unexpected exceptions that occur in the task activity abstraction. Normal exceptions from 
                        // activities are expected to be translated into TaskFailureException and handled outside the middleware
                        // context (see further below).
                        TraceHelper.TraceExceptionInstance(TraceEventType.Error, "TaskActivityDispatcher-ProcessException", taskMessage.OrchestrationInstance, e);
                        string? details = this.IncludeDetails
                            ? $"Unhandled exception while executing task: {e}"
                            : null;
                        responseEvent = new TaskFailedEvent(-1, scheduledEvent.EventId, e.Message, details, new FailureDetails(e));

                        traceActivity?.SetStatus(ActivityStatusCode.Error, e.Message);

                        this._LogHelper.TaskActivityFailure(orchestrationInstance, scheduledEvent.Name, (TaskFailedEvent)responseEvent, e);
                    }

                    var result = new ActivityExecutionResult { ResponseEvent = responseEvent };
                    dispatchContext.SetProperty(result);
                });

                result = dispatchContext.GetProperty<ActivityExecutionResult>();
            } catch (TaskFailureException e) {
                // These are normal task activity failures. They can come from Activity implementations or from middleware.
                TraceHelper.TraceExceptionInstance(TraceEventType.Error, "TaskActivityDispatcher-ProcessTaskFailure", taskMessage.OrchestrationInstance, e);
                string? details = this.IncludeDetails ? e.Details : null;
                var failureEvent = new TaskFailedEvent(-1, scheduledEvent.EventId, e.Message, details, e.FailureDetails);

                traceActivity?.SetStatus(ActivityStatusCode.Error, e.Message);

                this._LogHelper.TaskActivityFailure(orchestrationInstance, scheduledEvent.Name, failureEvent, e);
                CorrelationTraceClient.Propagate(() => CorrelationTraceClient.TrackException(e));
                result = new ActivityExecutionResult { ResponseEvent = failureEvent };
            } catch (Exception middlewareException) when (!Utils.IsFatal(middlewareException)) {
                traceActivity?.SetStatus(ActivityStatusCode.Error, middlewareException.Message);

                // These are considered retriable
                this._LogHelper.TaskActivityDispatcherError(workItem, $"Unhandled exception in activity middleware pipeline: {middlewareException}");
                throw;
            }

            HistoryEvent? eventToRespond = result?.ResponseEvent;

            if (eventToRespond is TaskCompletedEvent completedEvent) {
                this._LogHelper.TaskActivityCompleted(orchestrationInstance, scheduledEvent.Name, completedEvent);
            } else if (eventToRespond is null) {
                // Default response if middleware prevents a response from being generated
                eventToRespond = new TaskCompletedEvent(-1, scheduledEvent.EventId, null);
            }

            var responseTaskMessage = new TaskMessage {
                Event = eventToRespond,
                OrchestrationInstance = orchestrationInstance
            };

            // Stop the trace activity here to avoid including the completion time in the latency calculation
            traceActivity?.Stop();

            await this._OrchestrationService.CompleteTaskActivityWorkItemAsync(workItem, responseTaskMessage);
        } catch (SessionAbortedException e) {
            // The activity aborted its execution
            this._LogHelper.TaskActivityAborted(orchestrationInstance, scheduledEvent!, e.Message);
            TraceHelper.TraceInstance(TraceEventType.Warning, "TaskActivityDispatcher-ExecutionAborted", orchestrationInstance, "{0}: {1}", scheduledEvent?.Name ?? "", e.Message);
            await this._OrchestrationService.AbandonTaskActivityWorkItemAsync(workItem);
        } finally {
            diagnosticActivity?.Stop(); // Ensure the activity is stopped here to prevent it from leaking out.
            if (renewTask != null) {
                renewCancellationTokenSource.Cancel();
                try {
                    // wait the renewTask finish
                    await renewTask;
                } catch (OperationCanceledException) {
                    // ignore
                }
            }
        }
    }

    private async Task RenewUntil(TaskActivityWorkItem? workItem, CancellationToken cancellationToken) {
        try {
            if (workItem is null) { return; }
            if (workItem.LockedUntilUtc < DateTime.UtcNow) { return; }

            DateTime renewAt = workItem.LockedUntilUtc.Subtract(TimeSpan.FromSeconds(30));

            // service bus clock sku can really mess us up so just always renew every 30 secs regardless of 
            // what the message.LockedUntilUtc says. if the sku is negative then in the worst case we will be
            // renewing every 5 secs
            //
            renewAt = this.AdjustRenewAt(renewAt);

            while (!cancellationToken.IsCancellationRequested) {
                await Utils.DelayWithCancellation(TimeSpan.FromSeconds(5), cancellationToken);

                if (DateTime.UtcNow < renewAt || cancellationToken.IsCancellationRequested) {
                    continue;
                }

                try {
                    this._LogHelper.RenewActivityMessageStarting(workItem);
                    TraceHelper.Trace(TraceEventType.Information, "TaskActivityDispatcher-RenewLock", "Renewing lock for work item id {0}", workItem.Id);
                    workItem = await this._OrchestrationService.RenewTaskActivityWorkItemLockAsync(workItem);
                    if (workItem is null) { return; }
                    renewAt = workItem.LockedUntilUtc.Subtract(TimeSpan.FromSeconds(30));
                    renewAt = this.AdjustRenewAt(renewAt);
                    this._LogHelper.RenewActivityMessageCompleted(workItem, renewAt);
                    TraceHelper.Trace(TraceEventType.Information, "TaskActivityDispatcher-RenewLockAt", "Next renew for work item id '{0}' at '{1}'", workItem.Id, renewAt);
                } catch (Exception exception) when (!Utils.IsFatal(exception)) {
                    // might have been completed
                    this._LogHelper.RenewActivityMessageFailed(workItem, exception);
                    _ = TraceHelper.TraceException(TraceEventType.Warning, "TaskActivityDispatcher-RenewLockFailure", exception, "Failed to renew lock for work item {0}", workItem?.Id ?? string.Empty);
                    break;
                }
            }
        } catch (OperationCanceledException) {
            // cancellation was triggered
        } catch (ObjectDisposedException) {
            // brokered message is already disposed probably through 
            // a complete call in the main dispatcher thread
        }
    }

    private DateTime AdjustRenewAt(DateTime renewAt) {
        DateTime maxRenewAt = DateTime.UtcNow.Add(TimeSpan.FromSeconds(30));
        return renewAt > maxRenewAt ? maxRenewAt : renewAt;
    }
}