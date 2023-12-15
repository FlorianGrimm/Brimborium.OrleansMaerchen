//  ----------------------------------------------------------------------------------
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
/// Dispatcher for orchestrations and entities to handle processing and renewing, completion of orchestration events.
/// </summary>
public class TaskEntityDispatcher {
    private readonly INameVersionObjectManager<TaskEntity> _ObjectManager;
    private readonly IOrchestrationService _OrchestrationService;
    private readonly IEntityOrchestrationService _EntityOrchestrationService;
    private readonly WorkItemDispatcher<TaskOrchestrationWorkItem> _Dispatcher;
    private readonly DispatchMiddlewarePipeline _DispatchPipeline;
    private readonly EntityBackendProperties? _EntityBackendProperties;
    private readonly LogHelper _LogHelper;
    private readonly ErrorPropagationMode _ErrorPropagationMode;
    private readonly TaskOrchestrationDispatcher.NonBlockingCountdownLock _ConcurrentSessionLock;

    internal TaskEntityDispatcher(
        IOrchestrationService orchestrationService,
        INameVersionObjectManager<TaskEntity> entityObjectManager,
        DispatchMiddlewarePipeline entityDispatchPipeline,
        LogHelper logHelper,
        ErrorPropagationMode errorPropagationMode) {
        this._ObjectManager = entityObjectManager ?? throw new ArgumentNullException(nameof(entityObjectManager));
        this._OrchestrationService = orchestrationService ?? throw new ArgumentNullException(nameof(orchestrationService));
        this._DispatchPipeline = entityDispatchPipeline ?? throw new ArgumentNullException(nameof(entityDispatchPipeline));
        this._LogHelper = logHelper ?? throw new ArgumentNullException(nameof(logHelper));
        this._ErrorPropagationMode = errorPropagationMode;
        this._EntityOrchestrationService = (orchestrationService as IEntityOrchestrationService)!;
        this._EntityBackendProperties = this._EntityOrchestrationService.EntityBackendProperties;

        this._Dispatcher = new WorkItemDispatcher<TaskOrchestrationWorkItem>(
            "TaskEntityDispatcher",
            item => item is null ? string.Empty : item.InstanceId,
            this.OnFetchWorkItemAsync,
            this.OnProcessWorkItemSessionAsync) {
            GetDelayInSecondsAfterOnFetchException = orchestrationService.GetDelayInSecondsAfterOnFetchException,
            GetDelayInSecondsAfterOnProcessException = orchestrationService.GetDelayInSecondsAfterOnProcessException,
            SafeReleaseWorkItem = orchestrationService.ReleaseTaskOrchestrationWorkItemAsync,
            AbortWorkItem = orchestrationService.AbandonTaskOrchestrationWorkItemAsync,
            DispatcherCount = orchestrationService.TaskOrchestrationDispatcherCount,
            MaxConcurrentWorkItems = this._EntityBackendProperties?.MaxConcurrentTaskEntityWorkItems ?? 0,
            LogHelper = logHelper,
        };

        // To avoid starvation, we only allow half of all concurrently executing entities to
        // leverage extended sessions.
        var maxConcurrentSessions = (int)Math.Ceiling(this._Dispatcher.MaxConcurrentWorkItems / 2.0);
        this._ConcurrentSessionLock = new TaskOrchestrationDispatcher.NonBlockingCountdownLock(maxConcurrentSessions);
    }

    /// <summary>
    /// The entity options configured, or null if the backend does not support entities.
    /// </summary>
    public EntityBackendProperties? EntityBackendProperties => this._EntityBackendProperties;

    /// <summary>
    /// Starts the dispatcher to start getting and processing entity message batches
    /// </summary>
    public async Task StartAsync() {
        await this._Dispatcher.StartAsync();
    }

    /// <summary>
    /// Stops the dispatcher to stop getting and processing entity message batches
    /// </summary>
    /// <param name="forced">Flag indicating whether to stop gracefully or immediately</param>
    public async Task StopAsync(bool forced) {
        await this._Dispatcher.StopAsync(forced);
    }

