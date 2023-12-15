// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

[DataContract]
abstract class ClientEvent : Event
{
    [DataMember]
    public Guid ClientId { get; set; }

    [DataMember]
    public long RequestId { get; set; }

    [IgnoreDataMember]
    public int ReceiveChannel { get; set; }

    [IgnoreDataMember]
    public override EventId EventId => EventId.MakeClientResponseEventId(this.ClientId, this.RequestId);
}
