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

namespace Brimborium.OrleansAmqp.Serialization;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Brimborium.OrleansAmqp.Types;

internal static class SerializationCallback
{
    public const int OnSerializing = 0;
    public const int OnSerialized = 1;
    public const int OnDeserializing = 2;
    public const int OnDeserialized = 3;
}

internal abstract class SerializableType
{
    private readonly AmqpSerializer serializer;
    private readonly Type type;
    private readonly bool hasDefaultCtor;

    protected SerializableType(AmqpSerializer serializer, Type type)
    {
        this.serializer = serializer;
        this.type = type;
        this.hasDefaultCtor = type.GetConstructor(Type.EmptyTypes) != null;
    }

    public virtual EncodingType Encoding
    {
        get
        {
            throw new InvalidOperationException();
        }
    }

    public virtual bool IsResolved
    {
        get
        {
            return true;
        }
    }

    public virtual SerializableMember[] Members
    {
        get
        {
            throw new InvalidOperationException();
        }
    }

    public static SerializableType CreatePrimitiveType(Type type, Encode encoder, Decode decoder)
    {
        return new AmqpPrimitiveType(type, encoder, decoder);
    }

    public static SerializableType CreateObjectType(Type type)
    {
        return new AmqpObjectType(type);
    }

    public static SerializableType CreateEnumType(Type type, SerializableType underlyingType)
    {
        return new EnumType(type, underlyingType);
    }

    public static SerializableType CreateAmqpSerializableType(AmqpSerializer serializer, Type type)
    {
        return new AmqpSerializableType(serializer, type);
    }

    public static SerializableType CreateArrayType(AmqpSerializer serializer, Type type, Type itemType, SerializableType listType)
    {
        return new ArrayType(serializer, type, itemType, listType);
    }

    public static SerializableType CreateDelegatingType(AmqpSerializer serializer, Type type)
    {
        return new DelegatingType(serializer, type);
    }

    public static SerializableType CreateGenericListType(
        AmqpSerializer serializer,
        Type type,
        SerializableType itemType,
        MethodAccessor addAccessor)
    {
        return new GenericListType(serializer, type, itemType, addAccessor);
    }

    public static SerializableType CreateGenericMapType(
        AmqpSerializer serializer,
        Type type,
        SerializableType keyType,
        SerializableType valueType,
        MemberAccessor keyAccessor,
        MemberAccessor valueAccessor,
        MethodAccessor addAccessor)
    {
        return new GenericMapType(serializer, type, keyType, valueType, keyAccessor, valueAccessor, addAccessor);
    }

    public static SerializableType CreateDescribedListType(
        AmqpSerializer serializer,
        Type type,
        SerializableType baseType,
        string descriptorName,
        ulong? descriptorCode,
        SerializableMember[] members,
        SerializableType[] knownTypes,
        MethodAccessor[] serializationCallbacks)
    {
        return new DescribedListType(serializer, type, baseType, descriptorName,
            descriptorCode, members, knownTypes, serializationCallbacks);
    }

    public static SerializableType CreateDescribedMapType(
        AmqpSerializer serializer,
        Type type,
        SerializableType baseType,
        string descriptorName,
        ulong? descriptorCode,
        SerializableMember[] members,
        SerializableType[] knownTypes,
        MethodAccessor[] serializationCallbacks)
    {
        return new DescribedMapType(serializer, type, baseType, descriptorName, descriptorCode,
            members, knownTypes, serializationCallbacks);
    }

    public static SerializableType CreateDescribedSimpleMapType(
        AmqpSerializer serializer,
        Type type,
        SerializableType baseType,
        SerializableMember[] members,
        MethodAccessor[] serializationCallbacks)
    {
        return new DescribedSimpleMapType(serializer, type, baseType, members, serializationCallbacks);
    }

    public static SerializableType CreateDescribedSimpleListType(
        AmqpSerializer serializer,
        Type type,
        SerializableType baseType,
        SerializableMember[] members,
        MethodAccessor[] serializationCallbacks)
    {
        return new DescribedSimpleListType(serializer, type, baseType, members, serializationCallbacks);
    }