    /// <summary>
    /// Method to get the next work item to process within supplied timeout
    /// </summary>
    /// <param name="receiveTimeout">The max timeout to wait</param>
    /// <param name="cancellationToken">A cancellation token used to cancel a fetch operation.</param>
    /// <returns>A new TaskOrchestrationWorkItem</returns>
    protected Task<TaskOrchestrationWorkItem?> OnFetchWorkItemAsync(TimeSpan receiveTimeout, CancellationToken cancellationToken) {
        return this._EntityOrchestrationService.LockNextEntityWorkItemAsync(receiveTimeout, cancellationToken);
    }

    private async Task OnProcessWorkItemSessionAsync(TaskOrchestrationWorkItem workItem) {
        try {
            if (workItem.Session is null) {
                // Legacy behavior
                _ = await this.OnProcessWorkItemAsync(workItem);
                return;
            }

            var isExtendedSession = false;

            var processCount = 0;
            try {
                while (true) {
                    // While the work item contains messages that need to be processed, execute them.
                    if (workItem.NewMessages is not null
                        && (workItem.NewMessages.Count > 0)) {
                        bool isCompletedOrInterrupted = await this.OnProcessWorkItemAsync(workItem);
                        if (isCompletedOrInterrupted) {
                            break;
                        }

                        processCount++;
                    }

                    // Fetches beyond the first require getting an extended session lock, used to prevent starvation.
                    if (processCount > 0 && !isExtendedSession) {
                        isExtendedSession = this._ConcurrentSessionLock.Acquire();
                        if (!isExtendedSession) {
                            break;
                        }
                    }

                    Stopwatch timer = Stopwatch.StartNew();

                    // Wait for new messages to arrive for the session. This call is expected to block (asynchronously)
                    // until either new messages are available or until a provider-specific timeout has expired.
                    workItem.NewMessages = await workItem.Session.FetchNewOrchestrationMessagesAsync(workItem);
                    if (workItem.NewMessages is null) {
                        break;
                    }

                    workItem.OrchestrationRuntimeState.NewEvents.Clear();
                }
            } finally {
                if (isExtendedSession) {
                    this._ConcurrentSessionLock.Release();
                }
            }
        } catch (SessionAbortedException e) {
            // Either the orchestration or the orchestration service explicitly abandoned the session.
            OrchestrationInstance instance = workItem.OrchestrationRuntimeState?.OrchestrationInstance ?? new OrchestrationInstance { InstanceId = workItem.InstanceId };
            this._LogHelper.OrchestrationAborted(instance, e.Message);
            await this._OrchestrationService.AbandonTaskOrchestrationWorkItemAsync(workItem);
        }
    }

    private class WorkItemEffects {
        public WorkItemEffects(
            List<TaskMessage> activityMessages,
            List<TaskMessage> timerMessages,
            List<TaskMessage> instanceMessages,
            int taskIdCounter,
            string instanceId,
            OrchestrationRuntimeState runtimeState
            ) {
            this.ActivityMessages = activityMessages;
            this.TimerMessages = timerMessages;
            this.InstanceMessages = instanceMessages;
            this.taskIdCounter = taskIdCounter;
            this.InstanceId = instanceId;
            this.RuntimeState = runtimeState;
        }
        public List<TaskMessage> ActivityMessages;
        public List<TaskMessage> TimerMessages;
        public List<TaskMessage> InstanceMessages;
        public int taskIdCounter;
        public string InstanceId;
        public OrchestrationRuntimeState RuntimeState;
    }

