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

namespace Orleans.DurableTask.Core;

/// <summary>
/// Represents the state of an orchestration instance
/// </summary>
[DataContract]
[GenerateSerializer]
[Alias("OrchestrationInstance")]
public class OrchestrationInstance : IExtensibleDataObject {
    /// <summary>
    /// The instance id, assigned as unique to the orchestration
    /// </summary>
    [DataMember]
    [Id(0)]
    public string InstanceId { get; set; }

    /// <summary>
    /// The execution id, unique to the execution of this instance
    /// </summary>
    [DataMember]
    [Id(1)]
    public string? ExecutionId { get; set; }

    internal OrchestrationInstance Clone() {
        return new OrchestrationInstance {
            ExecutionId = this.ExecutionId,
            InstanceId = this.InstanceId
        };
    }

    /// <summary>
    /// Serves as a hash function for an OrchestrationInstance. 
    /// </summary>
    /// <returns>
    /// A hash code for the current object.
    /// </returns>
    [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
    public override int GetHashCode() {
        return (this.InstanceId ?? string.Empty).GetHashCode() 
            ^ (this.ExecutionId ?? string.Empty).GetHashCode();
    }

    /// <summary>
    /// Returns a string that represents the OrchestrationInstance.
    /// </summary>
    /// <returns>
    /// A string that represents the current object.
    /// </returns>
    public override string ToString() {
        return $"[InstanceId: {this.InstanceId}, ExecutionId: {this.ExecutionId}]";
    }

    /// <summary>
    /// Implementation for <see cref="IExtensibleDataObject.ExtensionData"/>.
    /// </summary>
    [Id(2)]
    public ExtensionDataObject? ExtensionData { get; set; }
}