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

using Brimborium.OrleansAmqp.Types;

/// <summary>
/// The Attach class defines an attach frame to attach a Link Endpoint to the Session.
/// </summary>
public sealed class Attach : DescribedList
{
    private string linkName;
    private uint handle;
    private bool role;
    private SenderSettleMode sndSettleMode;
    private ReceiverSettleMode rcvSettleMode;
    private object source;
    private object target;
    private Map unsettled;
    private bool incompleteUnsettled;
    private uint initialDeliveryCount;
    private ulong maxMessageSize;
    private object offeredCapabilities;
    private object desiredCapabilities;
    private Fields properties;

    /// <summary>
    /// Initializes an attach object.
    /// </summary>
    public Attach()
        : base(Codec.Attach, 14)
    {
    }

    /// <summary>
    /// Gets or sets the name field (index=0).
    /// </summary>
    public string LinkName
    {
        get { return this.GetField(0, this.linkName); }
        set { this.SetField(0, ref this.linkName, value); }
    }

    /// <summary>
    /// Gets or sets the handle field (index=1).
    /// </summary>
    public uint Handle
    {
        get { return this.GetField(1, this.handle, uint.MinValue); }
        set { this.SetField(1, ref this.handle, value); }
    }

    /// <summary>
    /// Gets or sets the role field (index=2).
    /// </summary>
    public bool Role
    {
        get { return this.GetField(2, this.role, false); }
        set { this.SetField(2, ref this.role, value); }
    }

    /// <summary>
    /// Gets or sets the snd-settle-mode field (index=3).
    /// </summary>
    public SenderSettleMode SndSettleMode
    {
        get { return this.GetField(3, this.sndSettleMode, SenderSettleMode.Unsettled); }
        set { this.SetField(3, ref this.sndSettleMode, value); }
    }

    /// <summary>
    /// Gets or sets the rcv-settle-mode field (index=4).
    /// </summary>
    public ReceiverSettleMode RcvSettleMode
    {
        get { return this.GetField(4, this.rcvSettleMode, ReceiverSettleMode.First); }
        set { this.SetField(4, ref this.rcvSettleMode, value); }
    }

    /// <summary>
    /// Gets or sets the source field (index=5).
    /// </summary>
    public object Source
    {
        get { return this.GetField(5, this.source); }
        set { this.SetField(5, ref this.source, value); }
    }

    /// <summary>
    /// Gets or sets the target field (index=6).
    /// </summary>
    public object Target
    {
        get { return this.GetField(6, this.target); }
        set { this.SetField(6, ref this.target, value); }
    }

    /// <summary>
    /// Gets or sets the unsettled field (index=7).
    /// </summary>
    public Map Unsettled
    {
        get { return this.GetField(7, this.unsettled); }
        set { this.SetField(7, ref this.unsettled, value); }
    }

    /// <summary>
    /// Gets or sets the incomplete-unsettled field (index=8).
    /// </summary>
    public bool IncompleteUnsettled
    {
        get { return this.GetField(8, this.incompleteUnsettled, false); }
        set { this.SetField(8, ref this.incompleteUnsettled, value); }
    }

    /// <summary>
    /// Gets or sets the initial-delivery-count field (index=9).
    /// </summary>
    public uint InitialDeliveryCount
    {
        get { return this.GetField(9, this.initialDeliveryCount, uint.MinValue); }
        set { this.SetField(9, ref this.initialDeliveryCount, value); }
    }

    /// <summary>
    /// Gets or sets the max-message-size field (index=10).
    /// </summary>
    public ulong MaxMessageSize
    {
        get { return this.GetField(10, this.maxMessageSize, ulong.MaxValue); }
        set { this.SetField(10, ref this.maxMessageSize, value); }
    }

    /// <summary>
    /// Gets or sets the offered-capabilities field (index=11).
    /// </summary>
    public Symbol[] OfferedCapabilities
    {
        get { return HasField(11) ? Codec.GetSymbolMultiple(ref this.offeredCapabilities) : null; }
        set { this.SetField(11, ref this.offeredCapabilities, value); }
    }