    public abstract void WriteObject(ByteBuffer buffer, object graph);

    public abstract object ReadObject(ByteBuffer buffer);

    private sealed class DelegatingType : SerializableType
    {
        private SerializableType serializableType;

        public DelegatingType(AmqpSerializer serializer, Type type) :
            base(serializer, type)
        {
        }

        public override EncodingType Encoding
        {
            get
            {
                return this.Serializable.Encoding;
            }
        }

        public override SerializableMember[] Members
        {
            get
            {
                return this.Serializable.Members;
            }
        }

        public override bool IsResolved
        {
            get
            {
                return false;
            }
        }

        private SerializableType Serializable
        {
            get
            {
                if (this.serializableType == null)
                {
                    this.serializableType = this.serializer.GetType(this.type);
                }

                return this.serializableType;
            }
        }

        public override object ReadObject(ByteBuffer buffer)
        {
            return this.Serializable.ReadObject(buffer);
        }

        public override void WriteObject(ByteBuffer buffer, object graph)
        {
            this.Serializable.WriteObject(buffer, graph);
        }
    }

    private sealed class AmqpPrimitiveType : SerializableType
    {
        private readonly Encode encoder;
        private readonly Decode decoder;

        public AmqpPrimitiveType(Type type, Encode encoder, Decode decoder)
            : base(null, type)
        {
            this.encoder = encoder;
            this.decoder = decoder;
        }

        public override void WriteObject(ByteBuffer buffer, object value)
        {
            this.encoder(buffer, value, true);
        }

        public override object ReadObject(ByteBuffer buffer)
        {
            byte formatCode = AmqpEncoder.ReadFormatCode(buffer);
            return this.decoder(buffer, formatCode);
        }
    }

    private sealed class AmqpObjectType : SerializableType
    {
        public AmqpObjectType(Type type)
            : base(null, type)
        {
        }

        public override void WriteObject(ByteBuffer buffer, object value)
        {
            AmqpEncoder.WriteObject(buffer, value);
        }

        public override object ReadObject(ByteBuffer buffer)
        {
            return AmqpEncoder.ReadObject(buffer);
        }
    }

    private sealed class EnumType : SerializableType
    {
        private readonly SerializableType underlyingType;

        public EnumType(Type type, SerializableType underlyingType)
            : base(null, type)
        {
            this.underlyingType = underlyingType;
        }

        public override void WriteObject(ByteBuffer buffer, object value)
        {
            if (value == null)
            {
                AmqpEncoder.WriteObject(buffer, value);
            }
            else
            {
                value = Convert.ChangeType(value, this.underlyingType.type);
                this.underlyingType.WriteObject(buffer, value);
            }
        }

        public override object ReadObject(ByteBuffer buffer)
        {
            object value = this.underlyingType.ReadObject(buffer);
            if (value != null)
            {
                value = Enum.ToObject(this.type, value);
            }

            return value;
        }
    }

    private sealed class AmqpSerializableType : SerializableType
    {
        public AmqpSerializableType(AmqpSerializer serializer, Type type)
            : base(serializer, type)
        {
        }

        public override void WriteObject(ByteBuffer buffer, object value)
        {
            if (value == null)
            {
                AmqpEncoder.WriteObject(buffer, value);
            }
            else
            {
                ((IAmqpSerializable)value).Encode(buffer);
            }
        }

        public override object ReadObject(ByteBuffer buffer)
        {
            buffer.Validate(false, FixedWidth.FormatCode);
            byte formatCode = buffer.Buffer[buffer.Offset];
            if (formatCode == FormatCode.Null)
            {
                buffer.Complete(FixedWidth.FormatCode);
                return null;
            }
            else
            {
                object value = this.type.CreateInstance(this.hasDefaultCtor);
                ((IAmqpSerializable)value).Decode(buffer);
                return value;
            }
        }
    }

    private abstract class CollectionType : SerializableType
    {
        protected CollectionType(AmqpSerializer serializer, Type type)
            : base(serializer, type)
        {
        }

        protected abstract int WriteMembers(ByteBuffer buffer, object container);

