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
/// A history event for orchestration execution starting
/// </summary>
[DataContract]
[GenerateSerializer]
[Alias("ExecutionStartedEvent")]
public class ExecutionStartedEvent : HistoryEvent, ISupportsDurableTraceContext {
    /// <summary>
    /// The orchestration instance for this event
    /// </summary>
    [DataMember][Id(0)] public OrchestrationInstance OrchestrationInstance;

    /// <summary>
    /// Creates a new ExecutionStartedEvent with the supplied parameters
    /// </summary>
    /// <param name="eventId">The event id of the history event</param>
    /// <param name="input">The serialized orchestration input </param>
    public ExecutionStartedEvent(int eventId, string? input)
        : base(eventId) {
        this.Input = input;
    }

    /// <summary>
    /// Creates a new ExecutionStartedEvent
    /// </summary>
    internal ExecutionStartedEvent() {
    }

    /// <summary>
    /// Gets the event type
    /// </summary>
    public override EventType EventType => EventType.ExecutionStarted;

    /// <summary>
    /// Gets or sets the parent instance of the event 
    /// </summary>
    [DataMember]
    [Id(1)]
    public ParentInstance? ParentInstance { get; set; }

    /// <summary>
    /// Gets or sets the orchestration name
    /// </summary>
    [DataMember]
    [Id(2)]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the orchestration version
    /// </summary>
    [DataMember]
    [Id(3)]
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the serialized input to the orchestration
    /// </summary>
    [DataMember]
    [Id(4)]
    public string? Input { get; set; }

    /// <summary>
    /// Gets or sets a dictionary of tags of string, string
    /// </summary>
    [DataMember]
    [Id(5)]
    public IDictionary<string, string>? Tags { get; set; }

    // TODO: Make this property obsolete
    /// <summary>
    /// Gets or sets the serialized end-to-end correlation state.
    /// </summary>
    [DataMember]
    [Id(6)]
    public string Correlation { get; set; }

    /// <summary>
    /// The W3C trace context associated with this event.
    /// </summary>
    [DataMember]
    [Id(7)]
    public DistributedTraceContext? ParentTraceContext { get; set; }

    /// <summary>
    /// Gets or sets date to start the orchestration
    /// </summary>
    [DataMember]
    [Id(8)]
    public DateTime? ScheduledStartTime { get; set; }

    /// <summary>
    /// Gets or sets the generation of the orchestration
    /// </summary>
    [DataMember]
    [Id(9)]
    public int? Generation { get; set; }

    // Used for Continue-as-New scenarios
    internal void SetParentTraceContext(ExecutionStartedEvent? parent) {
        if (parent is not null) {
            this.ParentTraceContext = parent.ParentTraceContext;
        }
    }
}