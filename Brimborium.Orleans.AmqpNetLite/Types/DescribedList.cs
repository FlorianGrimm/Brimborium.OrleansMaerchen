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

namespace Brimborium.OrleansAmqp.Types;

/// <summary>
/// The DescribedList class consist of a descriptor and an AMQP list.
/// </summary>
public abstract class DescribedList : RestrictedDescribed {
    private readonly int fieldCount;
    private int fields; // bitmask that stores an 'is null' flag for each field

    /// <summary>
    /// Initializes the described list object.
    /// </summary>
    /// <param name="descriptor">The descriptor of the concrete described list class.</param>
    /// <param name="fieldCount">The number of fields of the concrete described list class.</param>
    protected DescribedList(Descriptor descriptor, int fieldCount)
        : base(descriptor) {
        if (fieldCount > 32) {
            throw new NotSupportedException("Maximum number of fields allowed is 32.");
        }

        this.fieldCount = fieldCount;
    }

    /// <summary>
    /// Examines the list to check if a field is set.
    /// </summary>
    /// <param name="index">Zero-based offset of the field in the list.</param>
    /// <returns>True if a value is set; otherwise false.</returns>
    /// <remarks>The field index can be found in the description of each field.</remarks>
    public bool HasField(int index) {
        this.CheckFieldIndex(index);
        return (this.fields & (1 << index)) > 0;
    }

    /// <summary>
    /// Resets the value of a field to null.
    /// </summary>
    /// <param name="index">Zero-based offset of the field in the list.</param>
    /// <remarks>The field index can be found in the description of each field.</remarks>
    public void ResetField(int index) {
        this.CheckFieldIndex(index);
        this.fields &= ~(1 << index);
    }

    internal void SetField<T>(int index, ref T field, T value) {
        this.fields |= (1 << index);
        field = value;
    }

    internal T GetField<T>(int index, T field, T defaultValue = default(T)) {
        if ((this.fields & (1 << index)) == 0) {
            return defaultValue;
        }

        return field;
    }

    internal override void DecodeValue(ByteBuffer buffer) {
        byte formatCode = AmqpEncoder.ReadFormatCode(buffer);
        int size;
        int count;
        AmqpEncoder.ReadListCount(buffer, formatCode, out size, out count);
        for (int i = 0; i < count; i++) {
            formatCode = AmqpEncoder.ReadFormatCode(buffer);
            if (formatCode != FormatCode.Null) {
                this.ReadField(buffer, i, formatCode);
                this.fields |= (1 << i);
            }
        }
    }

    internal abstract void WriteField(ByteBuffer buffer, int index);

    internal abstract void ReadField(ByteBuffer buffer, int index, byte formatCode);


    internal override void EncodeValue(ByteBuffer buffer) {
        if (this.fields == 0) {
            AmqpBitConverter.WriteUByte(buffer, FormatCode.List0);
        } else {
            // Count non-null fields by removing leading zeros
            int count = 0;
            int temp = this.fields;
            while (temp > 0) {
                count++;
                temp <<= 1;
            }

            count = 32 - count;
            int pos = buffer.WritePos;
            AmqpBitConverter.WriteUByte(buffer, 0);
            AmqpBitConverter.WriteULong(buffer, 0);
            for (int i = 0; i < count; i++) {
                if ((this.fields & (1 << i)) > 0) {
                    this.WriteField(buffer, i);
                } else {
                    AmqpBitConverter.WriteUByte(buffer, FormatCode.Null);
                }
            }

            AmqpEncoder.WriteListCount(buffer, pos, count, true);
        }
    }

#if TRACE
    /// <summary>
    /// Returns a string representing the current object for tracing purpose.
    /// </summary>
    /// <param name="name">The object name.</param>
    /// <param name="fieldNames">The field names.</param>
    /// <param name="fieldValues">The field values.</param>
    /// <returns></returns>
    protected string GetDebugString(string name, object[] fieldNames, object[] fieldValues) {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(name);
        sb.Append('(');
        bool addComma = false;
        for (int i = 0; i < fieldValues.Length; i++) {
            if ((this.HasField(i) && fieldValues[i] != null) || Trace.WriteFrameNullFields) {
                if (addComma) {
                    sb.Append(",");
                }

                sb.Append(fieldNames[i]);
                sb.Append(":");
                sb.Append(Trace.GetTraceObject(fieldValues[i]));
                addComma = true;
            }
        }

        sb.Append(')');

        return sb.ToString();
    }
#endif

    private void CheckFieldIndex(int index) {
        if (index < 0 || index >= this.fieldCount) {
            throw new ArgumentOutOfRangeException(Fx.Format("Field index is invalid. {0} has {1} fields.",
                this.GetType().Name, this.fieldCount));
        }
    }
}
