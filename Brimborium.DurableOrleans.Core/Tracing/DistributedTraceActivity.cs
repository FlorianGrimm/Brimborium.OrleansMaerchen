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

using System;
using System.Diagnostics;
using System.Threading;
using Newtonsoft.Json;

namespace Orleans.DurableTask.Core.Tracing;

/// <summary>
/// Manage Activity for orchestration execution.
/// </summary>
internal class DistributedTraceActivity
{
    private static readonly AsyncLocal<Activity> CurrentActivity = new AsyncLocal<Activity>();

    /// <summary>
    /// Share the Activity across an orchestration execution.
    /// </summary>
    internal static Activity Current
    {
        get { return CurrentActivity.Value; }
        set { CurrentActivity.Value = value; }
    }
}
