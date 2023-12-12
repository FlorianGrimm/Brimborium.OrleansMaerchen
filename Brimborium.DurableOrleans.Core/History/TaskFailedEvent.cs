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
/// A history event for a task failure
/// </summary>
[DataContract]
[GenerateSerializer]
[Alias("TaskFailedEvent")]
public class TaskFailedEvent : HistoryEvent {
    /// <summary>
    /// Creates a new TaskFailedEvent with the supplied parameters
    /// </summary>
    /// <param name="eventId">The event id of the history event</param>
    /// <param name="taskScheduledId">The scheduled parent instance event id</param>
    /// <param name="reason">The task failure reason</param>
    /// <param name="details">Serialized details of the task failure</param>
    /// <param name="failureDetails">Structured details of the task failure.</param>
    public TaskFailedEvent(int eventId, int taskScheduledId, string? reason, string? details, FailureDetails? failureDetails)
        : base(eventId) {
        this.TaskScheduledId = taskScheduledId;
        this.Reason = reason;
        this.Details = details;
        this.FailureDetails = failureDetails;
    }

    /// <inheritdoc cref="TaskFailedEvent(int, int, string?, string?, FailureDetails?)"/>
    public TaskFailedEvent(int eventId, int taskScheduledId, string? reason, string? details)
        : this(eventId, taskScheduledId, reason, details, failureDetails: null) {
    }

    // Needed for deserialization
    private TaskFailedEvent()
        : this(-1, 0, default, default, default) { }

    /// <summary>
    /// Gets the event type
    /// </summary>
    public override EventType EventType => EventType.TaskFailed;

    /// <summary>
    /// Gets the scheduled parent instance event id
    /// </summary>
    [DataMember]
    [Id(0)]
    public int TaskScheduledId { get; private set; }

    /// <summary>
    /// Gets the task failure reason
    /// </summary>
    [DataMember]
    [Id(1)]
    public string? Reason { get; private set; }

    /// <summary>
    /// Gets details of the task failure
    /// </summary>
    [DataMember]
    [Id(2)]
    public string? Details { get; private set; }

    /// <summary>
    /// Gets the structured details of the task failure.
    /// </summary>
    [DataMember]
    [Id(3)]
    public FailureDetails? FailureDetails { get; private set; }
}