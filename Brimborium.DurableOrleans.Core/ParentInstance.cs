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

namespace Orleans.DurableTask.Core;

/// <summary>
/// Represents the parent orchestration of a sub orchestration
/// </summary>
[DataContract]
[GenerateSerializer]
[Alias("ParentInstance")]
public class ParentInstance : IExtensibleDataObject {
    /// <summary>
    /// The orchestration name of the parent instance
    /// </summary>
    [DataMember][Id(0)] public string Name;

    /// <summary>
    /// The orchestration instance of this parent instance
    /// </summary>
    [DataMember][Id(1)] public OrchestrationInstance OrchestrationInstance;

    /// <summary>
    /// The id of the child orchestration action
    /// </summary>
    [DataMember][Id(2)] public int TaskScheduleId;

    /// <summary>
    /// The orchestration version of the parent instance
    /// </summary>
    [DataMember][Id(3)] public string Version;

    internal ParentInstance Clone() {
        return new ParentInstance {
            Name = this.Name,
            Version = this.Version,
            TaskScheduleId = this.TaskScheduleId,
            OrchestrationInstance = this.OrchestrationInstance.Clone()
        };
    }

    /// <summary>
    /// Implementation for <see cref="IExtensibleDataObject.ExtensionData"/>.
    /// </summary>
    [Id(4)]
    public ExtensionDataObject ExtensionData { get; set; }
}