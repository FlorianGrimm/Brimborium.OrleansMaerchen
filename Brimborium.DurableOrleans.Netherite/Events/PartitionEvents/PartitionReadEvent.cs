﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

[DataContract]
abstract class PartitionReadEvent : PartitionEvent
{
    /// <summary>
    /// The target of the read operation.
    /// </summary>
    [IgnoreDataMember]
    public abstract TrackedObjectKey ReadTarget { get; }

    /// <summary>
    /// A secondary target, to be prefetched also, before executing the read
    /// </summary>
    [IgnoreDataMember]
    public virtual TrackedObjectKey? Prefetch => null;

    /// <summary>
    /// The continuation for the read operation.
    /// </summary>
    /// <param name="target">The current value of the tracked object for this key, or null if not present</param>
    /// <param name="partition">The partition</param>
    public abstract void OnReadComplete(TrackedObject target, Partition partition);

    protected override void ExtraTraceInformation(StringBuilder s)
    {
        int completed, total;
        if (this.Prefetch.HasValue)
        {
            completed = (this.prefetchLoaded ? 1 : 0) + (this.targetLoaded ? 1 : 0);
            total = 2;
        }
        else
        {
            completed = this.targetLoaded ? 1 : 0;
            total = 1;
        }
        s.Append(' ');
        s.Append(completed);
        s.Append('/');
        s.Append(total);
    }

    #region prefetch state machine

    [IgnoreDataMember]
    bool prefetchLoaded;

    [IgnoreDataMember]
    bool targetLoaded;

    [IgnoreDataMember]
    TrackedObject target;

    public void Deliver(TrackedObjectKey key, TrackedObject trackedObject, out bool isReady)
    {
        if (!this.Prefetch.HasValue)
        {
            this.prefetchLoaded = true;
            this.targetLoaded = true;
            this.target = trackedObject;
        }
        else if (key.Equals(this.Prefetch.Value))
        {
            this.prefetchLoaded = true;
        }
        else
        {
            this.targetLoaded = true;
            this.target = trackedObject;
        }

        isReady = (this.prefetchLoaded && this.targetLoaded);
    }

    public void Fire(Partition partition)
    {
        this.OnReadComplete(this.target, partition);
    }
  
    #endregion
}
