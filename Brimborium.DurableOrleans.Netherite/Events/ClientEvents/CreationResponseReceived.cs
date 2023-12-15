// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

[DataContract]
class CreationResponseReceived : ClientEvent
{
    [DataMember]
    public bool Succeeded { get; set; }

    [DataMember]
    public OrchestrationStatus? ExistingInstanceOrchestrationStatus { get; set; }
}