    /// <summary>
    /// Method to process a new work item
    /// </summary>
    /// <param name="workItem">The work item to process</param>
    protected async Task<bool> OnProcessWorkItemAsync(TaskOrchestrationWorkItem workItem) {
        var originalOrchestrationRuntimeState = workItem.OrchestrationRuntimeState;

        var runtimeState = workItem.OrchestrationRuntimeState;
        runtimeState.AddEvent(new OrchestratorStartedEvent(-1));

        Task? renewTask = null;
        using var renewCancellationTokenSource = new CancellationTokenSource();
        if (workItem.LockedUntilUtc < DateTime.MaxValue) {
            // start a task to run RenewUntil
            renewTask = Task.Factory.StartNew(
                () => TaskOrchestrationDispatcher.RenewUntil(workItem, this._OrchestrationService, this._LogHelper, nameof(TaskEntityDispatcher), renewCancellationTokenSource.Token),
                renewCancellationTokenSource.Token);
        }

        WorkItemEffects effects = new WorkItemEffects(
            activityMessages: [],
            timerMessages: [],
            instanceMessages: [],
            taskIdCounter: 0,
            instanceId: workItem.InstanceId,
            runtimeState: runtimeState
        );

        try {
            // Assumes that: if the batch contains a new "ExecutionStarted" event, it is the first message in the batch.
            if (!TaskOrchestrationDispatcher.ReconcileMessagesWithState(workItem, nameof(TaskEntityDispatcher), this._ErrorPropagationMode, this._LogHelper)) {
                // TODO : mark an orchestration as faulted if there is data corruption
                this._LogHelper.DroppingOrchestrationWorkItem(workItem, "Received work-item for an invalid orchestration");
            } else {

                // we start with processing all the requests and figuring out which ones to execute now
                // results can depend on whether the entity is locked, what the maximum batch size is,
                // and whether the messages arrived out of order

                this.DetermineWork(workItem.OrchestrationRuntimeState,
                     out SchedulerState schedulerState,
                     out Work workToDoNow);

                if (workToDoNow.OperationCount > 0) {
                    // execute the user-defined operations on this entity, via the middleware


                    var result = await this.ExecuteViaMiddlewareAsync(
                        workToDoNow,
                        runtimeState.OrchestrationInstance ?? throw new InvalidOperationException("runtimeState.OrchestrationInstance is null"),
                        schedulerState.EntityState ?? throw new InvalidOperationException("schedulerState.EntityState is null"));
                    var operationResults = result.Results!;

                    // if we encountered an error, record it as the result of the operations
                    // so that callers are notified that the operation did not succeed.
                    if (result.FailureDetails != null) {
                        OperationResult errorResult = new OperationResult() {
                            // for older SDKs only
                            Result = result.FailureDetails.ErrorMessage,
                            ErrorMessage = "entity dispatch failed",

                            // for newer SDKs only
                            FailureDetails = result.FailureDetails,
                        };

                        for (int i = operationResults.Count; i < workToDoNow.OperationCount; i++) {
                            operationResults.Add(errorResult);
                        }
                    }

                    // go through all results
                    // for each operation that is not a signal, send a result message back to the calling orchestrator
#warning TODO:!
                    for (int i = 0; i < result.Results!.Count; i++) {
#warning TODO:!
                        var req = workToDoNow.Operations![i];
                        if (!req.IsSignal) {
                            this.SendResultMessage(effects, req, result.Results[i]);
                        }
                    }

                    if (result.Results.Count < workToDoNow.OperationCount) {
                        // some requests were not processed (e.g. due to shutdown or timeout)
                        // in this case we just defer the work so it can be retried
                        var deferred = workToDoNow.RemoveDeferredWork(result.Results.Count);
                        schedulerState.PutBack(deferred);
                        workToDoNow.ToBeContinued(schedulerState);
                    }

                    // update the entity state based on the result
                    schedulerState.EntityState = result.EntityState;

                    // perform the actions
                    foreach (var action in result.Actions!) {
                        switch (action) {
                            case (SendSignalOperationAction sendSignalAction):
                                this.SendSignalMessage(effects, schedulerState, sendSignalAction);
                                break;
                            case (StartNewOrchestrationOperationAction startAction):
                                this.ProcessSendStartMessage(effects, runtimeState, startAction);
                                break;
                        }
                    }
                }

                // process the lock request, if any
                if (workToDoNow.LockRequest != null) {
                    this.ProcessLockRequest(effects, schedulerState, workToDoNow.LockRequest);
                }

                if (workToDoNow.ToBeRescheduled != null) {
                    foreach (var request in workToDoNow.ToBeRescheduled) {
                        // Reschedule all signals that were received before their time
                        this.SendScheduledSelfMessage(effects, request);
                    }
                }

                if (workToDoNow.SuspendAndContinue) {
                    this.SendContinueSelfMessage(effects);
                }

                // this batch is complete. Since this is an entity, we now
                // (always) start a new execution, as in continue-as-new

                var serializedSchedulerState = this.SerializeSchedulerStateForNextExecution(schedulerState);
                var nextExecutionStartedEvent = new ExecutionStartedEvent(-1, serializedSchedulerState) {
                    OrchestrationInstance = new OrchestrationInstance {
                        InstanceId = workItem.InstanceId,
                        ExecutionId = Guid.NewGuid().ToString("N")
                    },
                    Tags = runtimeState.Tags,
                    ParentInstance = runtimeState.ParentInstance,
                    Name = runtimeState.Name,
                    Version = runtimeState.Version
                };
                var entityStatus = new EntityStatus() {
                    EntityExists = schedulerState.EntityExists,
                    BacklogQueueSize = schedulerState.Queue?.Count ?? 0,
                    LockedBy = schedulerState.LockedBy,
                };
                var serializedEntityStatus = JsonConvert.SerializeObject(entityStatus, Serializer.InternalSerializerSettings);

                // create the new runtime state for the next execution
                runtimeState = new OrchestrationRuntimeState();
                runtimeState.Status = serializedEntityStatus;
                runtimeState.AddEvent(new OrchestratorStartedEvent(-1));
                runtimeState.AddEvent(nextExecutionStartedEvent);
                runtimeState.AddEvent(new OrchestratorCompletedEvent(-1));
            }
        } finally {
            if (renewTask != null) {
                try {
                    renewCancellationTokenSource.Cancel();
                    await renewTask;
                } catch (ObjectDisposedException) {
                    // ignore
                } catch (OperationCanceledException) {
                    // ignore
                }
            }
        }

        OrchestrationState? instanceState =
            (runtimeState.ExecutionStartedEvent == null)
            ? null
            : Utils.BuildOrchestrationState(runtimeState);

        if (workItem.RestoreOriginalRuntimeStateDuringCompletion) {
            // some backends expect the original runtime state object
            workItem.OrchestrationRuntimeState = originalOrchestrationRuntimeState;
        } else {
            workItem.OrchestrationRuntimeState = runtimeState;
        }

        await this._OrchestrationService.CompleteTaskOrchestrationWorkItemAsync(
            workItem,
            runtimeState,
            effects.ActivityMessages,
            effects.InstanceMessages,
            effects.TimerMessages,
            null,
            instanceState);

        if (workItem.RestoreOriginalRuntimeStateDuringCompletion) {
            workItem.OrchestrationRuntimeState = runtimeState;
        }

        return true;
    }

