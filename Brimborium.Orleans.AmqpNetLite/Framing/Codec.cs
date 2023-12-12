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

using Brimborium.OrleansAmqp.Sasl;
using Brimborium.OrleansAmqp.Transactions;
using Brimborium.OrleansAmqp.Types;

using System.Collections;

internal static class Codec {
    // transport performatives
    public static readonly Descriptor Open = new Descriptor(0x0000000000000010, "amqp:open:list");
    public static readonly Descriptor Begin = new Descriptor(0x0000000000000011, "amqp:begin:list");
    public static readonly Descriptor Attach = new Descriptor(0x0000000000000012, "amqp:attach:list");
    public static readonly Descriptor Flow = new Descriptor(0x0000000000000013, "amqp:flow:list");
    public static readonly Descriptor Transfer = new Descriptor(0x0000000000000014, "amqp:transfer:list");
    public static readonly Descriptor Dispose = new Descriptor(0x0000000000000015, "amqp:disposition:list");
    public static readonly Descriptor Detach = new Descriptor(0x0000000000000016, "amqp:detach:list");
    public static readonly Descriptor End = new Descriptor(0x0000000000000017, "amqp:end:list");
    public static readonly Descriptor Close = new Descriptor(0x0000000000000018, "amqp:close:list");

    public static readonly Descriptor Error = new Descriptor(0x000000000000001d, "amqp:error:list");

    // outcome
    public static readonly Descriptor Received = new Descriptor(0x0000000000000023, "amqp:received:list");
    public static readonly Descriptor Accepted = new Descriptor(0x0000000000000024, "amqp:accepted:list");
    public static readonly Descriptor Rejected = new Descriptor(0x0000000000000025, "amqp:rejected:list");
    public static readonly Descriptor Released = new Descriptor(0x0000000000000026, "amqp:released:list");
    public static readonly Descriptor Modified = new Descriptor(0x0000000000000027, "amqp:modified:list");

    public static readonly Descriptor Source = new Descriptor(0x0000000000000028, "amqp:source:list");
    public static readonly Descriptor Target = new Descriptor(0x0000000000000029, "amqp:target:list");

    // sasl
    public static readonly Descriptor SaslMechanisms = new Descriptor(0x0000000000000040, "amqp:sasl-mechanisms:list");
    public static readonly Descriptor SaslInit = new Descriptor(0x0000000000000041, "amqp:sasl-init:list");
    public static readonly Descriptor SaslChallenge = new Descriptor(0x0000000000000042, "amqp:sasl-challenge:list");
    public static readonly Descriptor SaslResponse = new Descriptor(0x0000000000000043, "amqp:sasl-response:list");
    public static readonly Descriptor SaslOutcome = new Descriptor(0x0000000000000044, "amqp:sasl-outcome:list");

    // message
    public static readonly Descriptor Header = new Descriptor(0x0000000000000070, "amqp:header:list");
    public static readonly Descriptor DeliveryAnnotations = new Descriptor(0x0000000000000071, "amqp:delivery-annotations:map");
    public static readonly Descriptor MessageAnnotations = new Descriptor(0x0000000000000072, "amqp:message-annotations:map");
    public static readonly Descriptor Properties = new Descriptor(0x0000000000000073, "amqp:properties:list");
    public static readonly Descriptor ApplicationProperties = new Descriptor(0x0000000000000074, "amqp:application-properties:map");
    public static readonly Descriptor Data = new Descriptor(0x0000000000000075, "amqp:data:binary");
    public static readonly Descriptor AmqpSequence = new Descriptor(0x0000000000000076, "amqp:amqp-sequence:list");
    public static readonly Descriptor AmqpValue = new Descriptor(0x0000000000000077, "amqp:amqp-value:*");
    public static readonly Descriptor Footer = new Descriptor(0x0000000000000078, "amqp:footer:map");

    // transactions
    public static readonly Descriptor Coordinator = new Descriptor(0x0000000000000030, "amqp:coordinator:list");
    public static readonly Descriptor Declare = new Descriptor(0x0000000000000031, "amqp:declare:list");
    public static readonly Descriptor Discharge = new Descriptor(0x0000000000000032, "amqp:discharge:list");
    public static readonly Descriptor Declared = new Descriptor(0x0000000000000033, "amqp:declared:list");
    public static readonly Descriptor TransactionalState = new Descriptor(0x0000000000000034, "amqp:transactional-state:list");

