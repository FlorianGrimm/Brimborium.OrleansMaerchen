// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

[DataContract]
class SendConfirmed : PartitionUpdateEvent
{
    [DataMember]
    public long Position { get; set; }

    [DataMember]
    public string SendingEventId { get; set; }

    [IgnoreDataMember]
    public override EventId EventId => EventId.MakePartitionInternalEventId($"{this.SendingEventId}C");

    public override void DetermineEffects(EffectTracker effects)
    {
        effects.Add(TrackedObjectKey.Outbox);
    }

    public override void ApplyTo(TrackedObject trackedObject, EffectTracker effects)
    {
        trackedObject.Process(this, effects);
    }
}
