// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

class MemoryStorageLayer : IStorageLayer {
    readonly NetheriteOrchestrationServiceSettings settings;
    readonly ILogger logger;

    TaskhubParameters taskhub;

    public MemoryStorageLayer(NetheriteOrchestrationServiceSettings settings, ILogger logger) {
        this.settings = settings;
        this.logger = logger;
    }

    void Reset() {
        this.taskhub = null;
    }

    public CancellationToken Termination => CancellationToken.None;

    ILoadPublisherService IStorageLayer.LoadPublisher => null; // we do not publish load for in-memory storage emulation

    async Task<bool> IStorageLayer.CreateTaskhubIfNotExistsAsync() {
        await Task.Yield();
        if (this.taskhub == null) {
            this.taskhub = new TaskhubParameters() {
                TaskhubName = this.settings.HubName,
                TaskhubGuid = Guid.NewGuid(),
                CreationTimestamp = DateTime.UtcNow,
                StorageFormat = String.Empty,
                PartitionCount = this.settings.PartitionCount,
            };
            return true;
        } else {
            return false;
        }
    }

    async Task IStorageLayer.DeleteTaskhubAsync() {
        await Task.Yield();
        this.taskhub = null;
    }

    IPartitionState IStorageLayer.CreatePartitionState(TaskhubParameters parameters) {
        return new MemoryStorage(this.logger);
    }

    (string containerName, string path) IStorageLayer.GetTaskhubPathPrefix(TaskhubParameters parameters) {
        return (string.Empty, string.Empty);
    }

    async Task<TaskhubParameters> IStorageLayer.TryLoadTaskhubAsync(bool throwIfNotFound) {
        await Task.Yield();
        return this.taskhub;
    }
}