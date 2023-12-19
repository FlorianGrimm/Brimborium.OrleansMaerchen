﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite.Scaling;

class AzureBlobLoadPublisher : ILoadPublisherService {
    readonly string taskHubName;
    readonly Task<CloudBlobContainer> blobContainer;
    TaskhubParameters parameters;

    readonly static JsonSerializerSettings serializerSettings = new JsonSerializerSettings() {
        TypeNameHandling = TypeNameHandling.None,
        MissingMemberHandling = MissingMemberHandling.Ignore,
    };

    public AzureBlobLoadPublisher(ConnectionInfo connectionInfo, string taskHubName) {
        this.blobContainer = this.GetBlobContainer(connectionInfo, taskHubName);
        this.taskHubName = taskHubName;
    }

    async Task<CloudBlobContainer> GetBlobContainer(ConnectionInfo connectionInfo, string taskHubName) {
        var cloudStorageAccount = await connectionInfo.GetAzureStorageV11AccountAsync();
        CloudBlobClient serviceClient = cloudStorageAccount.CreateCloudBlobClient();
        string containerName = BlobManager.GetContainerName(taskHubName);
        return serviceClient.GetContainerReference(containerName);
    }

    public TimeSpan PublishInterval => TimeSpan.FromSeconds(10);

    public Task CreateIfNotExistsAsync(CancellationToken cancellationToken) {
        // not needed since the blobs are stored in the taskhub's container
        return Task.CompletedTask;
    }

    async ValueTask<bool> LoadParameters(bool throwIfNotFound, CancellationToken cancellationToken) {
        if (this.parameters == null) {
            this.parameters = await this.ReadJsonBlobAsync<Netherite.Abstractions.TaskhubParameters>(
                (await this.blobContainer).GetBlockBlobReference("taskhubparameters.json"),
                throwIfNotFound: throwIfNotFound,
                throwOnParseError: throwIfNotFound,
                cancellationToken).ConfigureAwait(false);
        }
        return this.parameters != null;
    }

    public async Task<T> ReadJsonBlobAsync<T>(CloudBlockBlob blob, bool throwIfNotFound, bool throwOnParseError, CancellationToken token) where T : class {
        try {
            var jsonText = await blob.DownloadTextAsync(token).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<T>(jsonText);
        } catch (StorageException e) when (!throwIfNotFound && e.RequestInformation?.HttpStatusCode == 404) {
            // container or blob does not exist
        } catch (JsonException) when (!throwOnParseError) {
            // cannot parse content of blob
        } catch (StorageException e) when (e.InnerException is OperationCanceledException operationCanceledException) {
            throw new OperationCanceledException("Blob read was canceled.", operationCanceledException);
        }

        return null;
    }

    public async Task PublishAsync(Dictionary<uint, PartitionLoadInfo> info, CancellationToken cancellationToken) {
        await this.LoadParameters(throwIfNotFound: true, cancellationToken).ConfigureAwait(false);

        async Task UploadPartitionInfo(uint partitionId, PartitionLoadInfo loadInfo) {
            var blobDirectory = (await this.blobContainer).GetDirectoryReference($"{this.parameters.TaskhubGuid}/p{partitionId:D2}");
            var blob = blobDirectory.GetBlockBlobReference("loadinfo.json");
            var json = JsonConvert.SerializeObject(loadInfo, Formatting.Indented, serializerSettings);
            await blob.UploadTextAsync(json, cancellationToken);
        }

        List<Task> tasks = info.Select(kvp => UploadPartitionInfo(kvp.Key, kvp.Value)).ToList();
        await Task.WhenAll(tasks);
    }

    public async Task<Dictionary<uint, PartitionLoadInfo>> QueryAsync(CancellationToken cancellationToken) {
        await this.LoadParameters(throwIfNotFound: true, cancellationToken).ConfigureAwait(false);

        async Task<(uint, PartitionLoadInfo)> DownloadPartitionInfo(uint partitionId) {
            PartitionLoadInfo info = await this.ReadJsonBlobAsync<PartitionLoadInfo>(
                (await this.blobContainer).GetDirectoryReference($"{this.parameters.TaskhubGuid}/p{partitionId:D2}").GetBlockBlobReference("loadinfo.json"),
                throwIfNotFound: false,
                throwOnParseError: true,
                cancellationToken).ConfigureAwait(false);
            return (partitionId, info);
        }

        var tasks = Enumerable.Range(0, this.parameters.PartitionCount).Select(partitionId => DownloadPartitionInfo((uint)partitionId)).ToList();
        await Task.WhenAll(tasks).ConfigureAwait(false);
        return tasks.Select(task => task.Result).Where(pair => pair.Item2 != null).ToDictionary(pair => pair.Item1, pair => pair.Item2);
    }

    public async Task DeleteIfExistsAsync(CancellationToken cancellationToken) {
        if (await this.LoadParameters(throwIfNotFound: false, cancellationToken).ConfigureAwait(false)) {
            async Task DeletePartitionInfo(uint partitionId) {
                var blob = (await this.blobContainer).GetDirectoryReference($"{this.parameters.TaskhubGuid}/p{partitionId:D2}").GetBlockBlobReference("loadinfo.json");
                await BlobUtils.ForceDeleteAsync(blob).ConfigureAwait(false);
            }

            var tasks = Enumerable.Range(0, this.parameters.PartitionCount).Select(partitionId => DeletePartitionInfo((uint)partitionId)).ToList();
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }
}
