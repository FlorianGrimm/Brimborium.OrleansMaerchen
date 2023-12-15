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
/// Security mechanism response.
/// </summary>
public class SaslResponse : DescribedList
{
    private byte[] response;

    /// <summary>
    /// Initializes a SaslResponse object.
    /// </summary>
    public SaslResponse()
        : base(Codec.SaslResponse, 1)
    {
    }

    /// <summary>
    /// Gets or sets the security response data (index=0).
    /// </summary>
    public byte[] Response
    {
        get { return this.GetField(0, this.response); }
        set { this.SetField(0, ref this.response, value); }
    }

    internal override void WriteField(ByteBuffer buffer, int index)
    {
        switch (index)
        {
            case 0:
                AmqpEncoder.WriteBinary(buffer, this.response, true);
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
                this.response = AmqpEncoder.ReadBinary(buffer, formatCode);
                break;
            default:
                AssertException.Assert(false, "Invalid field index");
                break;
        }
    }
    
#if TRACE
    /// <summary>
    /// Returns a string that represents the current SASL response object.
    /// </summary>
    public override string ToString()
    {
        return this.GetDebugString(
            "sasl-response",
            new object[] { "response" },
            new object[] { response });
    }
#endif
}