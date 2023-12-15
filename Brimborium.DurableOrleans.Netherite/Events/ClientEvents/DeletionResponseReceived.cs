// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

[DataContract]
class DeletionResponseReceived : ClientEvent
{
    [DataMember]
    public int NumberInstancesDeleted { get; set; }
}
