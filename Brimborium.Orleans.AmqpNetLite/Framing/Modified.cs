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

namespace Brimborium.OrleansAmqp.Framing;

using Brimborium.OrleansAmqp.Types;

/// <summary>
/// The Modified class defines the modified outcome.
/// </summary>
public sealed class Modified : Outcome
{
    private bool deliveryFailed;
    private bool undeliverableHere;
    private Fields messageAnnotations;

    /// <summary>
    /// Initializes a modified outcome.
    /// </summary>
    public Modified()
        : base(Codec.Modified, 3)
    {
    }

    /// <summary>
    /// Gets or sets the delivery-failed field.
    /// </summary>
    public bool DeliveryFailed
    {
        get { return this.GetField(0, this.deliveryFailed, false); }
        set { this.SetField(0, ref this.deliveryFailed, value); }
    }

    /// <summary>
    /// Gets or sets the undeliverable-here field.
    /// </summary>
    public bool UndeliverableHere
    {
        get { return this.GetField(1, this.undeliverableHere, false); }
        set { this.SetField(1, ref this.undeliverableHere, value); }
    }

    /// <summary>
    /// Gets or sets the message-annotations field.
    /// </summary>
    public Fields MessageAnnotations
    {
        get { return this.GetField(2, this.messageAnnotations); }
        set { this.SetField(2, ref this.messageAnnotations, value); }
    }

    internal override void WriteField(ByteBuffer buffer, int index)
    {
        switch (index)
        {
            case 0:
                AmqpEncoder.WriteBoolean(buffer, this.deliveryFailed, true);
                break;
            case 1:
                AmqpEncoder.WriteBoolean(buffer, this.undeliverableHere, true);
                break;
            case 2:
                AmqpEncoder.WriteMap(buffer, this.messageAnnotations, true);
                break;
            default:
                Fx.Assert(false, "Invalid field index");
                break;
        }
    }

    internal override void ReadField(ByteBuffer buffer, int index, byte formatCode)
    {
        switch (index)
        {
            case 0:
                this.deliveryFailed = AmqpEncoder.ReadBoolean(buffer, formatCode);
                break;
            case 1:
                this.undeliverableHere = AmqpEncoder.ReadBoolean(buffer, formatCode);
                break;
            case 2:
                this.messageAnnotations = AmqpEncoder.ReadFields(buffer, formatCode);
                break;
            default:
                Fx.Assert(false, "Invalid field index");
                break;
        }
    }

#if TRACE
    /// <summary>
    /// Returns a string that represents the current modified object.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return this.GetDebugString(
            "modified",
            new object[] { "delivery-failed", "undeliverable-here", "message-annotations" },
            new object[] { deliveryFailed, undeliverableHere, messageAnnotations });
    }
#endif
}