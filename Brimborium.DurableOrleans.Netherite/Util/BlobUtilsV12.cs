﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;
using static Orleans.DurableTask.Netherite.TransportAbstraction;

static class BlobUtilsV12
{
    public class ServerTimeoutPolicy : HttpPipelineSynchronousPolicy
    {
        readonly int timeout;

        public ServerTimeoutPolicy(int timeout)
        {
            this.timeout = timeout;
        }

        public override void OnSendingRequest(HttpMessage message)
        {
            message.Request.Uri.AppendQuery("timeout", this.timeout.ToString());
        }
    }
    public struct ServiceClients
    {
        public BlobServiceClient Default;
        public BlobServiceClient Aggressive;
        public BlobServiceClient WithRetries;
    }

    internal static ServiceClients GetServiceClients(ConnectionInfo info)
    {
        var aggressiveOptions = new BlobClientOptions();
        aggressiveOptions.Retry.MaxRetries = 0;
        aggressiveOptions.Retry.NetworkTimeout = TimeSpan.FromSeconds(3);
        aggressiveOptions.AddPolicy(new ServerTimeoutPolicy(2), HttpPipelinePosition.PerCall);

        var defaultOptions = new BlobClientOptions();
        defaultOptions.Retry.MaxRetries = 0;
        defaultOptions.Retry.NetworkTimeout = TimeSpan.FromSeconds(16);
        defaultOptions.AddPolicy(new ServerTimeoutPolicy(15), HttpPipelinePosition.PerCall);

        var withRetriesOptions = new BlobClientOptions();
        withRetriesOptions.Retry.MaxRetries = 10;
        withRetriesOptions.Retry.Mode = RetryMode.Exponential;
        withRetriesOptions.Retry.Delay = TimeSpan.FromSeconds(1);
        withRetriesOptions.Retry.MaxDelay = TimeSpan.FromSeconds(30);
       
        return new ServiceClients()
        {
            Default = info.GetAzureStorageV12BlobServiceClient(defaultOptions),
            Aggressive = info.GetAzureStorageV12BlobServiceClient(aggressiveOptions),
            WithRetries = info.GetAzureStorageV12BlobServiceClient(withRetriesOptions),
        };
    }

    public struct ContainerClients
    {
        public BlobContainerClient Default;
        public BlobContainerClient Aggressive;
        public BlobContainerClient WithRetries;
    }

    internal static ContainerClients GetContainerClients(ServiceClients serviceClients, string blobContainerName)
    {
        return new ContainerClients()
        {
            Default = serviceClients.Default.GetBlobContainerClient(blobContainerName),
            Aggressive = serviceClients.Aggressive.GetBlobContainerClient(blobContainerName),
            WithRetries = serviceClients.WithRetries.GetBlobContainerClient(blobContainerName),
        };

    }

    public struct BlockBlobClients
    {
        public BlockBlobClient Default;
        public BlockBlobClient Aggressive;
        public BlockBlobClient WithRetries;

        public string Name => this.Default?.Name;
    }

    internal static BlockBlobClients GetBlockBlobClients(ContainerClients containerClients, string blobName)
    {
        return new BlockBlobClients()
        {
            Default = containerClients.Default.GetBlockBlobClient(blobName),
            Aggressive = containerClients.Aggressive.GetBlockBlobClient(blobName),
            WithRetries = containerClients.WithRetries.GetBlockBlobClient(blobName),
        };

    }

    public struct PageBlobClients
    {
        public PageBlobClient Default;
        public PageBlobClient Aggressive;
    }

    internal static PageBlobClients GetPageBlobClients(ContainerClients containerClients, string blobName)
    {
        return new PageBlobClients()
        {
            Default = containerClients.Default.GetPageBlobClient(blobName),
            Aggressive = containerClients.Aggressive.GetPageBlobClient(blobName),
        };

    }

    public struct BlobDirectory
    {
        readonly ContainerClients client;
        readonly string prefix;

        public ContainerClients Client => this.client;
        public string Prefix => this.prefix;

        public BlobDirectory(ContainerClients client, string prefix)
        {
            this.client = client;
            this.prefix = string.Concat(prefix);
        }

        public BlobDirectory GetSubDirectory(string path)
        {
            return new BlobDirectory(this.client, $"{this.prefix}/{path}");
        }

        public BlobUtilsV12.BlockBlobClients GetBlockBlobClient(string name)
        {
            return BlobUtilsV12.GetBlockBlobClients(this.client, $"{this.prefix}/{name}");
        }

        public BlobUtilsV12.PageBlobClients GetPageBlobClient(string name)
        {
            return BlobUtilsV12.GetPageBlobClients(this.client, $"{this.prefix}/{name}");
        }

        public async Task<List<string>> GetBlobsAsync(CancellationToken cancellationToken)
        {
            var list = new List<string>();
            await foreach (var blob in this.client.WithRetries.GetBlobsAsync(prefix: this.prefix, cancellationToken: cancellationToken))
            {
                list.Add(blob.Name);
            }
            return list;
        }

        public override string ToString()
        {
            return $"{this.prefix}/";
        }
    }

    /// <summary>
    /// Forcefully deletes a blob.
    /// </summary>
    /// <param name="blob">The CloudBlob to delete.</param>
    /// <returns>A task that completes when the operation is finished.</returns>
    public static async Task<bool> ForceDeleteAsync(BlobContainerClient containerClient, string blobName)
    {
        var blob = containerClient.GetBlobClient(blobName);

        try
        {
            await blob.DeleteAsync();
            return true;
        }
        catch (Azure.RequestFailedException e) when (BlobDoesNotExist(e))
        {
            return false;
        }
        catch (Azure.RequestFailedException e) when (CannotDeleteBlobWithLease(e))
        {
            try
            {
                var leaseClient = new BlobLeaseClient(blob);
                await leaseClient.BreakAsync(TimeSpan.Zero);
            }
            catch
            {
                // we ignore exceptions in the lease breaking since there could be races
            }

            // retry the delete
            try
            {
                await blob.DeleteAsync();
                return true;
            }
            catch (Azure.RequestFailedException ex) when (BlobDoesNotExist(ex))
            {
                return false;
            }
        }
    }

    // Lease error codes are documented at https://docs.microsoft.com/en-us/rest/api/storageservices/lease-blob

    public static bool LeaseConflictOrExpired(Azure.RequestFailedException e)
    {
        return e.Status == 409 || e.Status == 412;
    }

    public static bool LeaseConflict(Azure.RequestFailedException e)
    {
        return e.Status == 409;
    }

    public static bool LeaseExpired(Azure.RequestFailedException e)
    {
        return e.Status == 412;
    }

    public static bool CannotDeleteBlobWithLease(Azure.RequestFailedException e)
    {
        return e.Status == 412;
    }

    public static bool PreconditionFailed(Azure.RequestFailedException e)
    {
        return e.Status == 409 || e.Status == 412;
    }

    public static bool BlobDoesNotExist(Azure.RequestFailedException e)
    {
        return e.Status == 404 && e.ErrorCode == BlobErrorCode.BlobNotFound;
    }

    public static bool BlobAlreadyExists(Azure.RequestFailedException e)
    {
        return e.Status == 409;
    }
}