        protected abstract void ReadMembers(ByteBuffer buffer, object container, ref int count);

        protected abstract bool WriteFormatCode(ByteBuffer buffer);

        protected abstract void Initialize(ByteBuffer buffer, byte formatCode,
            out int size, out int count, out int encodeWidth, out CollectionType effectiveType);

        public override void WriteObject(ByteBuffer buffer, object graph)
        {
            if (graph == null)
            {
                AmqpEncoder.WriteObject(buffer, null);
                return;
            }

            if (!this.WriteFormatCode(buffer))
            {
                return;
            }

            int pos = buffer.WritePos;                  // remember the current position
            AmqpBitConverter.WriteULong(buffer, 0);     // reserve space for size and count

            int count = this.WriteMembers(buffer, graph);

            AmqpBitConverter.WriteInt(buffer.Buffer, pos, buffer.WritePos - pos - FixedWidth.UInt);
            AmqpBitConverter.WriteInt(buffer.Buffer, pos + FixedWidth.UInt, count);
        }

        public override object ReadObject(ByteBuffer buffer)
        {
            byte formatCode = AmqpEncoder.ReadFormatCode(buffer);
            if (formatCode == FormatCode.Null)
            {
                return null;
            }

            int size;
            int count;
            int encodeWidth;
            CollectionType effectiveType;
            this.Initialize(buffer, formatCode, out size, out count, out encodeWidth, out effectiveType);
            int offset = buffer.Offset;

            object container = effectiveType.type.CreateInstance(effectiveType.hasDefaultCtor);

            if (count > 0)
            {
                effectiveType.ReadMembers(buffer, container, ref count);

                if (count > 0)
                {
                    // skip unknown members
                    buffer.Complete(size - (buffer.Offset - offset) - encodeWidth);
                }
            }

            return container;
        }

        protected static void ReadSizeAndCount(ByteBuffer buffer, byte formatCode, out int size, out int count, out int width)
        {
            if (formatCode == FormatCode.List0)
            {
                size = count = width = 0;
            }
            else if (formatCode == FormatCode.List8 || formatCode == FormatCode.Map8)
            {
                width = FixedWidth.UByte;
                size = AmqpBitConverter.ReadUByte(buffer);
                count = AmqpBitConverter.ReadUByte(buffer);
            }
            else if (formatCode == FormatCode.List32 || formatCode == FormatCode.Map32)
            {
                width = FixedWidth.UInt;
                size = (int)AmqpBitConverter.ReadUInt(buffer);
                count = (int)AmqpBitConverter.ReadUInt(buffer);
            }
            else
            {
                throw new AmqpException(ErrorCode.InvalidField, Fx.Format(SRAmqp.AmqpInvalidFormatCode, formatCode, buffer.Offset));
            }
        }
    }

    private sealed class ArrayType : SerializableType
    {
        private readonly Type itemType;
        private readonly SerializableType listType;

        public ArrayType(AmqpSerializer serializer, Type type, Type itemType, SerializableType listType)
            : base(serializer, type)
        {
            this.itemType = itemType;
            this.listType = listType;
        }

        public override void WriteObject(ByteBuffer buffer, object graph)
        {
            this.listType.WriteObject(buffer, graph);
        }

        public override object ReadObject(ByteBuffer buffer)
        {
            object value = this.listType.ReadObject(buffer);
            if (value != null)
            {
                ICollection list = (ICollection)value;
                Array array = Array.CreateInstance(this.itemType, list.Count);
                list.CopyTo(array, 0);
                value = array;
            }

            return value;
        }
    }

    private sealed class GenericListType : CollectionType
    {
        private readonly SerializableType itemType;
        private readonly MethodAccessor addMethodAccessor;

        public GenericListType(AmqpSerializer serializer, Type type, SerializableType itemType, MethodAccessor addAccessor)
            : base(serializer, type)
        {
            this.itemType = itemType;
            this.addMethodAccessor = addAccessor;
        }

        protected override bool WriteFormatCode(ByteBuffer buffer)
        {
            AmqpBitConverter.WriteUByte(buffer, FormatCode.List32);
            return true;
        }