    /// <summary>
    /// Gets or sets the desired-capabilities field (index=12).
    /// </summary>
    public Symbol[] DesiredCapabilities
    {
        get { return HasField(12) ? Codec.GetSymbolMultiple(ref this.desiredCapabilities) : null; }
        set { this.SetField(12, ref this.desiredCapabilities, value); }
    }

    /// <summary>
    /// Gets or sets the properties field (index=13).
    /// </summary>
    public Fields Properties
    {
        get { return this.GetField(13, this.properties); }
        set { this.SetField(13, ref this.properties, value); }
    }

    internal override void WriteField(ByteBuffer buffer, int index)
    {
        switch (index)
        {
            case 0:
                AmqpEncoder.WriteString(buffer, this.linkName, true);
                break;
            case 1:
                AmqpEncoder.WriteUInt(buffer, this.handle, true);
                break;
            case 2:
                AmqpEncoder.WriteBoolean(buffer, this.role, true);
                break;
            case 3:
                AmqpEncoder.WriteUByte(buffer, (byte)this.sndSettleMode);
                break;
            case 4:
                AmqpEncoder.WriteUByte(buffer, (byte)this.rcvSettleMode);
                break;
            case 5:
                AmqpEncoder.WriteObject(buffer, this.source);
                break;
            case 6:
                AmqpEncoder.WriteObject(buffer, this.target);
                break;
            case 7:
                AmqpEncoder.WriteMap(buffer, this.unsettled, true);
                break;
            case 8:
                AmqpEncoder.WriteBoolean(buffer, this.incompleteUnsettled, true);
                break;
            case 9:
                AmqpEncoder.WriteUInt(buffer, this.initialDeliveryCount, true);
                break;
            case 10:
                AmqpEncoder.WriteULong(buffer, this.maxMessageSize, true);
                break;
            case 11:
                AmqpEncoder.WriteObject(buffer, this.offeredCapabilities);
                break;
            case 12:
                AmqpEncoder.WriteObject(buffer, this.desiredCapabilities);
                break;
            case 13:
                AmqpEncoder.WriteMap(buffer, this.properties, true);
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
                this.linkName = AmqpEncoder.ReadString(buffer, formatCode);
                break;
            case 1:
                this.handle = AmqpEncoder.ReadUInt(buffer, formatCode);
                break;
            case 2:
                this.role = AmqpEncoder.ReadBoolean(buffer, formatCode);
                break;
            case 3:
                this.sndSettleMode = (SenderSettleMode)AmqpEncoder.ReadUByte(buffer, formatCode);
                break;
            case 4:
                this.rcvSettleMode = (ReceiverSettleMode)AmqpEncoder.ReadUByte(buffer, formatCode);
                break;
            case 5:
                this.source = AmqpEncoder.ReadObject(buffer, formatCode);
                break;
            case 6:
                this.target = AmqpEncoder.ReadObject(buffer, formatCode);
                break;
            case 7:
                this.unsettled = AmqpEncoder.ReadMap(buffer, formatCode);
                break;
            case 8:
                this.incompleteUnsettled = AmqpEncoder.ReadBoolean(buffer, formatCode);
                break;
            case 9:
                this.initialDeliveryCount = AmqpEncoder.ReadUInt(buffer, formatCode);
                break;
            case 10:
                this.maxMessageSize = AmqpEncoder.ReadULong(buffer, formatCode);
                break;
            case 11:
                this.offeredCapabilities = AmqpEncoder.ReadObject(buffer, formatCode);
                break;
            case 12:
                this.desiredCapabilities = AmqpEncoder.ReadObject(buffer, formatCode);
                break;
            case 13:
                this.properties = AmqpEncoder.ReadFields(buffer, formatCode);
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
            "attach",
            new object[] { "name", "handle", "role", "snd-settle-mode", "rcv-settle-mode", "source", "target", "unsettled", "incomplete-unsettled", "initial-delivery-count", "max-message-size", "offered-capabilities", "desired-capabilities", "properties" },
            new object[] { linkName, handle, role, sndSettleMode, rcvSettleMode, source, target, unsettled, incompleteUnsettled, initialDeliveryCount, maxMessageSize, offeredCapabilities, desiredCapabilities, properties });
    }
#endif
}