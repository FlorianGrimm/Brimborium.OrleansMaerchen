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

internal sealed class Received : DeliveryState
{
    private uint sectionNumber;
    private ulong sectionOffset;

    public Received()
        : base(Codec.Received, 2)
    {
    }

    public uint SectionNumber
    {
        get { return this.GetField(0, this.sectionNumber, uint.MinValue); }
        set { this.SetField(0, ref this.sectionNumber, value); }
    }

    public ulong SectionOffset
    {
        get { return this.GetField(1, this.sectionOffset, ulong.MinValue); }
        set { this.SetField(1, ref this.sectionOffset, value); }
    }

    internal override void WriteField(ByteBuffer buffer, int index)
    {
        switch (index)
        {
            case 0:
                AmqpEncoder.WriteUInt(buffer, this.sectionNumber, true);
                break;
            case 1:
                AmqpEncoder.WriteULong(buffer, this.sectionOffset, true);
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
                this.sectionNumber = AmqpEncoder.ReadUInt(buffer, formatCode);
                break;
            case 1:
                this.sectionOffset = AmqpEncoder.ReadULong(buffer, formatCode);
                break;
            default:
                Fx.Assert(false, "Invalid field index");
                break;
        }
    }

    public override string ToString()
    {
#if TRACE
        return this.GetDebugString(
            "received",
            new object[] { "section-number", "section-offset" },
            new object[] { sectionNumber, sectionOffset });
#else
        return base.ToString();
#endif
    }
}