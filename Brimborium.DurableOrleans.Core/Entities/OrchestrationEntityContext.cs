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

namespace Orleans.DurableTask.Core.Entities;

/// <summary>
/// Tracks the entity-related state of an orchestration. 
/// Tracks and validates the synchronization state.
/// </summary>
public class OrchestrationEntityContext {
    private readonly string _InstanceId;
    private readonly string _ExecutionId;
    private readonly OrchestrationContext _InnerContext;
    private readonly MessageSorter _MessageSorter;

    private bool _LockAcquisitionPending;

    // the following are null unless we are inside a critical section
    private Guid? _CriticalSectionId;
    private EntityId[]? _CriticalSectionLocks;
    private HashSet<EntityId>? _AvailableLocks;

    /// <summary>
    /// Constructs an OrchestrationEntityContext.
    /// </summary>
    /// <param name="instanceId">The instance id.</param>
    /// <param name="executionId">The execution id.</param>
    /// <param name="innerContext">The inner context.</param>
    public OrchestrationEntityContext(
        string instanceId,
        string executionId,
        OrchestrationContext innerContext) {
        this._InstanceId = instanceId;
        this._ExecutionId = executionId;
        this._InnerContext = innerContext;
        this._MessageSorter = new MessageSorter();
    }

    /// <summary>
    /// Checks whether the configured backend supports entities.
    /// </summary>
    public bool EntitiesAreSupported => this._InnerContext.EntityParameters != null;

    /// <summary>
    /// Whether this orchestration is currently inside a critical section.
    /// </summary>
    public bool IsInsideCriticalSection => this._CriticalSectionId != null;

    /// <summary>
    /// The ID of the current critical section, or null if not currently in a critical section.
    /// </summary>
    public Guid? CurrentCriticalSectionId => this._CriticalSectionId;

    /// <summary>
    /// Enumerate all the entities that are available for calling from within a critical section. 
    /// This set contains all the entities that were locked prior to entering the critical section,
    /// and for which there is not currently an operation call pending.
    /// </summary>
    /// <returns>An enumeration of all the currently available entities.</returns>
    public IEnumerable<EntityId> GetAvailableEntities() {
        if (this.IsInsideCriticalSection) {
            foreach (var e in this._AvailableLocks!) {
                yield return e;
            }
        }
    }

