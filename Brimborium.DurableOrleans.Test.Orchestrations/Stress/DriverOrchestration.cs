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

namespace Orleans.DurableTask.Test.Orchestrations.Stress;

public class DriverOrchestration : TaskOrchestration<int, DriverOrchestrationData> {
    public override async Task<int> RunTask(OrchestrationContext context, DriverOrchestrationData data) {
        var results = new List<Task<int>>();
        var i = 0;
        for (; i < data.NumberOfParallelTasks; i++) {
            results.Add(context.CreateSubOrchestrationInstance<int>(typeof(TestOrchestration), data.SubOrchestrationData));
        }

        int[] counters = await Task.WhenAll(results.ToArray());
        int result = counters.Max();

        if (data.NumberOfIteration > 1) {
            data.NumberOfIteration--;
            context.ContinueAsNew(data);
        }

        return result;
    }
}
