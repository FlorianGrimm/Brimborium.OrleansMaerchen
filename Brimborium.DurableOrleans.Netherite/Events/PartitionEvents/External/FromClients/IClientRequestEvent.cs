// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

interface IClientRequestEvent
{
    Guid ClientId { get; set; }

    long RequestId { get; set; }

    DateTime TimeoutUtc { get; set; }

    EventId EventId { get; }
}