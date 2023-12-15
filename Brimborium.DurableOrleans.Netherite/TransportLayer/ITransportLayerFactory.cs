// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

public interface ITransportLayerFactory {
    ITransportLayer Create(NetheriteOrchestrationService orchestrationService);
}
