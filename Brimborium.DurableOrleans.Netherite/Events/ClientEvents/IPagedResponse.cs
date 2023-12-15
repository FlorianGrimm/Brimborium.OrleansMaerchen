// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

interface IPagedResponse
{
    string ContinuationToken { get; }

    int Count { get; }
}