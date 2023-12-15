// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

[DataContract]
class StateResponseReceived : ClientEvent
{
    [DataMember]
    public OrchestrationState OrchestrationState { get; set; }
}
