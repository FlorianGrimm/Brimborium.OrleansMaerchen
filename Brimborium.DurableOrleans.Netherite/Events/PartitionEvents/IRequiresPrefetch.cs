// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

interface IRequiresPrefetch
{
    public IEnumerable<TrackedObjectKey> KeysToPrefetch { get; }
}