        protected override int WriteMembers(ByteBuffer buffer, object container)
        {
            int count = 0;
            foreach (object item in (IEnumerable)container)
            {
                if (item == null)
                {
                    AmqpEncoder.WriteObject(buffer, null);
                }
                else
                {
                    SerializableType effectiveType = this.itemType;
                    if (item.GetType() != effectiveType.type)
                    {
                        effectiveType = this.serializer.GetType(item.GetType());
                    }

                    effectiveType.WriteObject(buffer, item);
                }

                count++;
            }

            return count;
        }

        protected override void Initialize(ByteBuffer buffer, byte formatCode,
            out int size, out int count, out int encodeWidth, out CollectionType effectiveType)
        {
            effectiveType = this;
            ReadSizeAndCount(buffer, formatCode, out size, out count, out encodeWidth);
        }

        protected override void ReadMembers(ByteBuffer buffer, object container, ref int count)
        {
            for (; count > 0; count--)
            {
                object value = this.itemType.ReadObject(buffer);
                this.addMethodAccessor.Invoke(container, new object[] { value });
            }
        }
    }

    private sealed class GenericMapType : CollectionType
    {
        private readonly SerializableType keyType;
        private readonly SerializableType valueType;
        private readonly MemberAccessor keyAccessor;
        private readonly MemberAccessor valueAccessor;
        private readonly MethodAccessor addMethodAccessor;

        public GenericMapType(AmqpSerializer serializer,
            Type type,
            SerializableType keyType,
            SerializableType valueType,
            MemberAccessor keyAccessor,
            MemberAccessor valueAccessor,
            MethodAccessor addAccessor)
            : base(serializer, type)
        {
            this.keyType = keyType;
            this.valueType = valueType;
            this.keyAccessor = keyAccessor;
            this.valueAccessor = valueAccessor;
            this.addMethodAccessor = addAccessor;
        }

        protected override bool WriteFormatCode(ByteBuffer buffer)
        {
            AmqpBitConverter.WriteUByte(buffer, FormatCode.Map32);
            return true;
        }

        protected override int WriteMembers(ByteBuffer buffer, object container)
        {
            int count = 0;
            foreach (object item in (IEnumerable)container)
            {
                object key = this.keyAccessor.Get(item);
                object value = this.valueAccessor.Get(item);
                if (value != null)
                {
                    this.keyType.WriteObject(buffer, key);

                    SerializableType effectiveType = this.valueType;
                    if (value.GetType() != effectiveType.type)
                    {
                        effectiveType = this.serializer.GetType(value.GetType());
                    }

                    effectiveType.WriteObject(buffer, value);

                    count += 2;
                }
            }
            return count;
        }

        protected override void Initialize(ByteBuffer buffer, byte formatCode,
            out int size, out int count, out int encodeWidth, out CollectionType effectiveType)
        {
            effectiveType = this;
            ReadSizeAndCount(buffer, formatCode, out size, out count, out encodeWidth);
        }

        protected override void ReadMembers(ByteBuffer buffer, object container, ref int count)
        {
            for (; count > 0; count -= 2)
            {
                object key = this.keyType.ReadObject(buffer);
                object value = this.valueType.ReadObject(buffer);
                this.addMethodAccessor.Invoke(container, new object[] { key, value });
            }
        }
    }

    private abstract class DescribedCompoundType : CollectionType
    {
        private readonly Symbol descriptorName;
        private readonly ulong? descriptorCode;
        private readonly SerializableMember[] members;
        private readonly MethodAccessor[] serializationCallbacks;
        private readonly SerializableType[] knownTypes;
        private SerializableType baseType;

        protected DescribedCompoundType(
            AmqpSerializer serializer,
            Type type,
            SerializableType baseType,
            string descriptorName,
            ulong? descriptorCode,
            SerializableMember[] members,
            SerializableType[] knownTypes,
            MethodAccessor[] serializationCallbacks)
            : base(serializer, type)
        {
            this.baseType = baseType;
            this.descriptorName = descriptorName;
            this.descriptorCode = descriptorCode;
            this.members = members;
            this.serializationCallbacks = serializationCallbacks;
            this.knownTypes = knownTypes;
        }

