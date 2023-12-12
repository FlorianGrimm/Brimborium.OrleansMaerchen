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

#nullable enable

namespace Orleans.DurableTask.Emulator;

/// <summary>
/// Fully functional in-proc orchestration service for testing
/// </summary>
public class LocalOrchestrationService
    : IOrchestrationService
    , IOrchestrationServiceClient
    , IDisposable {
    // ReSharper disable once NotAccessedField.Local
    //Dictionary<string, byte[]> _DictSessionState;
    private readonly List<TaskMessage> _ListTimerMessage;

    public readonly int MaxConcurrentWorkItems = 20;

    private readonly PeekLockSessionQueue _OrchestratorQueue;
    private readonly PeekLockQueue _WorkerQueue;

    private readonly CancellationTokenSource _CancellationTokenSource;

    private readonly Dictionary<string, Dictionary<string, OrchestrationState>> _DictInstanceStore;

    //Dictionary<string, Tuple<List<TaskMessage>, byte[]>> sessionLock;

    private readonly object _ThisLock = new object();
    private readonly object _TimerLock = new object();

    private readonly ConcurrentDictionary<string, TaskCompletionSource<OrchestrationState>> _DictOrchestrationWaiters;
    private static readonly JsonSerializerSettings StateJsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };

    /// <summary>
    ///     Creates a new instance of the LocalOrchestrationService with default settings
    /// </summary>
    public LocalOrchestrationService() {
        this._OrchestratorQueue = new PeekLockSessionQueue();
        this._WorkerQueue = new PeekLockQueue();

        //this._DictSessionState = new Dictionary<string, byte[]>();

        this._ListTimerMessage = new List<TaskMessage>();
        this._DictInstanceStore = new Dictionary<string, Dictionary<string, OrchestrationState>>();
        this._DictOrchestrationWaiters = new ConcurrentDictionary<string, TaskCompletionSource<OrchestrationState>>();
        this._CancellationTokenSource = new CancellationTokenSource();
    }

    private async Task TimerMessageSchedulerAsync() {
        while (!this._CancellationTokenSource.Token.IsCancellationRequested) {
            lock (this._TimerLock) {
                foreach (TaskMessage tm in this._ListTimerMessage.ToList()) {
                    if (tm.Event is not TimerFiredEvent te) {
                        // TODO : unobserved task exception (AFFANDAR)
                        throw new InvalidOperationException("Invalid timer message");
                    } else {
                        if (te.FireAt <= DateTime.UtcNow) {
                            this._OrchestratorQueue.SendMessage(tm);
                            _ = this._ListTimerMessage.Remove(tm);
                        }
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    /******************************/
    // management methods
    /******************************/
    /// <inheritdoc />
    public Task CreateAsync() {
        return this.CreateAsync(true);
    }

    /// <inheritdoc />
    public Task CreateAsync(bool recreateInstanceStore) {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CreateIfNotExistsAsync() {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync() {
        return this.DeleteAsync(true);
    }

    /// <inheritdoc />
    public Task DeleteAsync(bool deleteInstanceStore) {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StartAsync() {
        _ = Task.Run(() => this.TimerMessageSchedulerAsync());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(bool isForced) {
        this._CancellationTokenSource.Cancel();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync() {
        return this.StopAsync(false);
    }

    /// <summary>
    /// Determines whether is a transient or not.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>
    ///   <c>true</c> if is transient exception; otherwise, <c>false</c>.
    /// </returns>
    public bool IsTransientException(Exception exception) {
        return false;
    }

    /******************************/
    // client methods
    /******************************/
    /// <inheritdoc />
    public Task CreateTaskOrchestrationAsync(TaskMessage creationMessage) {
        return this.CreateTaskOrchestrationAsync(creationMessage, null);
    }

    /// <inheritdoc />
    public Task CreateTaskOrchestrationAsync(
        TaskMessage creationMessage,
        OrchestrationStatus[]? dedupeStatuses) {
        if (creationMessage.Event is not ExecutionStartedEvent ee) {
            throw new InvalidOperationException("Invalid creation task message");
        }

        ArgumentException.ThrowIfNullOrEmpty(
            creationMessage.OrchestrationInstance.ExecutionId,
            nameof(creationMessage));

        lock (this._ThisLock) {
            if (!this._DictInstanceStore.TryGetValue(
                creationMessage.OrchestrationInstance.InstanceId,
                out var ed)) {
                this._DictInstanceStore[creationMessage.OrchestrationInstance.InstanceId] = ed = new();
            }

            OrchestrationState? latestState = ed.Values
                .OrderBy(state => state.CreatedTime)
                .FirstOrDefault(state => state.OrchestrationStatus != OrchestrationStatus.ContinuedAsNew);

            if (latestState != null
                && (dedupeStatuses == null
                    || dedupeStatuses.Contains(latestState.OrchestrationStatus))) {
                // An orchestration with same instance id is already running
                throw new OrchestrationAlreadyExistsException($"An orchestration with id '{creationMessage.OrchestrationInstance.InstanceId}' already exists. It is in state {latestState.OrchestrationStatus}");
            }

            var newState = new OrchestrationState {
                OrchestrationInstance = new OrchestrationInstance {
                    InstanceId = creationMessage.OrchestrationInstance.InstanceId,
                    ExecutionId = creationMessage.OrchestrationInstance.ExecutionId,
                },
                CreatedTime = DateTime.UtcNow,
                LastUpdatedTime = DateTime.UtcNow,
                OrchestrationStatus = OrchestrationStatus.Pending,
                Version = ee.Version,
                Name = ee.Name,
                Input = ee.Input,
                ScheduledStartTime = ee.ScheduledStartTime,
                Tags = ee.Tags
            };

            ed.Add(creationMessage.OrchestrationInstance.ExecutionId, newState);

            this._OrchestratorQueue.SendMessage(creationMessage);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendTaskOrchestrationMessageAsync(TaskMessage message) {
        return this.SendTaskOrchestrationMessageBatchAsync(message);
    }

    /// <inheritdoc />
    public Task SendTaskOrchestrationMessageBatchAsync(params TaskMessage[] messages) {
        foreach (TaskMessage message in messages) {
            this._OrchestratorQueue.SendMessage(message);
        }

        return Task.FromResult<object?>(null);
    }

    /// <inheritdoc />
    public async Task<OrchestrationState> WaitForOrchestrationAsync(
        string instanceId,
        string? executionId,
        TimeSpan timeout,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(executionId)) {
            executionId = string.Empty;
        }

        string key = instanceId + "_" + executionId;

        if (!this._DictOrchestrationWaiters.TryGetValue(key, out var tcs)) {
            tcs = new TaskCompletionSource<OrchestrationState>();

            if (!this._DictOrchestrationWaiters.TryAdd(key, tcs)) {
                _ = this._DictOrchestrationWaiters.TryGetValue(key, out tcs);
            }

            if (tcs == null) {
                throw new InvalidOperationException("Unable to get tcs from orchestrationWaiters");
            }
        }

        // might have finished already
        lock (this._ThisLock) {
            if (this._DictInstanceStore.ContainsKey(instanceId)) {
                Dictionary<string, OrchestrationState> stateMap = this._DictInstanceStore[instanceId];

                if (stateMap != null && stateMap.Count > 0) {
                    OrchestrationState? state;
                    if (string.IsNullOrWhiteSpace(executionId)) {
                        IOrderedEnumerable<OrchestrationState> sortedStateMap = stateMap.Values.OrderByDescending(os => os.CreatedTime);
                        state = sortedStateMap.First();
                    } else {
                        if (stateMap.ContainsKey(executionId)) {
                            state = this._DictInstanceStore[instanceId][executionId];
                        } else {
                            state = null;
                        }
                    }

                    if (state != null
                        && state.OrchestrationStatus != OrchestrationStatus.Running
                        && state.OrchestrationStatus != OrchestrationStatus.Pending) {
                        // if only master id was specified then continueAsNew is a not a terminal state
                        if (!(string.IsNullOrWhiteSpace(executionId)
                                && state.OrchestrationStatus == OrchestrationStatus.ContinuedAsNew)) {
                            _ = tcs.TrySetResult(state);
                        }
                    }
                }
            }
        }

        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            this._CancellationTokenSource.Token);
        Task timeOutTask = Task.Delay(timeout, cts.Token);
        Task ret = await Task.WhenAny(tcs.Task, timeOutTask);

        if (ret == timeOutTask) {
            throw new TimeoutException("timed out or canceled while waiting for orchestration to complete");
        }

        cts.Cancel();

        return await tcs.Task;
    }

    /// <inheritdoc />
    public async Task<OrchestrationState?> GetOrchestrationStateAsync(
        string/*!*/ instanceId,
        string/*!*/ executionId) {
        OrchestrationState? response;

        lock (this._ThisLock) {
            if (!(
                this._DictInstanceStore.TryGetValue(instanceId, out var state)
                && state.TryGetValue(executionId, out response))) {
                response = null;
            }
        }

        return await Task.FromResult(response);
    }

    /// <inheritdoc />
    public async Task<IList<OrchestrationState>> GetOrchestrationStateAsync(
        string instanceId,
        bool allExecutions) {
        IList<OrchestrationState> response;

        lock (this._ThisLock) {
            if (this._DictInstanceStore.TryGetValue(instanceId, out var state)) {
                response = state.Values.ToList();
            } else {
                response = new List<OrchestrationState>();
            }
        }

        return await Task.FromResult(response);
    }

    /// <inheritdoc />
    public Task<string> GetOrchestrationHistoryAsync(string instanceId, string executionId) {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public Task PurgeOrchestrationHistoryAsync(DateTime thresholdDateTimeUtc, OrchestrationStateTimeRangeFilterType timeRangeFilterType) {
        throw new NotSupportedException();
    }

    /******************************/
    // Task orchestration methods
    /******************************/
    /// <inheritdoc />
    public int MaxConcurrentTaskOrchestrationWorkItems => this.MaxConcurrentWorkItems;

    /// <inheritdoc />
    public async Task<TaskOrchestrationWorkItem?> LockNextTaskOrchestrationWorkItemAsync(
        TimeSpan receiveTimeout,
        CancellationToken cancellationToken) {
        TaskSession taskSession = await this._OrchestratorQueue.AcceptSessionAsync(receiveTimeout,
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this._CancellationTokenSource.Token).Token);

        if (taskSession == null) {
            return null;
        }

        var wi = new TaskOrchestrationWorkItem(
#warning TODO:!
            instanceId: taskSession.Id!,
            orchestrationRuntimeState:
                this.DeserializeOrchestrationRuntimeState(taskSession.SessionState) ??
                new OrchestrationRuntimeState(),
            lockedUntilUtc: DateTime.UtcNow.AddMinutes(5),
            newMessages: taskSession.Messages.ToList(),
            session: null,
            traceContext: null,
            isExtendedSession: false
        );

        return wi;
    }

    /// <inheritdoc />
    public Task CompleteTaskOrchestrationWorkItemAsync(
        TaskOrchestrationWorkItem workItem,
        OrchestrationRuntimeState? newOrchestrationRuntimeState,
        IList<TaskMessage>? outboundMessages,
        IList<TaskMessage> orchestratorMessages,
        IList<TaskMessage>? workItemTimerMessages,
        TaskMessage? continuedAsNewMessage,
        OrchestrationState? state) {
        lock (this._ThisLock) {
            byte[]? newSessionState;

            if (newOrchestrationRuntimeState is null
                || newOrchestrationRuntimeState.ExecutionStartedEvent is null
                || newOrchestrationRuntimeState.OrchestrationStatus != OrchestrationStatus.Running) {
                newSessionState = null;
            } else {
                newSessionState = this.SerializeOrchestrationRuntimeState(newOrchestrationRuntimeState);
            }

            this._OrchestratorQueue.CompleteSession(
                workItem.InstanceId,
                newSessionState,
                orchestratorMessages,
                continuedAsNewMessage
                );

            if (outboundMessages != null) {
                foreach (TaskMessage m in outboundMessages) {
                    // TODO : make async (AFFANDAR)
                    this._WorkerQueue.SendMessageAsync(m);
                }
            }

            if (workItemTimerMessages != null) {
                lock (this._TimerLock) {
                    foreach (TaskMessage m in workItemTimerMessages) {
                        this._ListTimerMessage.Add(m);
                    }
                }
            }

            if (workItem.OrchestrationRuntimeState != newOrchestrationRuntimeState) {
                var oldState = Utils.BuildOrchestrationState(workItem.OrchestrationRuntimeState);
                this.CommitState(workItem.OrchestrationRuntimeState, oldState).GetAwaiter().GetResult();
            }

            if (state != null) {
                if (newOrchestrationRuntimeState is null) {
                    throw new InvalidOperationException("newOrchestrationRuntimeState is null");
                }
                this.CommitState(newOrchestrationRuntimeState, state).GetAwaiter().GetResult();
            }
        }

        return Task.FromResult(0);
    }

    private Task CommitState(OrchestrationRuntimeState runtimeState, OrchestrationState state) {
        ArgumentNullException.ThrowIfNull(runtimeState.OrchestrationInstance, nameof(runtimeState));
        var instanceId = runtimeState.OrchestrationInstance.InstanceId;
        var executionId = runtimeState.OrchestrationInstance.ExecutionId;
        ArgumentNullException.ThrowIfNull(instanceId, nameof(runtimeState.OrchestrationInstance.InstanceId));
        ArgumentNullException.ThrowIfNull(executionId, nameof(runtimeState.OrchestrationInstance.ExecutionId));

        if (!this._DictInstanceStore.TryGetValue(
            instanceId,
            out var mapState)) {
            mapState = new Dictionary<string, OrchestrationState>();
            this._DictInstanceStore[instanceId] = mapState;
        }

        mapState[executionId] = state;

        // signal any waiters waiting on instanceid_executionid or just the latest instanceid_

        if (state.OrchestrationStatus == OrchestrationStatus.Running
            || state.OrchestrationStatus == OrchestrationStatus.Pending) {
            return Task.FromResult(0);
        }

        string key = instanceId + "_" +
            executionId;

        string key1 = instanceId + "_";

        var tasks = new List<Task>();

        if (this._DictOrchestrationWaiters.TryGetValue(key, out var tcs)) {
            tasks.Add(Task.Run(() => tcs.TrySetResult(state)));
        }

        // for instance id level waiters, we will not consider ContinueAsNew as a terminal state because
        // the high level orchestration is still ongoing
        if (state.OrchestrationStatus != OrchestrationStatus.ContinuedAsNew
            && this._DictOrchestrationWaiters.TryGetValue(key1, out var tcs1)) {
            tasks.Add(Task.Run(() => tcs1.TrySetResult(state)));
        }

        if (tasks.Count > 0) {
            Task.WaitAll(tasks.ToArray());
        }

        return Task.FromResult(0);
    }

    /// <inheritdoc />
    public Task AbandonTaskOrchestrationWorkItemAsync(TaskOrchestrationWorkItem workItem) {
        this._OrchestratorQueue.AbandonSession(workItem.InstanceId);
        return Task.FromResult<object?>(null);
    }

    /// <inheritdoc />
    public Task ReleaseTaskOrchestrationWorkItemAsync(TaskOrchestrationWorkItem workItem) {
        return Task.FromResult<object?>(null);
    }

    /// <inheritdoc />
    public int TaskActivityDispatcherCount => 1;

    /// <summary>
    ///  Should we carry over unexecuted raised events to the next iteration of an orchestration on ContinueAsNew
    /// </summary>
    public BehaviorOnContinueAsNew EventBehaviourForContinueAsNew => BehaviorOnContinueAsNew.Carryover;

    /// <inheritdoc />
    public int MaxConcurrentTaskActivityWorkItems => this.MaxConcurrentWorkItems;

    /// <inheritdoc />
    public async Task ForceTerminateTaskOrchestrationAsync(string instanceId, string message) {
        var taskMessage = new TaskMessage {
            OrchestrationInstance = new OrchestrationInstance { InstanceId = instanceId },
            Event = new ExecutionTerminatedEvent(-1, message)
        };

        await this.SendTaskOrchestrationMessageAsync(taskMessage);
    }

    /// <inheritdoc />
    public Task RenewTaskOrchestrationWorkItemLockAsync(TaskOrchestrationWorkItem workItem) {
        workItem.LockedUntilUtc = workItem.LockedUntilUtc.AddMinutes(5);
        return Task.FromResult(0);
    }

    /// <inheritdoc />
    public bool IsMaxMessageCountExceeded(int currentMessageCount, OrchestrationRuntimeState runtimeState) {
        return false;
    }

    /// <inheritdoc />
    public int GetDelayInSecondsAfterOnProcessException(Exception exception) {
        return 0;
    }

    /// <inheritdoc />
    public int GetDelayInSecondsAfterOnFetchException(Exception exception) {
        return 0;
    }

    /// <inheritdoc />
    public int TaskOrchestrationDispatcherCount => 1;

    /******************************/
    // Task activity methods
    /******************************/
    /// <inheritdoc />
    public async Task<TaskActivityWorkItem?> LockNextTaskActivityWorkItem(TimeSpan receiveTimeout, CancellationToken cancellationToken) {
        TaskMessage taskMessage = await this._WorkerQueue.ReceiveMessageAsync(receiveTimeout,
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this._CancellationTokenSource.Token).Token);

        if (taskMessage == null) {
            return null;
        }

        return new TaskActivityWorkItem {
            // for the in memory provider we will just use the TaskMessage object ref itself as the id
            Id = "N/A",
            LockedUntilUtc = DateTime.UtcNow.AddMinutes(5),
            TaskMessage = taskMessage,
        };
    }

    /// <inheritdoc />
    public Task AbandonTaskActivityWorkItemAsync(TaskActivityWorkItem workItem) {
        this._WorkerQueue.AbandonMessageAsync(workItem.TaskMessage);
        return Task.FromResult<object?>(null);
    }

    /// <inheritdoc />
    public Task CompleteTaskActivityWorkItemAsync(TaskActivityWorkItem workItem, TaskMessage responseMessage) {
        lock (this._ThisLock) {
            this._WorkerQueue.CompleteMessageAsync(workItem.TaskMessage);
            this._OrchestratorQueue.SendMessage(responseMessage);
        }

        return Task.FromResult<object?>(null);
    }

    /// <inheritdoc />
    public Task<TaskActivityWorkItem?> RenewTaskActivityWorkItemLockAsync(TaskActivityWorkItem workItem) {
        // TODO : add expiration if we want to unit test it (AFFANDAR)
        workItem.LockedUntilUtc = workItem.LockedUntilUtc.AddMinutes(5);
        return Task.FromResult<TaskActivityWorkItem?>(workItem);
    }

    private byte[]? SerializeOrchestrationRuntimeState(OrchestrationRuntimeState runtimeState) {
        if (runtimeState == null) {
            return null;
        }

        string serializeState = JsonConvert.SerializeObject(runtimeState.Events, StateJsonSettings);
        return Encoding.UTF8.GetBytes(serializeState);
    }

    private OrchestrationRuntimeState? DeserializeOrchestrationRuntimeState(byte[]? stateBytes) {
        if (stateBytes == null || stateBytes.Length == 0) {
            return null;
        }

        string serializedState = Encoding.UTF8.GetString(stateBytes);
        var events = JsonConvert.DeserializeObject<IList<HistoryEvent>>(serializedState, StateJsonSettings);
        return new OrchestrationRuntimeState(events);
    }

    /// <inheritdoc />
    public void Dispose() {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing) {
        if (disposing) {
            this._CancellationTokenSource.Cancel();
            this._CancellationTokenSource.Dispose();
        }
    }
}
