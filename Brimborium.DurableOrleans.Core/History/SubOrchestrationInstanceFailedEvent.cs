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

namespace Orleans.DurableTask.Core.History;

/// <summary>
/// A history event for a sub orchestration instance failure
/// </summary>
[DataContract]
[GenerateSerializer]
[Alias("SubOrchestrationInstanceFailedEvent")]
public class SubOrchestrationInstanceFailedEvent : HistoryEvent {
    /// <summary>
    /// Creates a new SubOrchestrationInstanceFailedEvent with the supplied params
    /// </summary>
    /// <param name="eventId">The event id of the history event</param>
    /// <param name="taskScheduledId">The scheduled parent instance event id</param>
    /// <param name="reason">The sub orchestration failure reason</param>
    /// <param name="details">Details of the sub orchestration failure</param>
    /// <param name="failureDetails">Structured details of the sub orchestration failure.</param>
    public SubOrchestrationInstanceFailedEvent(int eventId, int taskScheduledId, string? reason, string? details, FailureDetails? failureDetails)
        : base(eventId) {
        this.TaskScheduledId = taskScheduledId;
        this.Reason = reason;
        this.Details = details;
        this.FailureDetails = failureDetails;
    }

    /// <inheritdoc cref="SubOrchestrationInstanceFailedEvent(int, int, string?, string?, FailureDetails?)"/>
    public SubOrchestrationInstanceFailedEvent(int eventId, int taskScheduledId, string? reason, string? details)
        : this(eventId, taskScheduledId, reason, details, null) {
    }

    // Needed for deserialization
    private SubOrchestrationInstanceFailedEvent()
        : base(-1) { }

    /// <summary>
    /// Gets the event type
    /// </summary>
    public override EventType EventType => EventType.SubOrchestrationInstanceFailed;

    /// <summary>
    /// Gets the scheduled parent instance event id
    /// </summary>
    [DataMember]
    [Id(0)]
    public int TaskScheduledId { get; private set; }

    /// <summary>
    /// Gets the sub orchestration failure reason
    /// </summary>
    [DataMember]
    [Id(1)]
    public string? Reason { get; private set; }

    /// <summary>
    /// Gets the details of the sub orchestration failure
    /// </summary>
    [DataMember]
    [Id(2)]
    public string? Details { get; private set; }

    /// <summary>
    /// Gets the structured details of the sub orchestration failure.
    /// </summary>
    [DataMember]
    [Id(3)]
    public FailureDetails? FailureDetails { get; internal set; }
}