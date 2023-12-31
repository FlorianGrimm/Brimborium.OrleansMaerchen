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

namespace Orleans.DurableTask.Core.Settings;

/// <summary>
///     Settings to configure the Task Orchestration Dispatcher
/// </summary>
public class TaskOrchestrationDispatcherSettings {
    /// <summary>
    ///     Creates a new instance of the TaskOrchestrationDispatcherSettings with default settings
    /// </summary>
    public TaskOrchestrationDispatcherSettings() {
        this.TransientErrorBackOffSecs = FrameworkConstants.OrchestrationTransientErrorBackOffSecs;
        this.NonTransientErrorBackOffSecs = FrameworkConstants.OrchestrationNonTransientErrorBackOffSecs;
        this.DispatcherCount = FrameworkConstants.OrchestrationDefaultDispatcherCount;
        this.MaxConcurrentOrchestrations = FrameworkConstants.OrchestrationDefaultMaxConcurrentItems;
        this.CompressOrchestrationState = false;
        this.EventBehaviourForContinueAsNew = BehaviorOnContinueAsNew.Carryover;
    }

    /// <summary>
    ///     Time in seconds to wait before retrying on a transient error (e.g. communication exception). Default is 10s.
    /// </summary>
    public int TransientErrorBackOffSecs { get; set; }

    /// <summary>
    ///     Time in seconds to wait before retrying on a non-transient error. Default is 120s.
    /// </summary>
    public int NonTransientErrorBackOffSecs { get; set; }

    /// <summary>
    ///     How many dispatchers to create. Default is 1.
    /// </summary>
    public int DispatcherCount { get; set; }

    /// <summary>
    ///     How many orchestrations to process concurrently. Default is 100.
    /// </summary>
    public int MaxConcurrentOrchestrations { get; set; }

    /// <summary>
    ///     Compress the orchestration state to enable more complex orchestrations at the cost of throughput. Default is False.
    /// </summary>
    public bool CompressOrchestrationState { get; set; }

    /// <summary>
    ///  Should we carry over unexecuted raised events to the next iteration of an orchestration on ContinueAsNew
    /// </summary>
    public BehaviorOnContinueAsNew EventBehaviourForContinueAsNew { get; set; }

    internal TaskOrchestrationDispatcherSettings Clone() {
        return new TaskOrchestrationDispatcherSettings {
            TransientErrorBackOffSecs = this.TransientErrorBackOffSecs,
            NonTransientErrorBackOffSecs = this.NonTransientErrorBackOffSecs,
            MaxConcurrentOrchestrations = this.MaxConcurrentOrchestrations,
            CompressOrchestrationState = this.CompressOrchestrationState,
        };
    }
}