        public override SerializableMember[] Members
        {
            get { return this.members; }
        }

        protected abstract byte Code
        {
            get;
        }

        protected override int WriteMembers(ByteBuffer buffer, object container)
        {
            this.InvokeSerializationCallback(SerializationCallback.OnSerializing, container);

            int count = 0;
            foreach (SerializableMember member in this.members)
            {
                object memberValue = member.Accessor.Get(container);
                SerializableType effectiveType = member.Type;
                if (memberValue != null && memberValue.GetType() != effectiveType.type)
                {
                    effectiveType = this.serializer.GetType(memberValue.GetType());
                }

                count += this.WriteMemberValue(buffer, member.Name, memberValue, effectiveType);
            }

            this.InvokeSerializationCallback(SerializationCallback.OnSerialized, container);

            return count;
        }

        protected override void ReadMembers(ByteBuffer buffer, object container, ref int count)
        {
            this.InvokeSerializationCallback(SerializationCallback.OnDeserializing, container);

            for (int i = 0; i < this.members.Length && count > 0; ++i)
            {
                count -= this.ReadMemberValue(buffer, this.members[i], container);
            }

            this.InvokeSerializationCallback(SerializationCallback.OnDeserialized, container);
        }

        protected abstract int WriteMemberValue(ByteBuffer buffer, string memberName, object memberValue, SerializableType effectiveType);

        protected abstract int ReadMemberValue(ByteBuffer buffer, SerializableMember serialiableMember, object container);

        protected override bool WriteFormatCode(ByteBuffer buffer)
        {
            AmqpBitConverter.WriteUByte(buffer, FormatCode.Described);
            if (this.descriptorCode != null)
            {
                AmqpEncoder.WriteULong(buffer, this.descriptorCode.Value, true);
            }
            else
            {
                AmqpEncoder.WriteSymbol(buffer, this.descriptorName, true);
            }

            AmqpBitConverter.WriteUByte(buffer, this.Code);
            return true;
        }

        protected override void Initialize(ByteBuffer buffer, byte formatCode,
            out int size, out int count, out int encodeWidth, out CollectionType effectiveType)
        {
            if (formatCode != FormatCode.Described)
            {
                throw new AmqpException(ErrorCode.InvalidField, Fx.Format(SRAmqp.AmqpInvalidFormatCode, formatCode, buffer.Offset));
            }

            effectiveType = null;
            formatCode = AmqpEncoder.ReadFormatCode(buffer);
            ulong? code = null;
            Symbol symbol = default(Symbol);
            if (formatCode == FormatCode.ULong0)
            {
                code = 0;
            }
            else if (formatCode == FormatCode.ULong || formatCode == FormatCode.SmallULong)
            {
                code = AmqpEncoder.ReadULong(buffer, formatCode);
            }
            else if (formatCode == FormatCode.Symbol8 || formatCode == FormatCode.Symbol32)
            {
                symbol = AmqpEncoder.ReadSymbol(buffer, formatCode);
            }

            if (this.AreEqual(this.descriptorCode, this.descriptorName, code, symbol))
            {
                effectiveType = this;
            }
            else if (this.knownTypes != null)
            {
                for (int i = 0; i < this.knownTypes.Length; ++i)
                {
                    SerializableType knownType = this.knownTypes[i];
                    if (!knownType.IsResolved)
                    {
                        knownType = this.serializer.GetType(knownType.type);
                        this.knownTypes[i] = knownType;
                    }

                    DescribedCompoundType describedKnownType = (DescribedCompoundType)knownType;
                    if (this.AreEqual(describedKnownType.descriptorCode, describedKnownType.descriptorName, code, symbol))
                    {
                        effectiveType = describedKnownType;
                        break;
                    }
                }
            }

            if (effectiveType == null)
            {
                throw new AmqpException(ErrorCode.DecodeError,
                    Fx.Format(SRAmqp.AmqpUnknownDescriptor, code != null ? code.ToString() : symbol!.ToString(), this.type.Name));
            }

            formatCode = AmqpEncoder.ReadFormatCode(buffer);
            ReadSizeAndCount(buffer, formatCode, out size, out count, out encodeWidth);
        }