    private void ProcessLockRequest(WorkItemEffects effects, SchedulerState schedulerState, RequestMessage request) {
        this._LogHelper.EntityLockAcquired(effects.InstanceId, request);

        // mark the entity state as locked
        schedulerState.LockedBy = request.ParentInstanceId;

        request.Position++;
        if (request.LockSet is null) { throw new InvalidOperationException("request.LockSet is null"); }
        if (request.Position < request.LockSet.Length) {
            // send lock request to next entity in the lock set
            var target = new OrchestrationInstance() {
                InstanceId = request.LockSet[request.Position].ToString()
                // no ExecutionId
            };
            this.SendLockRequestMessage(effects, schedulerState, target, request);
        } else {
            // send lock acquisition completed response back to originating orchestration instance
            var target = new OrchestrationInstance() {
#warning TODO: !
                InstanceId = request.ParentInstanceId!,
                ExecutionId = request.ParentExecutionId
            };
            this.SendLockResponseMessage(effects, target, request.Id);
        }
    }

    private string? SerializeSchedulerStateForNextExecution(SchedulerState schedulerState) {
        if ((this._EntityBackendProperties?.SupportsImplicitEntityDeletion ?? false)
            && schedulerState.IsEmpty
            && !schedulerState.Suspended) {
            // this entity scheduler is idle and the entity is deleted, so the instance and history can be removed from storage
            // we convey this to the durability provider by issuing a continue-as-new with null input
            return null;
        } else {
            // we persist the state of the entity scheduler and entity
            return JsonConvert.SerializeObject(
                schedulerState,
                typeof(SchedulerState),
                Serializer.InternalSerializerSettings);
        }
    }

