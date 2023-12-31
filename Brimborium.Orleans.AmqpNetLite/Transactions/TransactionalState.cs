﻿//  ------------------------------------------------------------------------------------
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

using Brimborium.OrleansAmqp.Framing;
using Brimborium.OrleansAmqp.Types;

/// <summary>
/// The state of a transactional message transfer.
/// </summary>
public sealed class TransactionalState : DeliveryState
{
    private byte[] txnId;
    private Outcome outcome;

    /// <summary>
    /// Initializes a transactional state object.
    /// </summary>
    public TransactionalState()
        : base(Codec.TransactionalState, 2)
    {
    }

    /// <summary>
    /// Gets or sets the txn-id field.
    /// </summary>
    public byte[] TxnId
    {
        get { return this.GetField(0, this.txnId); }
        set { this.SetField(0, ref this.txnId, value); }
    }

    /// <summary>
    /// Gets or sets the outcome field.
    /// </summary>
    public Outcome Outcome
    {
        get { return this.GetField(1, this.outcome); }
        set { this.SetField(1, ref this.outcome, value); }
    }

    internal override void WriteField(ByteBuffer buffer, int index)
    {
        switch (index)
        {
            case 0:
                AmqpEncoder.WriteBinary(buffer, this.txnId, true);
                break;
            case 1:
                AmqpEncoder.WriteObject(buffer, this.outcome);
                break;
            default:
                AssertException.Assert(false, "Invalid field index");
                break;
        }
    }

    internal override void ReadField(ByteBuffer buffer, int index, byte formatCode)
    {
        switch (index)
        {
            case 0:
                this.txnId = AmqpEncoder.ReadBinary(buffer, formatCode);
                break;
            case 1:
                this.outcome = (Outcome)AmqpEncoder.ReadObject(buffer, formatCode);
                break;
            default:
                AssertException.Assert(false, "Invalid field index");
                break;
        }
    }

#if TRACE
    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return this.GetDebugString(
            "txn-state",
            new object[] { "txn-id", "outcome" },
            new object[] { txnId, outcome });
    }
#endif
}