        private void InvokeSerializationCallback(int callbackIndex, object container)
        {
            if (this.baseType != null)
            {
                if (!this.baseType.IsResolved)
                {
                    this.baseType = this.serializer.GetType(this.baseType.type);
                }

                ((DescribedCompoundType)this.baseType).InvokeSerializationCallback(callbackIndex, container);
            }

            var callback = this.serializationCallbacks[callbackIndex];
            callback?.Invoke(container, new object[0] );
        }

        private bool AreEqual(ulong? code1, Symbol symbol1, ulong? code2, Symbol symbol2)
        {
            if (code1 != null && code2 != null)
            {
                return code1.Value == code2.Value;
            }

            if (symbol1 != null && symbol2 != null)
            {
                return string.Equals((string)symbol1, (string)symbol2, StringComparison.Ordinal);
            }

            return false;
        }
    }

    private sealed class DescribedListType : DescribedCompoundType
    {
        public DescribedListType(
            AmqpSerializer serializer,
            Type type,
            SerializableType baseType,
            string descriptorName,
            ulong? descriptorCode,
            SerializableMember[] members,
            SerializableType[] knownTypes,
            MethodAccessor[] serializationCallbacks)
            : base(serializer, type, baseType, descriptorName, descriptorCode, members, knownTypes, serializationCallbacks)
        {
        }

        public override EncodingType Encoding
        {
            get
            {
                return EncodingType.List;
            }
        }

        protected override byte Code
        {
            get { return FormatCode.List32; }
        }

        protected override int WriteMemberValue(ByteBuffer buffer, string memberName, object memberValue, SerializableType effectiveType)
        {
            if (memberValue == null)
            {
                AmqpEncoder.WriteObject(buffer, null);
            }
            else
            {
                effectiveType.WriteObject(buffer, memberValue);
            }

            return 1;
        }

        protected override int ReadMemberValue(ByteBuffer buffer, SerializableMember serialiableMember, object container)
        {
            object value = serialiableMember.Type.ReadObject(buffer);
            serialiableMember.Accessor.Set(container, value);
            return 1;
        }
    }

    private sealed class DescribedMapType : DescribedCompoundType
    {
        private readonly Dictionary<string, SerializableMember> membersMap;

        public DescribedMapType(
            AmqpSerializer serializer,
            Type type,
            SerializableType baseType,
            string descriptorName,
            ulong? descriptorCode,
            SerializableMember[] members,
            SerializableType[] knownTypes,
            MethodAccessor[] serializationCallbacks)
            : base(serializer, type, baseType, descriptorName, descriptorCode, members, knownTypes, serializationCallbacks)
        {
            this.membersMap = new Dictionary<string, SerializableMember>();
            foreach (SerializableMember member in members)
            {
                this.membersMap.Add(member.Name, member);
            }
        }

        public override EncodingType Encoding
        {
            get
            {
                return EncodingType.Map;
            }
        }

        protected override byte Code
        {
            get { return FormatCode.Map32; }
        }

        protected override int WriteMemberValue(ByteBuffer buffer, string memberName, object memberValue, SerializableType effectiveType)
        {
            if (memberValue != null)
            {
                AmqpEncoder.WriteSymbol(buffer, (Symbol)memberName, true);
                effectiveType.WriteObject(buffer, memberValue);
                return 2;
            }

            return 0;
        }

        protected override int ReadMemberValue(ByteBuffer buffer, SerializableMember serialiableMember, object container)
        {
            string key = this.ReadKey(buffer);
            SerializableMember member = null;
            if (!this.membersMap.TryGetValue(key, out member))
            {
                throw new AmqpException(ErrorCode.DecodeError, "Unknown key name " + key);
            }

            object value = member.Type.ReadObject(buffer);
            member.Accessor.Set(container, value);
            return 2;
        }