    #region Preprocess to determine work

    private void DetermineWork(OrchestrationRuntimeState runtimeState, out SchedulerState schedulerState, out Work batch) {
        ArgumentNullException.ThrowIfNull(runtimeState.OrchestrationInstance, nameof(runtimeState));
        string instanceId = runtimeState.OrchestrationInstance.InstanceId;
        schedulerState = new SchedulerState();
        batch = new Work();

        Queue<RequestMessage>? lockHolderMessages = null;

        foreach (HistoryEvent e in runtimeState.Events) {
            switch (e.EventType) {
                case EventType.ExecutionStarted:
                    if (runtimeState.Input != null) {
                        try {
                            // restore the scheduler state from the input
                            JsonConvert.PopulateObject(runtimeState.Input, schedulerState, Serializer.InternalSerializerSettings);
                        } catch (Exception exception) {
                            throw new EntitySchedulerException("Failed to deserialize entity scheduler state - may be corrupted or wrong version.", exception);
                        }
                    }
                    break;

                case EventType.EventRaised:
                    EventRaisedEvent eventRaisedEvent = (EventRaisedEvent)e;

                    if (EntityMessageEventNames.IsRequestMessage(eventRaisedEvent.Name)) {
                        // we are receiving an operation request or a lock request
                        var requestMessage = new RequestMessage();

                        try {
                            JsonConvert.PopulateObject(eventRaisedEvent.Input, requestMessage, Serializer.InternalSerializerSettings);
                        } catch (Exception exception) {
                            throw new EntitySchedulerException("Failed to deserialize incoming request message - may be corrupted or wrong version.", exception);
                        }

                        IEnumerable<RequestMessage> deliverNow;

                        if (requestMessage.ScheduledTime.HasValue) {
                            if ((requestMessage.ScheduledTime.Value - DateTime.UtcNow) > TimeSpan.FromMilliseconds(100)) {
                                // message was delivered too early. This can happen e.g. if the orchestration service has limits on the delay times for messages.
                                // We handle this by rescheduling the message instead of processing it.
                                deliverNow = Array.Empty<RequestMessage>();
                                batch.AddMessageToBeRescheduled(requestMessage);
                            } else {
                                // the message is scheduled to be delivered immediately.
                                // There are no FIFO guarantees for scheduled messages, so we skip the message sorter.
                                deliverNow = new RequestMessage[] { requestMessage };
                            }
                        } else {
                            // run this through the message sorter to help with reordering and duplicate filtering
                            deliverNow = schedulerState.MessageSorter.ReceiveInOrder(requestMessage, this._EntityBackendProperties?.EntityMessageReorderWindow ?? default);
                        }

                        foreach (var message in deliverNow) {
                            if (schedulerState.LockedBy != null && schedulerState.LockedBy == message.ParentInstanceId) {
                                if (lockHolderMessages is null) {
                                    lockHolderMessages = new Queue<RequestMessage>();
                                }

                                lockHolderMessages.Enqueue(message);
                            } else {
                                schedulerState.Enqueue(message);
                            }
                        }
                    } else if (EntityMessageEventNames.IsReleaseMessage(eventRaisedEvent.Name)) {
                        // we are receiving a lock release
                        var message = new ReleaseMessage();
                        try {
                            // restore the scheduler state from the input
                            JsonConvert.PopulateObject(eventRaisedEvent.Input, message, Serializer.InternalSerializerSettings);
                        } catch (Exception exception) {
                            throw new EntitySchedulerException("Failed to deserialize lock release message - may be corrupted or wrong version.", exception);
                        }

                        if (schedulerState.LockedBy == message.ParentInstanceId) {
                            this._LogHelper.EntityLockReleased(instanceId, message);
                            schedulerState.LockedBy = null;
                        }
                    } else {
                        // this is a continue message.
                        // Resumes processing of previously queued operations, if any.
                        schedulerState.Suspended = false;
                    }

                    break;
            }
        }

        // lock holder messages go to the front of the queue
        if (lockHolderMessages != null) {
            schedulerState.PutBack(lockHolderMessages);
        }

        if (!schedulerState.Suspended) {
            // 2. We add as many requests from the queue to the batch as possible,
            // stopping at lock requests or when the maximum batch size is reached
            while (schedulerState.MayDequeue()) {
                if (batch.OperationCount == (this._EntityBackendProperties?.MaxEntityOperationBatchSize ?? default)) {
                    // we have reached the maximum batch size already
                    // insert a delay after this batch to ensure write back
                    batch.ToBeContinued(schedulerState);
                    break;
                }

                var request = schedulerState.Dequeue();

                if (request.IsLockRequest) {
                    batch.AddLockRequest(request);
                    break;
                } else {
                    batch.AddOperation(request);
                }
            }
        }
    }

