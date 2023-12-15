// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

[DataContract]
abstract class ClientReadonlyRequestEvent : PartitionReadEvent, IClientRequestEvent
{
    [DataMember]
    public Guid ClientId { get; set; }

    [DataMember]
    public long RequestId { get; set; }

    [DataMember]
    public DateTime TimeoutUtc { get; set; }

    [IgnoreDataMember]
    public override EventId EventId => EventId.MakeClientRequestEventId(this.ClientId, this.RequestId);
}