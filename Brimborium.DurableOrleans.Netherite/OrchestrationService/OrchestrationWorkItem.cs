// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

class OrchestrationWorkItem : TaskOrchestrationWorkItem {
    public OrchestrationMessageBatch MessageBatch { get; set; }

    public Partition Partition { get; }

    public ExecutionType Type { get; set; }

    public enum ExecutionType { Fresh, ContinueFromHistory, ContinueFromCursor };

    public int EventCount;

    public List<string> NewMessagesOrigin { get; set; }

    public double StartedAt { get; set; }

    public string CustomStatus { get; set; }

    public override bool RestoreOriginalRuntimeStateDuringCompletion => false;

    public OrchestrationWorkItem(Partition partition, OrchestrationMessageBatch messageBatch, List<HistoryEvent> previousHistory = null, string customStatus = null) {
        this.Partition = partition;
        this.MessageBatch = messageBatch;
        this.InstanceId = messageBatch.InstanceId;
        this.NewMessages = messageBatch.MessagesToProcess;
        this.NewMessagesOrigin = messageBatch.MessagesToProcessOrigin;
        this.OrchestrationRuntimeState = new OrchestrationRuntimeState(previousHistory) { Status = customStatus };
        this.CustomStatus = customStatus;
        this.LockedUntilUtc = DateTime.MaxValue; // this backend does not require workitem lock renewals
        this.Session = null; // we don't use the extended session API because we are caching cursors in the work item
    }

    public void SetNextMessageBatch(OrchestrationMessageBatch messageBatch) {
        this.MessageBatch = messageBatch;
        this.NewMessages = messageBatch.MessagesToProcess;
        this.NewMessagesOrigin = messageBatch.MessagesToProcessOrigin;
        this.OrchestrationRuntimeState.NewEvents.Clear();
    }
}