        private string ReadKey(ByteBuffer buffer)
        {
            var formatCode = AmqpEncoder.ReadFormatCode(buffer);
            switch (formatCode)
            {
                case FormatCode.String32Utf8:
                case FormatCode.String8Utf8:
                    return AmqpEncoder.ReadString(buffer, formatCode);

                case FormatCode.Symbol8:
                case FormatCode.Symbol32:
                    return AmqpEncoder.ReadSymbol(buffer, formatCode);

                default:
                    throw new AmqpException(ErrorCode.DecodeError, "Format code " + formatCode + " not supported for map key");
            }
        }
    }

    private sealed class DescribedSimpleMapType : DescribedCompoundType
    {
        private readonly Dictionary<string, SerializableMember> membersMap;

        public DescribedSimpleMapType(
            AmqpSerializer serializer,
            Type type,
            SerializableType baseType,
            SerializableMember[] members,
            MethodAccessor[] serializationCallbacks)
            : base(serializer, type, baseType, null, null, members, null, serializationCallbacks)
        {
            this.membersMap = new Dictionary<string, SerializableMember>();
            foreach (SerializableMember member in members)
            {
                this.membersMap.Add(member.Name, member);
            }
        }

        public override EncodingType Encoding
        {
            get
            {
                return EncodingType.SimpleMap;
            }
        }

        protected override byte Code
        {
            get { return FormatCode.Map32; }
        }

        protected override bool WriteFormatCode(ByteBuffer buffer)
        {
            AmqpBitConverter.WriteUByte(buffer, FormatCode.Map32);
            return true;
        }

        protected override void Initialize(ByteBuffer buffer, byte formatCode, out int size, out int count, out int encodeWidth, out CollectionType effectiveType)
        {
            effectiveType = this;
            ReadSizeAndCount(buffer, formatCode, out size, out count, out encodeWidth);
        }

        protected override int WriteMemberValue(ByteBuffer buffer, string memberName, object memberValue, SerializableType effectiveType)
        {
            if (memberValue != null)
            {
                AmqpEncoder.WriteString(buffer, memberName, true);
                effectiveType.WriteObject(buffer, memberValue);
                return 2;
            }

            return 0;
        }

        protected override int ReadMemberValue(ByteBuffer buffer, SerializableMember serialiableMember, object container)
        {
            string key = AmqpEncoder.ReadString(buffer, AmqpEncoder.ReadFormatCode(buffer));
            SerializableMember member = null;
            if (!this.membersMap.TryGetValue(key, out member))
            {
                throw new AmqpException(ErrorCode.DecodeError, "Unknown key name " + key);
            }

            object value = member.Type.ReadObject(buffer);
            member.Accessor.Set(container, value);
            return 2;
        }
    }

    private sealed class DescribedSimpleListType : DescribedCompoundType
    {
        public DescribedSimpleListType(
            AmqpSerializer serializer,
            Type type,
            SerializableType baseType,
            SerializableMember[] members,
            MethodAccessor[] serializationCallbacks)
            : base(serializer, type, baseType, null, null, members, null, serializationCallbacks)
        {
        }

        public override EncodingType Encoding
        {
            get
            {
                return EncodingType.SimpleList;
            }
        }

        protected override byte Code
        {
            get { return FormatCode.List32; }
        }

        protected override bool WriteFormatCode(ByteBuffer buffer)
        {
            AmqpBitConverter.WriteUByte(buffer, FormatCode.List32);
            return true;
        }

        protected override void Initialize(ByteBuffer buffer, byte formatCode, out int size, out int count, out int encodeWidth, out CollectionType effectiveType)
        {
            effectiveType = this;
            ReadSizeAndCount(buffer, formatCode, out size, out count, out encodeWidth);
        }

        protected override int WriteMemberValue(ByteBuffer buffer, string memberName, object memberValue, SerializableType effectiveType)
        {
            if (memberValue != null)
            {
                effectiveType.WriteObject(buffer, memberValue);
            }
            else
            {
                AmqpEncoder.WriteObject(buffer, null);
            }

            return 1;
        }

        protected override int ReadMemberValue(ByteBuffer buffer, SerializableMember serialiableMember, object container)
        {
            object value = serialiableMember.Type.ReadObject(buffer);
            serialiableMember.Accessor.Set(container, value);
            return 1;
        }
    }
}
