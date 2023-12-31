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
/// The accepted outcome.
/// </summary>
public sealed class Accepted : Outcome {
    /// <summary>
    /// Initializes an accepted outcome.
    /// </summary>
    public Accepted()
        : base(Codec.Accepted, 0) {
    }

    internal override void WriteField(ByteBuffer buffer, int index) {
        AssertException.Assert(false, "Invalid field index");
    }

    internal override void ReadField(ByteBuffer buffer, int index, byte formatCode) {
        AssertException.Assert(false, "Invalid field index");
    }

    /// <summary>
    /// Returns a string that represents the current accepted outcome.
    /// </summary>
    /// <returns></returns>
    public override string ToString() {
#if TRACE
        return this.GetDebugString(
            "accepted",
            new object[0],
            new object[0]);
#else
        return base.ToString();
#endif
    }
}