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
/// Utility for executing task orchestrators.
/// </summary>
public class TaskOrchestrationExecutor {
    private readonly TaskOrchestrationContext context;
    private readonly TaskScheduler decisionScheduler;
    private readonly OrchestrationRuntimeState orchestrationRuntimeState;
    private readonly TaskOrchestration taskOrchestration;
    private readonly bool skipCarryOverEvents;
    private Task<string>? result;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskOrchestrationExecutor"/> class.
    /// </summary>
    /// <param name="orchestrationRuntimeState"></param>
    /// <param name="taskOrchestration"></param>
    /// <param name="eventBehaviourForContinueAsNew"></param>
    /// <param name="entityParameters"></param>
    /// <param name="errorPropagationMode"></param>
    public TaskOrchestrationExecutor(
        OrchestrationRuntimeState orchestrationRuntimeState,
        TaskOrchestration taskOrchestration,
        BehaviorOnContinueAsNew eventBehaviourForContinueAsNew,
        TaskOrchestrationEntityParameters? entityParameters,
        ErrorPropagationMode errorPropagationMode = ErrorPropagationMode.SerializeExceptions) {
        this.decisionScheduler = new SynchronousTaskScheduler();
        this.context = new TaskOrchestrationContext(
#warning TODO:!
            orchestrationRuntimeState.OrchestrationInstance!,
            this.decisionScheduler,
#warning TODO:!
            entityParameters!,
            errorPropagationMode);
        this.orchestrationRuntimeState = orchestrationRuntimeState;
        this.taskOrchestration = taskOrchestration;
        this.skipCarryOverEvents = eventBehaviourForContinueAsNew == BehaviorOnContinueAsNew.Ignore;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskOrchestrationExecutor"/> class.
    /// This overload is needed only to avoid breaking changes because this is a public constructor.
    /// </summary>
    /// <param name="orchestrationRuntimeState"></param>
    /// <param name="taskOrchestration"></param>
    /// <param name="eventBehaviourForContinueAsNew"></param>
    /// <param name="errorPropagationMode"></param>
    public TaskOrchestrationExecutor(
        OrchestrationRuntimeState orchestrationRuntimeState,
        TaskOrchestration taskOrchestration,
        BehaviorOnContinueAsNew eventBehaviourForContinueAsNew,
        ErrorPropagationMode errorPropagationMode = ErrorPropagationMode.SerializeExceptions)
        : this(orchestrationRuntimeState, taskOrchestration, eventBehaviourForContinueAsNew, entityParameters: null, errorPropagationMode) {
    }

    internal bool IsCompleted => this.result != null && (this.result.IsCompleted || this.result.IsFaulted);

    /// <summary>
    /// Executes an orchestration from the beginning.
    /// </summary>
    /// <returns>
    /// The result of the orchestration execution, including any actions scheduled by the orchestrator.
    /// </returns>
    public OrchestratorExecutionResult Execute() {
        return this.ExecuteCore(
            pastEvents: this.orchestrationRuntimeState.PastEvents,
            newEvents: this.orchestrationRuntimeState.NewEvents);
    }

    /// <summary>
    /// Resumes an orchestration
    /// </summary>
    /// <returns>
    /// The result of the orchestration execution, including any actions scheduled by the orchestrator.
    /// </returns>
    public OrchestratorExecutionResult ExecuteNewEvents() {
        this.context.ClearPendingActions();
        return this.ExecuteCore(
            pastEvents: Enumerable.Empty<HistoryEvent>(),
            newEvents: this.orchestrationRuntimeState.NewEvents);
    }

    private OrchestratorExecutionResult ExecuteCore(IEnumerable<HistoryEvent> pastEvents, IEnumerable<HistoryEvent> newEvents) {
        SynchronizationContext? prevCtx = SynchronizationContext.Current;

        try {
            SynchronizationContext syncCtx = new TaskOrchestrationSynchronizationContext(this.decisionScheduler);
            SynchronizationContext.SetSynchronizationContext(syncCtx);
            OrchestrationContext.IsOrchestratorThread = true;

            try {
                void ProcessEvents(IEnumerable<HistoryEvent> events) {
                    foreach (HistoryEvent historyEvent in events) {
                        if (historyEvent.EventType == EventType.OrchestratorStarted) {
                            var decisionStartedEvent = (OrchestratorStartedEvent)historyEvent;
                            this.context.CurrentUtcDateTime = decisionStartedEvent.Timestamp;
                            continue;
                        }

                        this.ProcessEvent(historyEvent);
                        historyEvent.IsPlayed = true;
                    }
                }

                // Replay the old history to rebuild the local state of the orchestration.
                // TODO: Log a verbose message indicating that the replay has started (include event count?)
                this.context.IsReplaying = true;
                ProcessEvents(pastEvents);

                // Play the newly arrived events to determine the next action to take.
                // TODO: Log a verbose message indicating that new events are being processed (include event count?)
                this.context.IsReplaying = false;
                ProcessEvents(newEvents);

                // check if workflow is completed after this replay
                // TODO: Create a setting that allows orchestrations to complete when the orchestrator
                //       function completes, even if there are open tasks.
                if (!this.context.HasOpenTasks) {
                    if (this.result!.IsCompleted) {
                        if (this.result.IsFaulted) {
                            Exception? exception = this.result.Exception?.InnerExceptions.FirstOrDefault();
                            Debug.Assert(exception != null);

                            if (Utils.IsExecutionAborting(exception!)) {
                                // Let this exception propagate out to be handled by the dispatcher
                                ExceptionDispatchInfo.Capture(exception).Throw();
                            }

                            this.context.FailOrchestration(exception);
                        } else {
                            this.context.CompleteOrchestration(this.result.Result);
                        }
                    }

                    // TODO: It is an error if result is not completed when all OpenTasks are done.
                    // Throw an exception in that case.
                }
            } catch (NonDeterministicOrchestrationException exception) {
                this.context.FailOrchestration(exception);
            }

            return new OrchestratorExecutionResult {
                Actions = this.context.OrchestratorActions,
                CustomStatus = this.taskOrchestration.GetStatus(),
            };
        } finally {
            SynchronizationContext.SetSynchronizationContext(prevCtx);
            OrchestrationContext.IsOrchestratorThread = false;
        }
    }

    private void ProcessEvent(HistoryEvent historyEvent) {
        bool overrideSuspension = historyEvent.EventType == EventType.ExecutionResumed || historyEvent.EventType == EventType.ExecutionTerminated;
        if (this.context.IsSuspended && !overrideSuspension) {
            this.context.HandleEventWhileSuspended(historyEvent);
        } else {
            switch (historyEvent.EventType) {
                case EventType.ExecutionStarted:
                    if (historyEvent is not ExecutionStartedEvent executionStartedEvent) { 
                        throw new ArgumentException(historyEvent.EventType.ToString(), nameof(historyEvent)); 
                    }
                    this.result = this.taskOrchestration.Execute(this.context, executionStartedEvent.Input);
                    break;
                case EventType.ExecutionTerminated:
                    if (historyEvent is not ExecutionTerminatedEvent executionTerminatedEvent) {
                        throw new ArgumentException(historyEvent.EventType.ToString(), nameof(historyEvent));
                    }
                    this.context.HandleExecutionTerminatedEvent(executionTerminatedEvent);
                    break;
                case EventType.TaskScheduled:
                    if (historyEvent is not TaskScheduledEvent taskScheduledEvent) {
                        throw new ArgumentException(historyEvent.EventType.ToString(), nameof(historyEvent));
                    }
                    this.context.HandleTaskScheduledEvent(taskScheduledEvent);
                    break;
                case EventType.TaskCompleted:
                    if (historyEvent is not TaskCompletedEvent taskCompletedEvent) {
                        throw new ArgumentException(historyEvent.EventType.ToString(), nameof(historyEvent));
                    }
                    this.context.HandleTaskCompletedEvent(taskCompletedEvent);
                    break;
                case EventType.TaskFailed:
                    if (historyEvent is not TaskFailedEvent taskFailedEvent) {
                        throw new ArgumentException(historyEvent.EventType.ToString(), nameof(historyEvent));
                    }
                    this.context.HandleTaskFailedEvent(taskFailedEvent);
                    break;
                case EventType.SubOrchestrationInstanceCreated:
                    if (historyEvent is not SubOrchestrationInstanceCreatedEvent subOrchestrationInstanceCreatedEvent) {
                        throw new ArgumentException(historyEvent.EventType.ToString(), nameof(historyEvent));
                    }
                    this.context.HandleSubOrchestrationCreatedEvent(subOrchestrationInstanceCreatedEvent);
                    break;
                case EventType.SubOrchestrationInstanceCompleted:
                    if (historyEvent is not SubOrchestrationInstanceCompletedEvent subOrchestrationInstanceCompletedEvent) {
                        throw new ArgumentException(historyEvent.EventType.ToString(), nameof(historyEvent));
                    }
                    this.context.HandleSubOrchestrationInstanceCompletedEvent(subOrchestrationInstanceCompletedEvent);
                    break;
                case EventType.SubOrchestrationInstanceFailed:
                    if (historyEvent is not SubOrchestrationInstanceFailedEvent subOrchestrationInstanceFailedEvent) {
                        throw new ArgumentException(historyEvent.EventType.ToString(), nameof(historyEvent));
                    }
                    this.context.HandleSubOrchestrationInstanceFailedEvent(subOrchestrationInstanceFailedEvent);
                    break;
                case EventType.TimerCreated:
                    if (historyEvent is not TimerCreatedEvent timerCreatedEvent) {
                        throw new ArgumentException(historyEvent.EventType.ToString(), nameof(historyEvent));
                    }
                    this.context.HandleTimerCreatedEvent(timerCreatedEvent);
                    break;
                case EventType.TimerFired:
                    if (historyEvent is not TimerFiredEvent timerFiredEvent) {
                        throw new ArgumentException(historyEvent.EventType.ToString(), nameof(historyEvent));
                    }
                    this.context.HandleTimerFiredEvent(timerFiredEvent);
                    break;
                case EventType.EventSent:
                    if (historyEvent is not EventSentEvent eventSentEvent) {
                        throw new ArgumentException(historyEvent.EventType.ToString(), nameof(historyEvent));
                    }
                    this.context.HandleEventSentEvent(eventSentEvent);
                    break;
                case EventType.EventRaised:
                    if (historyEvent is not EventRaisedEvent eventRaisedEvent) {
                        throw new ArgumentException(historyEvent.EventType.ToString(), nameof(historyEvent));
                    }
                    this.context.HandleEventRaisedEvent(eventRaisedEvent, this.skipCarryOverEvents, this.taskOrchestration);
                    break;
                case EventType.ExecutionSuspended:
                    if (historyEvent is not ExecutionSuspendedEvent executionSuspendedEvent) {
                        throw new ArgumentException(historyEvent.EventType.ToString(), nameof(historyEvent));
                    }
                    this.context.HandleExecutionSuspendedEvent(executionSuspendedEvent);
                    break;
                case EventType.ExecutionResumed:
                    if (historyEvent is not ExecutionResumedEvent executionResumedEvent) {
                        throw new ArgumentException(historyEvent.EventType.ToString(), nameof(historyEvent));
                    }
                    this.context.HandleExecutionResumedEvent(executionResumedEvent, this.ProcessEvent);
                    break;
            }
        }
    }

    private class TaskOrchestrationSynchronizationContext : SynchronizationContext {
        private readonly TaskScheduler _Scheduler;

        public TaskOrchestrationSynchronizationContext(TaskScheduler scheduler) {
            this._Scheduler = scheduler;
        }

        public override void Post(SendOrPostCallback sendOrPostCallback, object state) {
            _ = Task.Factory.StartNew(() => sendOrPostCallback(state),
                CancellationToken.None,
                TaskCreationOptions.None,
                this._Scheduler);
        }

        public override void Send(SendOrPostCallback sendOrPostCallback, object state) {
            var t = new Task(() => sendOrPostCallback(state));
            t.RunSynchronously(this._Scheduler);
            t.Wait();
        }
    }
}