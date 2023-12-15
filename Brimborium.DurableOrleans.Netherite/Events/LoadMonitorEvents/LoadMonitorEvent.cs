// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

[DataContract]
abstract class LoadMonitorEvent : Event
{
    [DataMember]
    public Guid RequestId { get; set; }

    public override EventId EventId => EventId.MakeLoadMonitorEventId(this.RequestId);
}
