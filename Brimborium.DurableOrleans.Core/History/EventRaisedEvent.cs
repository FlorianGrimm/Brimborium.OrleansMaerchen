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

namespace Orleans.DurableTask.Core.History;

/// <summary>
/// A history event for event raised
/// </summary>
[DataContract]
[GenerateSerializer]
[Alias("EventRaisedEvent")]
public class EventRaisedEvent : HistoryEvent, ISupportsDurableTraceContext {
    /// <summary>
    /// Creates a new <see cref="EventRaisedEvent"/> with the supplied event id and input.
    /// </summary>
    /// <param name="eventId">The ID of the event.</param>
    /// <param name="input">The serialized event payload.</param>
    public EventRaisedEvent(int eventId, string? input)
        : base(eventId) {
        this.Input = input;
    }

    /// <summary>
    /// Gets the event type
    /// </summary>
    public override EventType EventType => EventType.EventRaised;

    /// <summary>
    /// Gets or sets the orchestration name
    /// </summary>
    [DataMember]
    [Id(0)]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the serialized payload of the event
    /// </summary>
    [DataMember]
    [Id(1)]
    public string? Input { get; set; }

    /// <summary>
    /// The W3C trace context associated with this event.
    /// </summary>
    [DataMember]
    [Id(2)]
    public DistributedTraceContext? ParentTraceContext { get; set; }
}