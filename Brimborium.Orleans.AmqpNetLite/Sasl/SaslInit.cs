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

namespace Brimborium.OrleansAmqp.Sasl;

using Brimborium.OrleansAmqp.Framing;
using Brimborium.OrleansAmqp.Types;

/// <summary>
/// SaslInit selects the mechanism and provides the initial response if needed.
/// </summary>
public class SaslInit : DescribedList
{
    private Symbol mechanism;
    private byte[] initialResponse;
    private string hostName;

    /// <summary>
    /// Initializes a SaslInit object.
    /// </summary>
    public SaslInit()
        : base(Codec.SaslInit, 3)
    {
    }

    /// <summary>
    /// Gets or sets the selected security mechanism (index=0).
    /// </summary>
    public Symbol Mechanism
    {
        get { return this.GetField(0, this.mechanism); }
        set { this.SetField(0, ref this.mechanism, value); }
    }

    /// <summary>
    /// Gets or sets the initial security response data (index=1).
    /// </summary>
    public byte[] InitialResponse
    {
        get { return this.GetField(1, this.initialResponse); }
        set { this.SetField(1, ref this.initialResponse, value); }
    }

    /// <summary>
    /// Gets or sets the name of the target host (index=2).
    /// </summary>
    public string HostName
    {
        get { return this.GetField(2, this.hostName); }
        set { this.SetField(2, ref this.hostName, value); }
    }

    internal override void WriteField(ByteBuffer buffer, int index)
    {
        switch (index)
        {
            case 0:
                AmqpEncoder.WriteSymbol(buffer, this.mechanism, true);
                break;
            case 1:
                AmqpEncoder.WriteBinary(buffer, this.initialResponse, true);
                break;
            case 2:
                AmqpEncoder.WriteString(buffer, this.hostName, true);
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
                this.mechanism = AmqpEncoder.ReadSymbol(buffer, formatCode);
                break;
            case 1:
                this.initialResponse = AmqpEncoder.ReadBinary(buffer, formatCode);
                break;
            case 2:
                this.hostName = AmqpEncoder.ReadString(buffer, formatCode);
                break;
            default:
                AssertException.Assert(false, "Invalid field index");
                break;
        }
    }

#if TRACE
    /// <summary>
    /// Returns a string that represents the current SASL init object.
    /// </summary>
    public override string ToString()
    {
        return this.GetDebugString(
            "sasl-init",
            new object[] { "mechanism", "initial-response", "hostname" },
            new object[] { mechanism, "...", hostName });
    }
#endif
}