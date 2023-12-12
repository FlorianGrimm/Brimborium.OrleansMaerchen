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
/// A history event for a task completion
/// </summary>
[DataContract]
[GenerateSerializer]
[Alias("TaskCompletedEvent")]
public class TaskCompletedEvent : HistoryEvent {
    /// <summary>
    /// Creates a new TaskCompletedEvent with the supplied parameters
    /// </summary>
    /// <param name="eventId">The event id of the history event</param>
    /// <param name="taskScheduledId">The scheduled parent instance event id</param>
    /// <param name="result">The serialized result of the task</param>
    public TaskCompletedEvent(int eventId, int taskScheduledId, string? result)
        : base(eventId) {
        this.TaskScheduledId = taskScheduledId;
        this.Result = result;
    }

    /// <summary>
    /// Gets the event type
    /// </summary>
    public override EventType EventType => EventType.TaskCompleted;

    /// <summary>
    /// Gets the scheduled parent instance event id
    /// </summary>
    [DataMember]
    [Id(0)]
    public int TaskScheduledId { get; private set; }

    /// <summary>
    /// Gets the serialized result of the task
    /// </summary>
    [DataMember]
    [Id(1)]
    public string? Result { get; private set; }
}