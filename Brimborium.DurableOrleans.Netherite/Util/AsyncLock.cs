﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Orleans.DurableTask.Netherite;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides an async lock (does not block waiting threads)
/// </summary>
class AsyncLock : SemaphoreSlim, IDisposable
{
    readonly AcquisitionToken token; 

    public AsyncLock() : base(1, 1)
    {
        this.token = new AcquisitionToken()
        {
            AsyncLock = this
        };
    }

    public async ValueTask<AcquisitionToken> LockAsync()
    {
        await base.WaitAsync();
        return this.token;
    }

    internal struct AcquisitionToken : IDisposable
    {
        public AsyncLock AsyncLock;

        public void Dispose()
        {
            this.AsyncLock.Release();
        }
    }        
}
