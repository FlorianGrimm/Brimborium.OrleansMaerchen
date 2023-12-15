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
/// The Open class defines the connection negotiation parameters.
/// </summary>
public sealed class Open : DescribedList
{
    private string containerId;
    private string hostName;
    private uint maxFrameSize;
    private ushort channelMax;
    private uint idleTimeOut;
    private object outgoingLocales;
    private object incomingLocales;
    private object offeredCapabilities;
    private object desiredCapabilities;
    private Fields properties;

    /// <summary>
    /// Initializes the Open object.
    /// </summary>
    public Open()
        : base(Codec.Open, 10)
    {
    }

    /// <summary>
    /// Gets or sets the container-id field (index=0).
    /// </summary>
    public string ContainerId
    {
        get { return this.GetField(0, this.containerId); }
        set { this.SetField(0, ref this.containerId, value); }
    }

    /// <summary>
    /// Gets or sets the hostname field (index=1).
    /// </summary>
    public string HostName
    {
        get { return this.GetField(1, this.hostName); }
        set { this.SetField(1, ref this.hostName, value); }
    }

    /// <summary>
    /// Gets or sets the max-frame-size field (index=2).
    /// </summary>
    public uint MaxFrameSize
    {
        get { return this.GetField(2, this.maxFrameSize, uint.MaxValue); }
        set { this.SetField(2, ref this.maxFrameSize, value); }
    }

    /// <summary>
    /// Gets or sets the channel-max field (index=3).
    /// </summary>
    public ushort ChannelMax
    {
        get { return this.GetField(3, this.channelMax, ushort.MaxValue); }
        set { this.SetField(3, ref this.channelMax, value); }
    }

    /// <summary>
    /// Gets or sets the idle-time-out field (index=4).
    /// </summary>
    /// <remarks>To avoid spurious timeouts, the value SHOULD be half the actual timeout threshold.</remarks>
    public uint IdleTimeOut
    {
        get { return this.GetField(4, this.idleTimeOut, 0u); }
        set { this.SetField(4, ref this.idleTimeOut, value); }
    }

    /// <summary>
    /// Gets or sets the outgoing-locales field (index=5).
    /// </summary>
    public Symbol[] OutgoingLocales
    {
        get { return HasField(5) ? Codec.GetSymbolMultiple(ref this.outgoingLocales) : null; }
        set { this.SetField(5, ref this.outgoingLocales, value); }
    }

    /// <summary>
    /// Gets or sets the incoming-locales field (index=6).
    /// </summary>
    public Symbol[] IncomingLocales
    {
        get { return HasField(6) ? Codec.GetSymbolMultiple(ref this.incomingLocales) : null; }
        set { this.SetField(6, ref this.incomingLocales, value); }
    }

    /// <summary>
    /// Gets or sets the offered-capabilities field (index=7).
    /// </summary>
    public Symbol[] OfferedCapabilities
    {
        get { return HasField(7) ? Codec.GetSymbolMultiple(ref this.offeredCapabilities) : null; }
        set { this.SetField(7, ref this.offeredCapabilities, value); }
    }

    /// <summary>
    /// Gets or sets the desired-capabilities field (index=8).
    /// </summary>
    public Symbol[] DesiredCapabilities
    {
        get { return HasField(8) ? Codec.GetSymbolMultiple(ref this.desiredCapabilities) : null; }
        set { this.SetField(8, ref this.desiredCapabilities, value); }
    }

    /// <summary>
    /// Gets or sets the properties field (index=9).
    /// </summary>
    public Fields Properties
    {
        get { return this.GetField(9, this.properties); }
        set { this.SetField(9, ref this.properties, value); }
    }

    internal override void WriteField(ByteBuffer buffer, int index)
    {
        switch (index)
        {
            case 0:
                AmqpEncoder.WriteString(buffer, this.containerId, true);
                break;
            case 1:
                AmqpEncoder.WriteString(buffer, this.hostName, true);
                break;
            case 2:
                AmqpEncoder.WriteUInt(buffer, this.maxFrameSize, true);
                break;
            case 3:
                AmqpEncoder.WriteUShort(buffer, this.channelMax);
                break;
            case 4:
                AmqpEncoder.WriteUInt(buffer, this.idleTimeOut, true);
                break;
            case 5:
                AmqpEncoder.WriteObject(buffer, this.outgoingLocales);
                break;
            case 6:
                AmqpEncoder.WriteObject(buffer, this.incomingLocales);
                break;
            case 7:
                AmqpEncoder.WriteObject(buffer, this.offeredCapabilities);
                break;
            case 8:
                AmqpEncoder.WriteObject(buffer, this.desiredCapabilities);
                break;
            case 9:
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
                this.containerId = AmqpEncoder.ReadString(buffer, formatCode);
                break;
            case 1:
                this.hostName = AmqpEncoder.ReadString(buffer, formatCode);
                break;
            case 2:
                this.maxFrameSize = AmqpEncoder.ReadUInt(buffer, formatCode);
                break;
            case 3:
                this.channelMax = AmqpEncoder.ReadUShort(buffer, formatCode);
                break;
            case 4:
                this.idleTimeOut = AmqpEncoder.ReadUInt(buffer, formatCode);
                break;
            case 5:
                this.outgoingLocales = AmqpEncoder.ReadObject(buffer, formatCode);
                break;
            case 6:
                this.incomingLocales = AmqpEncoder.ReadObject(buffer, formatCode);
                break;
            case 7:
                this.offeredCapabilities = AmqpEncoder.ReadObject(buffer, formatCode);
                break;
            case 8:
                this.desiredCapabilities = AmqpEncoder.ReadObject(buffer, formatCode);
                break;
            case 9:
                this.properties = AmqpEncoder.ReadFields(buffer, formatCode);
                break;
            default:
                AssertException.Assert(false, "Invalid field index");
                break;
        }
    }

#if TRACE
    /// <summary>
    /// Returns a string that represents the current open object.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return this.GetDebugString(
            "open",
            new object[] { "container-id", "host-name", "max-frame-size", "channel-max", "idle-time-out", "outgoing-locales", "incoming-locales", "offered-capabilities", "desired-capabilities", "properties" },
            new object[] {containerId, hostName, maxFrameSize, channelMax, idleTimeOut, outgoingLocales, incomingLocales, offeredCapabilities, desiredCapabilities, properties});
    }
#endif
}