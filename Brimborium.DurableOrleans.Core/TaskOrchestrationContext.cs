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

public /*internal*/ class TaskOrchestrationContext : OrchestrationContext {
    private readonly Dictionary<int, OpenTaskInfo> _OpenTasks;
    private readonly SortedDictionary<int, OrchestratorAction> _OrchestratorActionsMap;
    private OrchestrationCompleteOrchestratorAction? _ContinueAsNew;
    private bool _ExecutionCompletedOrTerminated;
    private int _IdCounter;
    private readonly Queue<HistoryEvent> _EventsWhileSuspended;

    public bool IsSuspended { get; private set; }

    public bool HasContinueAsNew => this._ContinueAsNew != null;

    public void AddEventToNextIteration(HistoryEvent he) {
#warning TODO: !
        this._ContinueAsNew?.CarryoverEvents.Add(he);
    }

    public TaskOrchestrationContext(
        OrchestrationInstance orchestrationInstance,
        TaskScheduler taskScheduler,
        TaskOrchestrationEntityParameters? entityParameters = null,
        ErrorPropagationMode errorPropagationMode = ErrorPropagationMode.SerializeExceptions) {
        Utils.UnusedParameter(taskScheduler);

        this._OpenTasks = new Dictionary<int, OpenTaskInfo>();
        this._OrchestratorActionsMap = new SortedDictionary<int, OrchestratorAction>();
        this._IdCounter = 0;
        this._EventsWhileSuspended = new Queue<HistoryEvent>();
        this.MessageDataConverter = JsonDataConverter.Default;
        this.ErrorDataConverter = JsonDataConverter.Default;
        this.OrchestrationInstance = orchestrationInstance;
        this.IsReplaying = false;
        this.EntityParameters = entityParameters;
        this.ErrorPropagationMode = errorPropagationMode;
    }

    public IEnumerable<OrchestratorAction> OrchestratorActions => this._OrchestratorActionsMap.Values;

    public bool HasOpenTasks => this._OpenTasks.Count > 0;

    internal void ClearPendingActions() {
        this._OrchestratorActionsMap.Clear();
        this._ContinueAsNew = null;
    }

    public override async Task<TResult> ScheduleTask<TResult>(
        string name,
        string version,
        params object[] parameters) {
        var result = await this.ScheduleTaskToWorker<TResult>(name, version, null, parameters);
        return result;
    }

    public async Task<TResult> ScheduleTaskToWorker<TResult>(
        string name,
        string version,
        string? taskList,
        params object[] parameters) {
        object? result = await this.ScheduleTaskInternal(name, version, taskList, typeof(TResult), parameters);

        if (result is null) {
            return default!;
        } else {
            return (TResult)result;
        }
    }

    public async Task<object?> ScheduleTaskInternal(
        string name,
        string version,
        string? taskList,
        Type resultType,
        params object[] parameters) {
        int id = this._IdCounter++;
        string serializedInput = this.MessageDataConverter.SerializeInternal(parameters);
        var scheduleTaskTaskAction = new ScheduleTaskOrchestratorAction {
            Id = id,
            Name = name,
            Version = version,
            Tasklist = taskList,
            Input = serializedInput,
        };

        this._OrchestratorActionsMap.Add(id, scheduleTaskTaskAction);

        var tcs = new TaskCompletionSource<string?>();
        this._OpenTasks.Add(id, new OpenTaskInfo { Name = name, Version = version, Result = tcs });

        string? serializedResult = await tcs.Task;

        return this.MessageDataConverter.Deserialize(serializedResult, resultType);
    }

    public override async Task<T> CreateSubOrchestrationInstance<T>(
        string name,
        string version,
        string instanceId,
        object input) {
        return await this.CreateSubOrchestrationInstanceCore<T>(name, version, instanceId, input, null);
    }

    public override async Task<T> CreateSubOrchestrationInstance<T>(
        string name,
        string version,
        string instanceId,
        object input,
        IDictionary<string, string> tags) {
        return await this.CreateSubOrchestrationInstanceCore<T>(name, version, instanceId, input, tags);
    }

    public override async Task<T> CreateSubOrchestrationInstance<T>(
        string name,
        string version,
        object input) {
        return await this.CreateSubOrchestrationInstanceCore<T>(name, version, null, input, null);
    }

    private async Task<T> CreateSubOrchestrationInstanceCore<T>(
        string name,
        string version,
        string? instanceId,
        object input,
        IDictionary<string, string>? tags) {
        int id = this._IdCounter++;
        string serializedInput = this.MessageDataConverter.SerializeInternal(input);

        var actualInstanceId = instanceId;
        if (string.IsNullOrWhiteSpace(actualInstanceId)) {
            actualInstanceId = this.OrchestrationInstance.ExecutionId + ":" + id;
        }

        var action = new CreateSubOrchestrationAction(
            id: id,
            instanceId: actualInstanceId,
            name: name,
            version: version,
            input: serializedInput,
            tags: tags);

        this._OrchestratorActionsMap.Add(id, action);

        if (OrchestrationTags.IsTaggedAsFireAndForget(tags)) {
            // this is a fire-and-forget orchestration, so we do not wait for a result.
            return default(T)!;
        } else {
            var tcs = new TaskCompletionSource<string?>();
            this._OpenTasks.Add(id, new OpenTaskInfo { Name = name, Version = version, Result = tcs });

            string? serializedResult = await tcs.Task;

            if (this.MessageDataConverter.Deserialize<T>(serializedResult)
               .TryGetValue(out var result)) {
                return result;
            } else {
                return default(T)!;
            }
        }
    }

    public override void SendEvent(OrchestrationInstance orchestrationInstance, string eventName, object eventData) {
        if (string.IsNullOrWhiteSpace(orchestrationInstance?.InstanceId)) {
            throw new ArgumentException(nameof(orchestrationInstance));
        }

        int id = this._IdCounter++;

        string serializedEventData = this.MessageDataConverter.SerializeInternal(eventData);

        var action = new SendEventOrchestratorAction {
            Id = id,
            Instance = orchestrationInstance,
            EventName = eventName,
            EventData = serializedEventData,
        };

        this._OrchestratorActionsMap.Add(id, action);
    }

    public override void ContinueAsNew(object input) {
        this.ContinueAsNew(null, input);
    }

    public override void ContinueAsNew(string? newVersion, object input) {
        this.ContinueAsNewCore(newVersion, input);
    }

    private void ContinueAsNewCore(string? newVersion, object input) {
        string serializedInput = this.MessageDataConverter.SerializeInternal(input);

        this._ContinueAsNew = new OrchestrationCompleteOrchestratorAction {
            Result = serializedInput,
            OrchestrationStatus = OrchestrationStatus.ContinuedAsNew,
            NewVersion = newVersion
        };
    }

    public override Task<T> CreateTimer<T>(DateTime fireAt, T state) {
        return this.CreateTimer(fireAt, state, CancellationToken.None);
    }

    public override async Task<T> CreateTimer<T>(DateTime fireAt, T state, CancellationToken cancelToken) {
        int id = this._IdCounter++;
        var createTimerOrchestratorAction = new CreateTimerOrchestratorAction {
            Id = id,
            FireAt = fireAt,
        };

        this._OrchestratorActionsMap.Add(id, createTimerOrchestratorAction);

        var tcs = new TaskCompletionSource<string?>();
        this._OpenTasks.Add(id, new OpenTaskInfo { Name = null, Version = null, Result = tcs });

        if (cancelToken != CancellationToken.None) {
            _ = cancelToken.Register(s => {
                if (tcs.TrySetCanceled()) {
                    // TODO: Emit a log message that the timer is cancelled.
                    _ = this._OpenTasks.Remove(id);
                }
            }, tcs);
        }

        _ = await tcs.Task;

        return state;
    }

    public void HandleTaskScheduledEvent(TaskScheduledEvent scheduledEvent) {
        int taskId = scheduledEvent.EventId;
        if (!this._OrchestratorActionsMap.ContainsKey(taskId)) {
            throw new NonDeterministicOrchestrationException(scheduledEvent.EventId,
                $"A previous execution of this orchestration scheduled an activity task with sequence ID {taskId} and name "
                + $"'{scheduledEvent.Name}' (version '{scheduledEvent.Version}'), but the current replay execution hasn't "
                + "(yet?) scheduled this task. Was a change made to the orchestrator code after this instance had already "
                + "started running?");
        }

        var orchestrationAction = this._OrchestratorActionsMap[taskId];
        if (orchestrationAction is not ScheduleTaskOrchestratorAction currentReplayAction) {
            throw new NonDeterministicOrchestrationException(scheduledEvent.EventId,
                $"A previous execution of this orchestration scheduled an activity task with sequence number {taskId} named "
                + $"'{scheduledEvent.Name}', but the current orchestration replay instead produced a "
                + $"{orchestrationAction.GetType().Name} action with this sequence number. Was a change made to the "
                + "orchestrator code after this instance had already started running?");
        }

        if (!string.Equals(scheduledEvent.Name, currentReplayAction.Name, StringComparison.OrdinalIgnoreCase)) {
            throw new NonDeterministicOrchestrationException(scheduledEvent.EventId,
                $"A previous execution of this orchestration scheduled an activity task with sequence number {taskId} "
                + $"named '{scheduledEvent.Name}', but the current orchestration replay instead scheduled an activity "
                + $"task named '{currentReplayAction.Name}' with this sequence number.  Was a change made to the "
                + "orchestrator code after this instance had already started running?");
        }

        _ = this._OrchestratorActionsMap.Remove(taskId);
    }

    public void HandleTimerCreatedEvent(TimerCreatedEvent timerCreatedEvent) {
        int taskId = timerCreatedEvent.EventId;
        if (taskId == FrameworkConstants.FakeTimerIdToSplitDecision) {
            // This is our dummy timer to split decision for avoiding 100 messages per transaction service bus limit
            return;
        }

        if (!this._OrchestratorActionsMap.ContainsKey(taskId)) {
            throw new NonDeterministicOrchestrationException(timerCreatedEvent.EventId,
                $"A previous execution of this orchestration scheduled a timer task with sequence number {taskId} but "
                + "the current replay execution hasn't (yet?) scheduled this task. Was a change made to the orchestrator "
                + "code after this instance had already started running?");
        }

        var orchestrationAction = this._OrchestratorActionsMap[taskId];
        if (orchestrationAction is not CreateTimerOrchestratorAction) {
            throw new NonDeterministicOrchestrationException(timerCreatedEvent.EventId,
                $"A previous execution of this orchestration scheduled a timer task with sequence number {taskId} named "
                + $"but the current orchestration replay instead produced a {orchestrationAction.GetType().Name} action with "
                + "this sequence number. Was a change made to the orchestrator code after this instance had already "
                + "started running?");
        }

        _ = this._OrchestratorActionsMap.Remove(taskId);
    }

    public void HandleSubOrchestrationCreatedEvent(SubOrchestrationInstanceCreatedEvent subOrchestrationCreateEvent) {
        int taskId = subOrchestrationCreateEvent.EventId;
        if (!this._OrchestratorActionsMap.ContainsKey(taskId)) {
            throw new NonDeterministicOrchestrationException(subOrchestrationCreateEvent.EventId,
               $"A previous execution of this orchestration scheduled a sub-orchestration task with sequence ID {taskId} "
               + $"and name '{subOrchestrationCreateEvent.Name}' (version '{subOrchestrationCreateEvent.Version}', "
               + $"instance ID '{subOrchestrationCreateEvent.InstanceId}'), but the current replay execution hasn't (yet?) "
               + "scheduled this task. Was a change made to the orchestrator code after this instance had already started running?");
        }

        var orchestrationAction = this._OrchestratorActionsMap[taskId];
        if (orchestrationAction is not CreateSubOrchestrationAction currentReplayAction) {
            throw new NonDeterministicOrchestrationException(subOrchestrationCreateEvent.EventId,
               $"A previous execution of this orchestration scheduled a sub-orchestration task with sequence ID {taskId} "
               + $"and name '{subOrchestrationCreateEvent.Name}' (version '{subOrchestrationCreateEvent.Version}', "
               + $"instance ID '{subOrchestrationCreateEvent.InstanceId}'), but the current orchestration replay instead "
               + $"produced a {orchestrationAction.GetType().Name} action at this sequence number. Was a change made to "
               + "the orchestrator code after this instance had already started running?");
        }

        if (!string.Equals(subOrchestrationCreateEvent.Name, currentReplayAction.Name, StringComparison.OrdinalIgnoreCase)) {
            throw new NonDeterministicOrchestrationException(subOrchestrationCreateEvent.EventId,
               $"A previous execution of this orchestration scheduled a sub-orchestration task with sequence ID {taskId} "
               + $"and name '{subOrchestrationCreateEvent.Name}' (version '{subOrchestrationCreateEvent.Version}', "
               + $"instance ID '{subOrchestrationCreateEvent.InstanceId}'), but the current orchestration replay instead "
               + $"scheduled a sub-orchestration task with name {currentReplayAction.Name} at this sequence number. "
               + "Was a change made to the orchestrator code after this instance had already started running?");
        }

        _ = this._OrchestratorActionsMap.Remove(taskId);
    }

    public void HandleEventSentEvent(EventSentEvent eventSentEvent) {
        int taskId = eventSentEvent.EventId;
        if (!this._OrchestratorActionsMap.ContainsKey(taskId)) {
            throw new NonDeterministicOrchestrationException(eventSentEvent.EventId,
               $"A previous execution of this orchestration scheduled a send event task with sequence ID {taskId}, "
               + $"type '{eventSentEvent.EventType}' name '{eventSentEvent.Name}', instance ID '{eventSentEvent.InstanceId}', "
               + $"but the current replay execution hasn't (yet?) scheduled this task. Was a change made to the orchestrator code "
               + $"after this instance had already started running?");
        }

        var orchestrationAction = this._OrchestratorActionsMap[taskId];
        if (!(orchestrationAction is SendEventOrchestratorAction currentReplayAction)) {
            throw new NonDeterministicOrchestrationException(eventSentEvent.EventId,
               $"A previous execution of this orchestration scheduled a send event task with sequence ID {taskId}, "
               + $"type '{eventSentEvent.EventType}', name '{eventSentEvent.Name}', instance ID '{eventSentEvent.InstanceId}', "
               + $"but the current orchestration replay instead scheduled a {orchestrationAction.GetType().Name} task "
               + "at this sequence number. Was a change made to the orchestrator code after this instance had already "
               + "started running?");

        }

        if (!string.Equals(eventSentEvent.Name, currentReplayAction.EventName, StringComparison.OrdinalIgnoreCase)) {
            throw new NonDeterministicOrchestrationException(eventSentEvent.EventId,
               $"A previous execution of this orchestration scheduled a send event task with sequence ID {taskId}, "
               + $"type '{eventSentEvent.EventType}', name '{eventSentEvent.Name}', instance ID '{eventSentEvent.InstanceId}'), "
               + $"but the current orchestration replay instead scheduled a send event task with name {currentReplayAction.EventName}"
               + "at this sequence number. Was a change made to the orchestrator code after this instance had already "
               + "started running?");
        }

        _ = this._OrchestratorActionsMap.Remove(taskId);
    }

    public void HandleEventRaisedEvent(EventRaisedEvent eventRaisedEvent, bool skipCarryOverEvents, TaskOrchestration taskOrchestration) {
        if (skipCarryOverEvents || !this.HasContinueAsNew) {
            taskOrchestration.RaiseEvent(this, eventRaisedEvent.Name, eventRaisedEvent.Input);
        } else {
            this.AddEventToNextIteration(eventRaisedEvent);
        }
    }

    public void HandleTaskCompletedEvent(TaskCompletedEvent completedEvent) {
        int taskId = completedEvent.TaskScheduledId;
        if (this._OpenTasks.ContainsKey(taskId)) {
            OpenTaskInfo info = this._OpenTasks[taskId];
            info.Result.SetResult(completedEvent.Result);

            _ = this._OpenTasks.Remove(taskId);
        } else {
            this.LogDuplicateEvent("TaskCompleted", completedEvent, taskId);
        }
    }

    public void HandleTaskFailedEvent(TaskFailedEvent failedEvent) {
        int taskId = failedEvent.TaskScheduledId;
        if (this._OpenTasks.ContainsKey(taskId)) {
            OpenTaskInfo info = this._OpenTasks[taskId];

            // When using ErrorPropagationMode.SerializeExceptions the "cause" is deserialized from history.
            // This isn't fully reliable because not all exception types can be serialized/deserialized.
            // When using ErrorPropagationMode.UseFailureDetails we instead use FailureDetails to convey
            // error information, which doesn't involve any serialization at all.
            Exception? cause = this.ErrorPropagationMode == ErrorPropagationMode.SerializeExceptions ?
                Utils.RetrieveCause(failedEvent.Details, this.ErrorDataConverter) :
                null;

            var taskFailedException = new TaskFailedException(
                failedEvent.EventId,
                taskId,
                info.Name,
                info.Version,
                failedEvent.Reason,
                cause);

            taskFailedException.FailureDetails = failedEvent.FailureDetails;

            TaskCompletionSource<string?> tcs = info.Result;
            tcs.SetException(taskFailedException);

            _ = this._OpenTasks.Remove(taskId);
        } else {
            this.LogDuplicateEvent("TaskFailed", failedEvent, taskId);
        }
    }

    public void HandleSubOrchestrationInstanceCompletedEvent(SubOrchestrationInstanceCompletedEvent completedEvent) {
        int taskId = completedEvent.TaskScheduledId;
        if (this._OpenTasks.ContainsKey(taskId)) {
            OpenTaskInfo info = this._OpenTasks[taskId];
            info.Result.SetResult(completedEvent.Result);

            _ = this._OpenTasks.Remove(taskId);
        } else {
            this.LogDuplicateEvent("SubOrchestrationInstanceCompleted", completedEvent, taskId);
        }
    }

    public void HandleSubOrchestrationInstanceFailedEvent(SubOrchestrationInstanceFailedEvent failedEvent) {
        int taskId = failedEvent.TaskScheduledId;
        if (this._OpenTasks.ContainsKey(taskId)) {
            OpenTaskInfo info = this._OpenTasks[taskId];

            // When using ErrorPropagationMode.SerializeExceptions the "cause" is deserialized from history.
            // This isn't fully reliable because not all exception types can be serialized/deserialized.
            // When using ErrorPropagationMode.UseFailureDetails we instead use FailureDetails to convey
            // error information, which doesn't involve any serialization at all.
            Exception? cause = this.ErrorPropagationMode == ErrorPropagationMode.SerializeExceptions ?
                Utils.RetrieveCause(failedEvent.Details, this.ErrorDataConverter)
                : null;

            var failedException = new SubOrchestrationFailedException(
                failedEvent.EventId,
                taskId,
                info.Name,
                info.Version,
                failedEvent.Reason,
                cause);
            failedException.FailureDetails = failedEvent.FailureDetails;

            TaskCompletionSource<string?> tcs = info.Result;
            tcs.SetException(failedException);

            _ = this._OpenTasks.Remove(taskId);
        } else {
            this.LogDuplicateEvent("SubOrchestrationInstanceFailed", failedEvent, taskId);
        }
    }

    public void HandleTimerFiredEvent(TimerFiredEvent timerFiredEvent) {
        int taskId = timerFiredEvent.TimerId;
        if (this._OpenTasks.ContainsKey(taskId)) {
            OpenTaskInfo info = this._OpenTasks[taskId];
            info.Result.SetResult(timerFiredEvent.TimerId.ToString());
            _ = this._OpenTasks.Remove(taskId);
        } else {
            this.LogDuplicateEvent("TimerFired", timerFiredEvent, taskId);
        }
    }

    private void LogDuplicateEvent(string source, HistoryEvent historyEvent, int taskId) {
        TraceHelper.TraceSession(
            TraceEventType.Warning,
            "TaskOrchestrationContext-DuplicateEvent",
            this.OrchestrationInstance.InstanceId,
            "Duplicate {0} Event: {1}, type: {2}, ts: {3}",
            source,
            taskId.ToString(),
            historyEvent.EventType,
            historyEvent.Timestamp.ToString(CultureInfo.InvariantCulture));
    }

    public void HandleExecutionTerminatedEvent(ExecutionTerminatedEvent terminatedEvent) {
        this.CompleteOrchestration(terminatedEvent.Input, null, OrchestrationStatus.Terminated);
    }

    public void CompleteOrchestration(string result) {
        this.CompleteOrchestration(result, null, OrchestrationStatus.Completed);
    }

    public void HandleEventWhileSuspended(HistoryEvent historyEvent) {
        if (historyEvent.EventType != EventType.ExecutionSuspended) {
            this._EventsWhileSuspended.Enqueue(historyEvent);
        }
    }

    public void HandleExecutionSuspendedEvent(ExecutionSuspendedEvent suspendedEvent) {
        this.IsSuspended = true;
    }

    public void HandleExecutionResumedEvent(ExecutionResumedEvent resumedEvent, Action<HistoryEvent> eventProcessor) {
        this.IsSuspended = false;
        while (this._EventsWhileSuspended.Count > 0) {
            eventProcessor(this._EventsWhileSuspended.Dequeue());
        }
    }

    public void FailOrchestration(Exception failure) {
        ArgumentNullException.ThrowIfNull(failure, nameof(failure));

        string reason = failure.Message;

        // string details is legacy, FailureDetails is the newer way to share failure information
        string? details = null;
        FailureDetails? failureDetails = null;

        // correlation 
        CorrelationTraceClient.Propagate(
            () => {
                CorrelationTraceClient.TrackException(failure);
            });

        if (failure is OrchestrationFailureException orchestrationFailureException) {
            if (this.ErrorPropagationMode == ErrorPropagationMode.UseFailureDetails) {
                // When not serializing exceptions, we instead construct FailureDetails objects
                failureDetails = orchestrationFailureException.FailureDetails;
            } else {
                details = orchestrationFailureException.Details;
            }
        } else {
            if (this.ErrorPropagationMode == ErrorPropagationMode.UseFailureDetails) {
                failureDetails = new FailureDetails(failure);
            } else {
                details = $"Unhandled exception while executing orchestration: {failure}\n\t{failure.StackTrace}";
            }
        }

        this.CompleteOrchestration(reason, details, OrchestrationStatus.Failed, failureDetails);
    }

    public void CompleteOrchestration(string result, string? details, OrchestrationStatus orchestrationStatus, FailureDetails? failureDetails = null) {
        int id = this._IdCounter++;
        OrchestrationCompleteOrchestratorAction completedOrchestratorAction;
        if (orchestrationStatus == OrchestrationStatus.Completed && this._ContinueAsNew != null) {
            completedOrchestratorAction = this._ContinueAsNew;
        } else {
            if (this._ExecutionCompletedOrTerminated) {
                return;
            }

            this._ExecutionCompletedOrTerminated = true;

            completedOrchestratorAction = new OrchestrationCompleteOrchestratorAction();
            completedOrchestratorAction.Result = result;
            completedOrchestratorAction.Details = details;
            completedOrchestratorAction.OrchestrationStatus = orchestrationStatus;
            completedOrchestratorAction.FailureDetails = failureDetails;
        }

        completedOrchestratorAction.Id = id;
        this._OrchestratorActionsMap.Add(id, completedOrchestratorAction);
    }

    private class OpenTaskInfo {
        public required string? Name { get; set; }

        public required string? Version { get; set; }

        public required TaskCompletionSource<string?> Result { get; set; }
    }
}