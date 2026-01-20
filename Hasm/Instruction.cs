namespace Hasm
{
    [ProtoBuf.ProtoContract]
    internal struct Instruction
    {
        internal enum OperandType
        {
            Literal,
            UserRegistry,
            StackPointer,
            ReturnAddress
        }
        
        [ProtoBuf.ProtoMember(1)]
        internal Operation Operation;

        [ProtoBuf.ProtoMember(2)]
        internal OperandType DestinationRegistryType;
        [ProtoBuf.ProtoMember(3)]
        internal uint DestinationRegistry;
        
        [ProtoBuf.ProtoMember(4)]
        internal OperandType LeftOperandType;
        [ProtoBuf.ProtoMember(5)]
        internal float LeftOperandValue;
        
        [ProtoBuf.ProtoMember(6)]
        internal OperandType RightOperandType;
        [ProtoBuf.ProtoMember(7)]
        internal float RightOperandValue;

        [ProtoBuf.ProtoMember(8)]
        internal uint Line;
        [ProtoBuf.ProtoMember(9)]
        internal string RawText;

        public override string ToString()
        {
            return $"{Operation} {DestinationRegistryType} {DestinationRegistry} {LeftOperandType} {LeftOperandValue} " +
                   $"{RightOperandType} {RightOperandValue}";
        }
    }
}