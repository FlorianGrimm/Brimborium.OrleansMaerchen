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
/// Represents the state of an orchestration
/// </summary>
[DataContract]
[GenerateSerializer]
[Alias("OrchestrationState")]
public class OrchestrationState : IExtensibleDataObject {
    /// <summary>
    /// Completion datetime of the orchestration
    /// </summary>
    [DataMember]
    [Id(0)]
    public DateTime CompletedTime;

    /// <summary>
    /// The size of the compressed serialized runtime state
    /// </summary>
    [DataMember]
    [Id(1)]
    public long CompressedSize;

    /// <summary>
    /// Creation time of the orchestration
    /// </summary>
    [DataMember]
    [Id(2)]
    public DateTime CreatedTime;

    /// <summary>
    /// Serialized input of the orchestration
    /// </summary>
    [DataMember]
    [Id(3)]
    public string? Input;

    /// <summary>
    /// Last updated time of the orchestration
    /// </summary>
    [DataMember]
    [Id(4)]
    public DateTime LastUpdatedTime;

    /// <summary>
    /// The orchestration name
    /// </summary>
    [DataMember]
    [Id(5)]
    public string Name=string.Empty;

    /// <summary>
    /// The orchestration instance this state represents
    /// </summary>
    [DataMember]
    [Id(6)]
    public OrchestrationInstance? OrchestrationInstance;

    /// <summary>
    /// The current orchestration status
    /// </summary>
    [DataMember]
    [Id(7)]
    public OrchestrationStatus OrchestrationStatus;

    /// <summary>
    /// The serialized output of the orchestration
    /// </summary>
    [DataMember]
    [Id(8)]
    public string? Output;

    /// <summary>
    /// The parent instance if this is orchestration has one
    /// </summary>
    [DataMember]
    [Id(9)]
    public ParentInstance? ParentInstance;

    /// <summary>
    /// The size of the raw (uncompressed) serialized runtime state
    /// </summary>
    [DataMember]
    [Id(10)]
    public long Size;

    /// <summary>
    /// String status of the orchestration
    /// </summary>
    [DataMember]
    [Id(11)]
    public string? Status;

    /// <summary>
    /// The dictionary of tags and string values associated with this orchestration
    /// </summary>
    [DataMember]
    [Id(12)]
    public IDictionary<string, string>? Tags;

    /// <summary>
    /// The orchestration version
    /// </summary>
    [DataMember]
    [Id(13)]
    public string Version = string.Empty;

    /// <summary>
    /// The orchestration generation. Reused instanceIds will increment this value.
    /// </summary>
    [DataMember]
    [Id(14)]
    public int? Generation;

    /// <summary>
    /// Gets or sets date to start the orchestration
    /// </summary>
    [DataMember]
    [Id(15)]
    public DateTime? ScheduledStartTime { get; set; }

    /// <summary>
    /// Gets or sets failure details associated with the orchestration.
    /// </summary>
    [DataMember]
    [Id(16)]
    public FailureDetails? FailureDetails { get; set; }

    /// <summary>
    /// Clear input and/or output fields. Creates a shallow copy since
    /// we do not want to modify the original copy.
    /// </summary>
    /// <returns></returns>
    public OrchestrationState ClearFieldsImmutably(bool clearInput, bool clearOutput) {
        if (!(clearInput || clearOutput)) {
            return this;
        } else {
            // since we keep the OrchestrationState immutable in the backend, we must make a copy
            // before we can clear those fields
            var copy = (OrchestrationState)this.MemberwiseClone();

            if (clearInput) {
                copy.Input = null;
            }

            if (clearOutput) {
                copy.Output = null;
                copy.FailureDetails = null;
            }

            return copy;
        }
    }

    /// <summary>
    /// Implementation for <see cref="IExtensibleDataObject.ExtensionData"/>.
    /// </summary>
    [Id(17)]
    public ExtensionDataObject? ExtensionData { get; set; }
}