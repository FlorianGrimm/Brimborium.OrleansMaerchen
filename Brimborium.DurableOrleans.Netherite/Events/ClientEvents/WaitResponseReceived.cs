// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

[DataContract]
class WaitResponseReceived : ClientEvent
{
    [DataMember]
    public OrchestrationState OrchestrationState { get; set; }

    protected override void ExtraTraceInformation(StringBuilder s)
    {
        s.Append(' ');
        s.Append(this.OrchestrationState.OrchestrationStatus.ToString());
    }
}
