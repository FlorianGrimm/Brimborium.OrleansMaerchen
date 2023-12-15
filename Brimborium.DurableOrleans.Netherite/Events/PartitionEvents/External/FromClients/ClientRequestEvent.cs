// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

[DataContract]
abstract class ClientRequestEvent : PartitionUpdateEvent, IClientRequestEvent
{
    [DataMember]
    public Guid ClientId { get; set; }

    [DataMember]
    public long RequestId { get; set; }

    [IgnoreDataMember]
    public string WorkItemId => WorkItemTraceHelper.FormatClientWorkItemId(this.ClientId, this.RequestId);

    [DataMember]
    public DateTime TimeoutUtc { get; set; }

    [IgnoreDataMember]
    public override EventId EventId => EventId.MakeClientRequestEventId(this.ClientId, this.RequestId);
}