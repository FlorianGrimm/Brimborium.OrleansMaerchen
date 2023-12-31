//  ------------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation
//  All rights reserved. 
//  
//  Licensed under the Apache License, Version 2.0 (the ""License""); you may not use this 
//  file except in compliance with the License. You may obtain a copy of the License at 
//  http://www.apache.org/licenses/LICENSE-2.0  
//  
//  THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, 
//  EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY IMPLIED WARRANTIES OR 
//  CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABLITY OR 
//  NON-INFRINGEMENT. 
// 
//  See the Apache Version 2.0 License for specific language governing permissions and 
//  limitations under the License.
//  ------------------------------------------------------------------------------------

namespace Brimborium.OrleansAmqp.Transactions;

using Brimborium.OrleansAmqp.Types;

/// <summary>
/// Symbols indicating (desired/available) capabilities of a transaction coordinator.
/// </summary>
public static class TxnCapabilities
{
    /// <summary>
    /// Support local transactions.
    /// </summary>
    public static readonly Symbol LocalTransactions = "amqp:local-transactions";
    
    
    /// <summary>
    /// Support AMQP Distributed Transactions.
    /// </summary>
    public static readonly Symbol DistributedTxn = "amqp:distributed-transactions";
    
    
    /// <summary>
    /// Support AMQP Promotable Transactions.
    /// </summary>
    public static readonly Symbol PrototableTransactions = "amqp:prototable-transactions";
    
    
    /// <summary>
    /// Support multiple active transactions on a single session.
    /// </summary>
    public static readonly Symbol MultiTxnsPerSsn = "amqp:multi-txns-per-ssn";
    
    
    /// <summary>
    /// Support transactions whose txn-id is used across sessions on one connection.
    /// </summary>
    public static readonly Symbol MultiSsnsPerTxn = "amqp:multi-ssns-per-txn";
}