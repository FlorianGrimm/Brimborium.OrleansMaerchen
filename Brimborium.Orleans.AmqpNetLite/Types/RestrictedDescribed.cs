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

namespace Brimborium.OrleansAmqp.Types;

/// <summary>
/// The RestrictedDescribed class is an AMQP described value, whose descriptor is
/// restricted to symbol or ulong.
/// </summary>
public abstract class RestrictedDescribed : Described
{
    private readonly Descriptor descriptor;

    /// <summary>
    /// Initializes a described value.
    /// </summary>
    /// <param name="descriptor">The descriptor of the value.</param>
    protected RestrictedDescribed(Descriptor descriptor)
    {
        this.descriptor = descriptor;
    }

    /// <summary>
    /// Gets the descriptor.
    /// </summary>
    public Descriptor Descriptor
    {
        get { return this.descriptor; }
    }

    internal override void EncodeDescriptor(ByteBuffer buffer)
    {
        AmqpEncoder.WriteULong(buffer, this.descriptor.Code, true);
    }

    internal override void DecodeDescriptor(ByteBuffer buffer)
    {
        var formatCode = AmqpEncoder.ReadFormatCode(buffer);
        if (formatCode == FormatCode.Described)
        {
            formatCode = AmqpEncoder.ReadFormatCode(buffer);
        }

        if (formatCode == FormatCode.Symbol8 ||
            formatCode == FormatCode.Symbol32)
        {
            AmqpEncoder.ReadSymbol(buffer, formatCode);
        }
        else if (formatCode == FormatCode.ULong ||
                 formatCode == FormatCode.ULong0 ||
                 formatCode == FormatCode.SmallULong)
        {
            AmqpEncoder.ReadULong(buffer, formatCode);
        }
        else
        {
            throw AmqpEncoder.InvalidFormatCodeException(formatCode, buffer.Offset);
        }
    }
}