    private class Work {
        private List<RequestMessage>? _OperationBatch; // a (possibly empty) sequence of operations to be executed on the entity
        private RequestMessage? _LockRequest = null; // zero or one lock request to be executed after all the operations
        private List<RequestMessage>? _ToBeRescheduled; // a (possibly empty) list of timed messages that were delivered too early and should be rescheduled
        private bool _SuspendAndContinue; // a flag telling as to send ourselves a continue signal

        public int OperationCount => this._OperationBatch?.Count ?? 0;
        public IReadOnlyList<RequestMessage>? Operations => this._OperationBatch;
        public IReadOnlyList<RequestMessage>? ToBeRescheduled => this._ToBeRescheduled;
        public RequestMessage? LockRequest => this._LockRequest;
        public bool SuspendAndContinue => this._SuspendAndContinue;

        public void AddOperation(RequestMessage operationMessage) {
            this._OperationBatch ??= new List<RequestMessage>();
            this._OperationBatch.Add(operationMessage);
        }

        public void AddLockRequest(RequestMessage lockRequest) {
            Debug.Assert(this._LockRequest is null);
            this._LockRequest = lockRequest;
        }

        public void AddMessageToBeRescheduled(RequestMessage requestMessage) {
            this._ToBeRescheduled ??= new List<RequestMessage>();
            this._ToBeRescheduled.Add(requestMessage);
        }

        public void ToBeContinued(SchedulerState schedulerState) {
            if (!schedulerState.Suspended) {
                this._SuspendAndContinue = true;
            }
        }

        public List<OperationRequest> GetOperationRequests() {
            if (this._OperationBatch is null) { return []; }
            var operations = new List<OperationRequest>(this._OperationBatch.Count);
            for (int index = 0; index < this._OperationBatch.Count; index++) {
                var request = this._OperationBatch[index];
                operations.Add(new OperationRequest() {
                    Operation = request.Operation,
                    Id = request.Id,
                    Input = request.Input,
                });
            }
            return operations;
        }

        public Queue<RequestMessage> RemoveDeferredWork(int index) {
            var deferred = new Queue<RequestMessage>();
            var operationBatch = this._OperationBatch ??= new();
            for (int i = index; i < operationBatch.Count; i++) {
                deferred.Enqueue(operationBatch[i]);
            }
            this._OperationBatch.RemoveRange(index, operationBatch.Count - index);
            if (this._LockRequest != null) {
                deferred.Enqueue(this._LockRequest);
                this._LockRequest = null;
            }
            return deferred;
        }
    }