    /// <summary>
    /// Check that a suborchestration is a valid transition in the current state.
    /// </summary>
    /// <param name="errorMessage">The error message, if it is not valid, or null otherwise</param>
    /// <returns>whether the transition is valid </returns>
    public bool ValidateSuborchestrationTransition(out string? errorMessage) {
        if (this.IsInsideCriticalSection) {
            errorMessage = "While holding locks, cannot call suborchestrators.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Check that acquire is a valid transition in the current state.
    /// </summary>
    /// <param name="oneWay">Whether this is a signal or a call.</param>
    /// <param name="targetInstanceId">The target instance id.</param>
    /// <param name="errorMessage">The error message, if it is not valid, or null otherwise</param>
    /// <returns>whether the transition is valid </returns>
    public bool ValidateOperationTransition(string targetInstanceId, bool oneWay, out string? errorMessage) {
        if (this.IsInsideCriticalSection) {
            var lockToUse = EntityId.FromString(targetInstanceId);
            if (oneWay) {
                if (this._CriticalSectionLocks is not null
                    && this._CriticalSectionLocks.Contains(lockToUse)) {
                    errorMessage = "Must not signal a locked entity from a critical section.";
                    return false;
                }
            } else {
                if (!this._AvailableLocks!.Remove(lockToUse)) {
                    if (this._LockAcquisitionPending) {
                        errorMessage = "Must await the completion of the lock request prior to calling any entity.";
                        return false;
                    }
                    if (this._CriticalSectionLocks is not null
                        && this._CriticalSectionLocks.Contains(lockToUse)) {
                        errorMessage = "Must not call an entity from a critical section while a prior call to the same entity is still pending.";
                        return false;
                    } else {
                        errorMessage = "Must not call an entity from a critical section if it is not one of the locked entities.";
                        return false;
                    }
                }
            }
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Check that acquire is a valid transition in the current state.
    /// </summary>
    /// <param name="errorMessage">The error message, if it is not valid, or null otherwise</param>
    /// <returns>whether the transition is valid </returns>
    public bool ValidateAcquireTransition(out string? errorMessage) {
        if (this.IsInsideCriticalSection) {
            errorMessage = "Must not enter another critical section from within a critical section.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Called after an operation call within a critical section completes.
    /// </summary>
    /// <param name="targetInstanceId"></param>
    public void RecoverLockAfterCall(string targetInstanceId) {
        if (this.IsInsideCriticalSection) {
            var lockToUse = EntityId.FromString(targetInstanceId);
            _ = this._AvailableLocks!.Add(lockToUse);
        }
    }

    /// <summary>
    /// Get release messages for all locks in the critical section, and release them
    /// </summary>
    public IEnumerable<EntityMessageEvent> EmitLockReleaseMessages() {
        if (this.IsInsideCriticalSection) {
            if (this._CriticalSectionLocks is not null) {
                var message = new ReleaseMessage() {
                    ParentInstanceId = this._InstanceId,
                    Id = this._CriticalSectionId!.Value.ToString(),
                };

                foreach (var entityId in this._CriticalSectionLocks) {
                    var instance = new OrchestrationInstance() { InstanceId = entityId.ToString() };
                    yield return new EntityMessageEvent(EntityMessageEventNames.ReleaseMessageEventName, message, instance);
                }
            }

            this._CriticalSectionLocks = null;
            this._AvailableLocks = null;
            this._CriticalSectionId = null;
        }
    }

    /// <summary>
    /// Creates a request message to be sent to an entity.
    /// </summary>
    /// <param name="target">The target entity.</param>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="oneWay">If true, this is a signal, otherwise it is a call.</param>
    /// <param name="operationId">A unique identifier for this request.</param>
    /// <param name="scheduledTimeUtc">A time for which to schedule the delivery, or null if this is not a scheduled message</param>
    /// <param name="input">The operation input</param>
    /// <returns>The event to send.</returns>
    public EntityMessageEvent EmitRequestMessage(
        OrchestrationInstance target,
        string operationName,
        bool oneWay,
        Guid operationId,
        (DateTime Original, DateTime Capped)? scheduledTimeUtc,
        string? input) {
        var request = new RequestMessage() {
            ParentInstanceId = this._InstanceId,
            ParentExecutionId = this._ExecutionId,
            Id = operationId,
            IsSignal = oneWay,
            Operation = operationName,
            ScheduledTime = scheduledTimeUtc?.Original,
            Input = input,
        };

        this.AdjustOutgoingMessage(target.InstanceId, request, scheduledTimeUtc?.Capped, out string eventName);

        return new EntityMessageEvent(eventName, request, target);
    }

    /// <summary>
    /// Creates an acquire message to be sent to an entity.
    /// </summary>
    /// <param name="lockRequestId">A unique request id.</param>
    /// <param name="entities">All the entities that are to be acquired.</param>
    /// <returns>The event to send.</returns>
    public EntityMessageEvent EmitAcquireMessage(Guid lockRequestId, EntityId[] entities) {
        // All the entities in entity[] need to be locked, but to avoid deadlock, the locks have to be acquired
        // sequentially, in order. So, we send the lock request to the first entity; when the first lock
        // is granted by the first entity, the first entity will forward the lock request to the second entity,
        // and so on; after the last entity grants the last lock, a response is sent back here.

        // acquire the locks in a globally fixed order to avoid deadlocks
        Array.Sort(entities);

        // remove duplicates if necessary. Probably quite rare, so no need to optimize more.
        for (int i = 0; i < entities.Length - 1; i++) {
            if (entities[i].Equals(entities[i + 1])) {
                entities = entities.Distinct().ToArray();
                break;
            }
        }

        // send lock request to first entity in the lock set
        var target = new OrchestrationInstance() { InstanceId = entities[0].ToString() };
        var request = new RequestMessage() {
            Id = lockRequestId,
            ParentInstanceId = this._InstanceId,
            ParentExecutionId = this._ExecutionId,
            LockSet = entities,
            Position = 0,
        };

        this._CriticalSectionId = lockRequestId;
        this._CriticalSectionLocks = entities;
        this._LockAcquisitionPending = true;

        this.AdjustOutgoingMessage(target.InstanceId, request, null, out string eventName);

        return new EntityMessageEvent(eventName, request, target);
    }

    /// <summary>
    /// Called when a response to the acquire message is received from the last entity.
    /// </summary>
    /// <param name="result">The result returned.</param>
    /// <param name="criticalSectionId">The guid for the lock operation</param>
    public void CompleteAcquire(OperationResult result, Guid criticalSectionId) {
        this._AvailableLocks = new HashSet<EntityId>(this._CriticalSectionLocks ?? []);
        this._LockAcquisitionPending = false;
    }

    internal void AdjustOutgoingMessage(string instanceId, RequestMessage requestMessage, DateTime? cappedTime, out string eventName) {
        if (cappedTime.HasValue) {
            eventName = EntityMessageEventNames.ScheduledRequestMessageEventName(cappedTime.Value);
        } else {
            this._MessageSorter.LabelOutgoingMessage(
                requestMessage,
                instanceId,
                this._InnerContext.CurrentUtcDateTime,
                this._InnerContext.EntityParameters?.EntityMessageReorderWindow ?? TimeSpan.Zero);

            eventName = EntityMessageEventNames.RequestMessageEventName;
        }
    }

    /// <summary>
    /// Extracts the operation result from an event that represents an entity response.
    /// </summary>
    /// <param name="eventContent">The serialized event content.</param>
    /// <returns></returns>
    public OperationResult DeserializeEntityResponseEvent(string eventContent) {
        var responseMessage = new ResponseMessage();

        // for compatibility, we deserialize in a way that is resilient to any typename presence/absence/mismatch
        try {
            // restore the scheduler state from the input
            JsonConvert.PopulateObject(eventContent, responseMessage, Serializer.InternalSerializerSettings);
        } catch (Exception exception) {
            throw new EntitySchedulerException("Failed to deserialize entity response.", exception);
        }

        return new OperationResult() {
            Result = responseMessage.Result,
            ErrorMessage = responseMessage.ErrorMessage,
            FailureDetails = responseMessage.FailureDetails,
        };
    }
}