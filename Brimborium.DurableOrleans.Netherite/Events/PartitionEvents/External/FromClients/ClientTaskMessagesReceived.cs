// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

[DataContract]
class ClientTaskMessagesReceived : ClientRequestEvent
{
    [DataMember]
    public TaskMessage[] TaskMessages { get; set; }

    [IgnoreDataMember]
    public override string TracedInstanceId => this.TaskMessages[0].OrchestrationInstance.InstanceId;

    public override void DetermineEffects(EffectTracker effects)
    {
        effects.Add(TrackedObjectKey.Sessions);
    }

    public override void ApplyTo(TrackedObject trackedObject, EffectTracker effects)
    {
        trackedObject.Process(this, effects);
    }
}