    #endregion

    #region Send Messages

    private void SendResultMessage(WorkItemEffects effects, RequestMessage request, OperationResult result) {
        var destination = new OrchestrationInstance() {
#warning TODO: !
            InstanceId = request.ParentInstanceId!,
            ExecutionId = request.ParentExecutionId,
        };
        var responseMessage = new ResponseMessage() {
            Result = result.Result,
            ErrorMessage = result.ErrorMessage,
            FailureDetails = result.FailureDetails,
        };
        this.ProcessSendEventMessage(effects, destination, EntityMessageEventNames.ResponseMessageEventName(request.Id), responseMessage);
    }

    private void SendSignalMessage(WorkItemEffects effects, SchedulerState schedulerState, SendSignalOperationAction action) {
        OrchestrationInstance destination = new OrchestrationInstance() {
            InstanceId = action.InstanceId
        };
        RequestMessage message = new RequestMessage() {
            ParentInstanceId = effects.InstanceId,
            ParentExecutionId = null, // for entities, message sorter persists across executions
            Id = Guid.NewGuid(),
            IsSignal = true,
            Operation = action.Name,
            ScheduledTime = action.ScheduledTime,
        };
        string eventName;
        if (action.ScheduledTime.HasValue) {
            DateTime original = action.ScheduledTime.Value;
#warning TODO:!
            DateTime capped = this._EntityBackendProperties!.GetCappedScheduledTime(DateTime.UtcNow, original);
            eventName = EntityMessageEventNames.ScheduledRequestMessageEventName(capped);
        } else {
            eventName = EntityMessageEventNames.RequestMessageEventName;
            schedulerState.MessageSorter.LabelOutgoingMessage(
                message,
#warning TODO:!
                action.InstanceId!,
                DateTime.UtcNow,
#warning TODO:!
                this._EntityBackendProperties!.EntityMessageReorderWindow);
        }
        this.ProcessSendEventMessage(effects, destination, eventName, message);
    }

    private void SendLockRequestMessage(WorkItemEffects effects, SchedulerState schedulerState, OrchestrationInstance target, RequestMessage message) {
        schedulerState.MessageSorter.LabelOutgoingMessage(message, target.InstanceId, DateTime.UtcNow, this._EntityBackendProperties?.EntityMessageReorderWindow ?? default);
        this.ProcessSendEventMessage(effects, target, EntityMessageEventNames.RequestMessageEventName, message);
    }

    private void SendLockResponseMessage(WorkItemEffects effects, OrchestrationInstance target, Guid requestId) {
        var message = new ResponseMessage() {
            // content is ignored by receiver but helps with tracing
            Result = ResponseMessage.LockAcquisitionCompletion,
        };
        this.ProcessSendEventMessage(effects, target, EntityMessageEventNames.ResponseMessageEventName(requestId), message);
    }

    private void SendScheduledSelfMessage(WorkItemEffects effects, RequestMessage request) {
        var self = new OrchestrationInstance() {
            InstanceId = effects.InstanceId,
        };
#warning TODO:!
        this.ProcessSendEventMessage(effects, self, EntityMessageEventNames.ScheduledRequestMessageEventName(request.ScheduledTime!.Value), request);
    }

    private void SendContinueSelfMessage(WorkItemEffects effects) {
        var self = new OrchestrationInstance() {
            InstanceId = effects.InstanceId,
        };
        this.ProcessSendEventMessage(effects, self, EntityMessageEventNames.ContinueMessageEventName, null);
    }