    static Codec() {
        AmqpEncoder.Initialize();

        AmqpEncoder.AddKnownDescribed(Codec.Open, () => new Open());
        AmqpEncoder.AddKnownDescribed(Codec.Begin, () => new Begin());
        AmqpEncoder.AddKnownDescribed(Codec.Attach, () => new Attach());
        AmqpEncoder.AddKnownDescribed(Codec.Detach, () => new Detach());
        AmqpEncoder.AddKnownDescribed(Codec.End, () => new End());
        AmqpEncoder.AddKnownDescribed(Codec.Close, () => new Close());
        AmqpEncoder.AddKnownDescribed(Codec.Flow, () => new Flow());
        AmqpEncoder.AddKnownDescribed(Codec.Dispose, () => new Dispose());
        AmqpEncoder.AddKnownDescribed(Codec.Transfer, () => new Transfer());

        AmqpEncoder.AddKnownDescribed(Codec.Error, () => new Error(null));
        AmqpEncoder.AddKnownDescribed(Codec.Source, () => new Source());
        AmqpEncoder.AddKnownDescribed(Codec.Target, () => new Target());

        AmqpEncoder.AddKnownDescribed(Codec.Accepted, () => new Accepted());
        AmqpEncoder.AddKnownDescribed(Codec.Rejected, () => new Rejected());
        AmqpEncoder.AddKnownDescribed(Codec.Released, () => new Released());
        AmqpEncoder.AddKnownDescribed(Codec.Modified, () => new Modified());
        AmqpEncoder.AddKnownDescribed(Codec.Received, () => new Received());

        AmqpEncoder.AddKnownDescribed(Codec.SaslMechanisms, () => new SaslMechanisms());
        AmqpEncoder.AddKnownDescribed(Codec.SaslInit, () => new SaslInit());
        AmqpEncoder.AddKnownDescribed(Codec.SaslChallenge, () => new SaslChallenge());
        AmqpEncoder.AddKnownDescribed(Codec.SaslResponse, () => new SaslResponse());
        AmqpEncoder.AddKnownDescribed(Codec.SaslOutcome, () => new SaslOutcome());

        AmqpEncoder.AddKnownDescribed(Codec.Header, () => new Header());
        AmqpEncoder.AddKnownDescribed(Codec.DeliveryAnnotations, () => new DeliveryAnnotations());
        AmqpEncoder.AddKnownDescribed(Codec.MessageAnnotations, () => new MessageAnnotations());
        AmqpEncoder.AddKnownDescribed(Codec.Properties, () => new Properties());
        AmqpEncoder.AddKnownDescribed(Codec.ApplicationProperties, () => new ApplicationProperties());
        AmqpEncoder.AddKnownDescribed(Codec.Data, () => new Data());
        AmqpEncoder.AddKnownDescribed(Codec.AmqpSequence, () => new AmqpSequence());
        AmqpEncoder.AddKnownDescribed(Codec.AmqpValue, () => new AmqpValue());
        AmqpEncoder.AddKnownDescribed(Codec.Footer, () => new Footer());

        AmqpEncoder.AddKnownDescribed(Codec.Coordinator, () => new Coordinator());
        AmqpEncoder.AddKnownDescribed(Codec.Declare, () => new Declare());
        AmqpEncoder.AddKnownDescribed(Codec.Discharge, () => new Discharge());
        AmqpEncoder.AddKnownDescribed(Codec.Declared, () => new Declared());
        AmqpEncoder.AddKnownDescribed(Codec.TransactionalState, () => new TransactionalState());
    }

    // Transport layer should call Codec to encode/decode frames. It ensures that
    // all dependant static fields in other class are initialized correctly.
    // NETMF does not track cross-class static field/ctor dependencies

    public static void Encode(RestrictedDescribed command, ByteBuffer buffer) {
        ArgumentNullException.ThrowIfNull(command, nameof(command));
        command.Encode(buffer);
    }

    public static object Decode(ByteBuffer buffer) {
        return AmqpEncoder.ReadDescribed(buffer, AmqpEncoder.ReadFormatCode(buffer));
    }

    public static Symbol[] GetSymbolMultiple(object[] fields, int index) {
        if (fields[index] == null) {
            return null;
        }

        if (fields[index] is Symbol[] symbols) {
            return symbols;
        }

        if (fields[index] is Symbol symbol) {
            symbols = new Symbol[] { symbol };
            fields[index] = symbols;
            return symbols;
        }

        throw new AmqpException(ErrorCode.InvalidField, Fx.Format("{0} {1}", index, fields[index].GetType().Name));
    }

    public static Symbol[] GetSymbolMultiple(ref object obj) {
        if (obj == null) {
            return null;
        }

        if (obj is Symbol[] symbols) {
            return symbols;
        }

        if (obj is Symbol symbol) {
            symbols = new Symbol[] { symbol };
            obj = symbols;
            return symbols;
        }

        throw new AmqpException(ErrorCode.InvalidField, obj.GetType().Name);
    }
}