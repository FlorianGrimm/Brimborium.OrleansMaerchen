﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

/// <summary>
/// The result returned by an instance query
/// </summary>
[DataContract]
public class InstanceQueryResult {
    /// <summary>
    /// The instances returned by the query.
    /// </summary>
    public IEnumerable<OrchestrationState> Instances { get; set; }

    /// <summary>
    /// A continuation token to resume the query, or null if the results are complete.
    /// </summary>
    public string ContinuationToken { get; set; }
}