    private void ProcessSendEventMessage(WorkItemEffects effects, OrchestrationInstance destination, string eventName, object? eventContent) {
        string? serializedContent = null;
        if (eventContent != null) {
            serializedContent = JsonConvert.SerializeObject(eventContent, Serializer.InternalSerializerSettings);
        }

        var eventSentEvent = new EventSentEvent(effects.taskIdCounter++) {
            InstanceId = destination.InstanceId,
            Name = eventName,
            Input = serializedContent,
        };
#warning TODO:!
        this._LogHelper.RaisingEvent(effects.RuntimeState.OrchestrationInstance!, eventSentEvent);

        effects.InstanceMessages.Add(new TaskMessage {
            OrchestrationInstance = destination,
            Event = new EventRaisedEvent(-1, serializedContent) {
                Name = eventName,
                Input = serializedContent,
            },
        });
    }

    private void ProcessSendStartMessage(WorkItemEffects effects, OrchestrationRuntimeState runtimeState, StartNewOrchestrationOperationAction action) {
        OrchestrationInstance destination = new OrchestrationInstance() {
#warning TODO:!
            InstanceId = action.InstanceId!,
            ExecutionId = Guid.NewGuid().ToString("N"),
        };
        var executionStartedEvent = new ExecutionStartedEvent(-1, action.Input) {
            Tags = OrchestrationTags.MergeTags(
                runtimeState.Tags,
                new Dictionary<string, string>() { { OrchestrationTags.FireAndForget, "" } }),
            OrchestrationInstance = destination,
            ScheduledStartTime = action.ScheduledStartTime,
            ParentInstance = new ParentInstance {
#warning TODO:!
                OrchestrationInstance = runtimeState.OrchestrationInstance!,
                Name = runtimeState.Name,
                Version = runtimeState.Version,
                TaskScheduleId = effects.taskIdCounter++,
            },
#warning TODO:!
            Name = action.Name!,
#warning TODO:!
            Version = action.Version!,
        };
        this._LogHelper.SchedulingOrchestration(executionStartedEvent);

        effects.InstanceMessages.Add(new TaskMessage {
            OrchestrationInstance = destination,
            Event = executionStartedEvent,
        });
    }

    #endregion

    private async Task<EntityBatchResult> ExecuteViaMiddlewareAsync(Work workToDoNow, OrchestrationInstance instance, string serializedEntityState) {
        // the request object that will be passed to the worker
        var request = new EntityBatchRequest() {
            InstanceId = instance.InstanceId,
            EntityState = serializedEntityState,
            Operations = workToDoNow.GetOperationRequests(),
        };

        this._LogHelper.EntityBatchExecuting(request);

        var entityId = EntityId.FromString(instance.InstanceId);
        string entityName = entityId.Name;

        // Get the TaskEntity implementation. If it's not found, it either means that the developer never
        // registered it (which is an error, and we'll throw for this further down) or it could be that some custom
        // middleware (e.g. out-of-process execution middleware) is intended to implement the entity logic.
        TaskEntity? taskEntity = this._ObjectManager.GetObject(entityName, version: null);

        var dispatchContext = new DispatchMiddlewareContext();
        dispatchContext.SetProperty(request);

        await this._DispatchPipeline.RunAsync(dispatchContext, async _ => {
            // Check to see if the custom middleware intercepted and substituted the orchestration execution
            // with its own execution behavior, providing us with the end results. If so, we can terminate
            // the dispatch pipeline here.
            var resultFromMiddleware = dispatchContext.GetProperty<EntityBatchResult>();
            if (resultFromMiddleware != null) {
                return;
            }

            if (taskEntity is null) {
                throw TraceHelper.TraceExceptionInstance(
                    TraceEventType.Error,
                    "TaskOrchestrationDispatcher-EntityTypeMissing",
                    instance,
                    new TypeMissingException($"Entity not found: {entityName}"));
            }

            var result = await taskEntity.ExecuteOperationBatchAsync(request);

            dispatchContext.SetProperty(result);
        });

        var result = dispatchContext.GetProperty<EntityBatchResult>();

        this._LogHelper.EntityBatchExecuted(request, result);

        return result;
    }
}