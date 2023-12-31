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

namespace Brimborium.OrleansAmqp.Framing;

/// <summary>
/// An AMQP Value section contains a single strongly typed value.
/// </summary>
public sealed class AmqpValue<T> : AmqpValue {
    private readonly AmqpSerializer _Serializer;

    /// <summary>
    /// Initializes an AmqpValue object.
    /// </summary>
    public AmqpValue(T value)
        : this(value, AmqpSerializer.instance) {
    }

    /// <summary>
    /// Initializes an AmqpValue object with an <see cref="AmqpSerializer"/> that
    /// is used to serialize the value.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="serializer"></param>
    public AmqpValue(T value, AmqpSerializer serializer)
        : base() {
        this.Value = value;
        this._Serializer = serializer;
    }

    /// <summary>
    /// Writes the value into the buffer using Brimborium.OrleansAmqpSerializer.
    /// </summary>
    /// <param name="buffer">The buffer to write the encoded object.</param>
    /// <param name="value">The object to be written.</param>
    protected override void WriteValue(ByteBuffer buffer, object value) {
        this._Serializer.WriteObject(buffer, this.Value);
    }
}
