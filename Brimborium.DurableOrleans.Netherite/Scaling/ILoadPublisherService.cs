﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite.Scaling;

/// <summary>
/// An interface for the load publish service.
/// </summary>
public interface ILoadPublisherService {
    /// <summary>
    /// Publish the load of a partition to the service.
    /// </summary>
    /// <param name="loadInfo">A collection of load information for partitions</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A task indicating completion</returns>
    Task PublishAsync(Dictionary<uint, PartitionLoadInfo> loadInfo, CancellationToken cancellationToken);

    /// <summary>
    /// Delete all load information for a taskhub.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A task indicating completion</returns>
    Task DeleteIfExistsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Prepare the service for a taskhub.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A task indicating completion</returns>
    Task CreateIfNotExistsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Query all load information for a taskhub.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A task returning a dictionary with load information for the partitions</returns>
    Task<Dictionary<uint, PartitionLoadInfo>> QueryAsync(CancellationToken cancellationToken);
}
