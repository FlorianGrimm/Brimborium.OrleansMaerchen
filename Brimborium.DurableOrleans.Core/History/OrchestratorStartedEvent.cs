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
/// A history event for orchestrator started
/// </summary>
[DataContract]
[GenerateSerializer]
[Alias("OrchestratorStartedEvent")]
public class OrchestratorStartedEvent : HistoryEvent {
    /// <summary>
    /// Creates a new <see cref="OrchestratorStartedEvent"/> with the supplied parameters
    /// </summary>
    /// <param name="eventId">The event id of the history event</param>
    public OrchestratorStartedEvent(int eventId)
        : base(eventId) {
    }

    /// <summary>
    /// Gets the event type
    /// </summary>
    public override EventType EventType => EventType.OrchestratorStarted;
}