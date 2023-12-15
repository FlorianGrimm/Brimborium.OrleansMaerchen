// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

[DataContract]
class HistoryResponseReceived : ClientEvent
{
    [DataMember]
    public string ExecutionId { get; set; }

    [DataMember]
    public List<HistoryEvent> History { get; set; }
}
