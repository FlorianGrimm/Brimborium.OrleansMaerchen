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

/// <summary>
/// An AMQP Value section contains a single value.
/// </summary>
public class AmqpValue : RestrictedDescribed {
    private object _Value;
    private ByteBuffer _ValueBuffer;
    private bool _ValueDecoded;

    /// <summary>
    /// Initializes an AmqpValue object.
    /// </summary>
    public AmqpValue()
        : base(Codec.AmqpValue) {
    }

    /// <summary>
    /// Gets or sets the value in the body section.
    /// </summary>
    public object Value {
        get {
            if (!this._ValueDecoded) {
                this._Value = AmqpEncoder.ReadObject(this._ValueBuffer);
                this._ValueDecoded = true;
            }

            return this._Value;
        }
        set {
            this._Value = value;
            this._ValueDecoded = true;
        }
    }

    /// <summary>
    /// Gets a ByteBuffer that contains the encoded value. A NULL value
    /// indicates that the message is not created by the decoder.
    /// </summary>
    public ByteBuffer ValueBuffer {
        get { return this._ValueBuffer; }
    }

    /// <summary>
    /// Writes the value into the buffer using the default encoder. Override
    /// this method to encode the value using an extended or custom encoder.
    /// </summary>
    /// <param name="buffer">The buffer to write the encoded object.</param>
    /// <param name="value">The object to be written.</param>
    protected virtual void WriteValue(ByteBuffer buffer, object value) {
        AmqpEncoder.WriteObject(buffer, value);
    }

    internal override void EncodeValue(ByteBuffer buffer) {
        if (this._Value is ByteBuffer byteBuffer) {
            AmqpEncoder.WriteBinaryBuffer(buffer, byteBuffer);
        } else if (this._ValueBuffer != null && !this._ValueDecoded) {
            AmqpBitConverter.WriteBytes(buffer, this._ValueBuffer.Buffer, this._ValueBuffer.Offset, this._ValueBuffer.Length);
        } else {
            this.WriteValue(buffer, this._Value);
        }
    }

    internal override void DecodeValue(ByteBuffer buffer) {
        // initialize the value buffer only
        int offset = buffer.Offset;
        byte formatCode = AmqpEncoder.ReadFormatCode(buffer);
        while (formatCode == FormatCode.Described) {
            AmqpEncoder.ReadObject(buffer);
            formatCode = AmqpEncoder.ReadFormatCode(buffer);
        }

        int count = this.GetCount(formatCode, buffer);
        buffer.Validate(false, count);
        buffer.Complete(count);

        int size = buffer.Offset - offset;
        this._ValueBuffer = new ByteBuffer(buffer.Buffer, offset, size, size);
    }

    private int GetCount(byte formatCode, ByteBuffer buffer) {
        int count;
        if (formatCode >= 0xA0) {
            //bin8  a0 10100001
            //str8  a1 10100001
            //sym8  a3 10100011
            //list8 c0 11000000
            //map8  c1 11000001
            //arr8  e0 11100000
            //bin32 b0 10110000
            //str32 b1 10110001
            //sym32 b3 10110011
            //lit32 d0 11010000
            //map32 d1 11010001
            //arr32 f0 11110000
            if ((formatCode & 0x10) == 0) {
                count = AmqpBitConverter.ReadUByte(buffer);
            } else {
                count = AmqpBitConverter.ReadInt(buffer);
            }
        } else {
            count = (1 << ((formatCode >> 4) - 4)) >> 1;
        }

        return count;
    }
}