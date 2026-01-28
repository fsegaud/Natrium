namespace Hasm
{
    [ProtoBuf.ProtoContract]
    internal struct Instruction
    {
        internal enum OperandType
        {
            Literal,
            HexLiteral,
            UserRegister,
            StackPointer,
            ReturnAddress,
            DeviceRegister,
        }
        
        [ProtoBuf.ProtoMember(1)] internal Operation Operation;

        [ProtoBuf.ProtoMember(2)] internal OperandType DestinationRegistryType;
        [ProtoBuf.ProtoMember(3)] internal uint Destination;
        
        [ProtoBuf.ProtoMember(4)] internal OperandType LeftOperandType;
        [ProtoBuf.ProtoMember(5)] internal double LeftOperandValue;
        
        [ProtoBuf.ProtoMember(6)] internal OperandType RightOperandType;
        [ProtoBuf.ProtoMember(7)] internal double RightOperandValue;

        [ProtoBuf.ProtoMember(8)] internal uint Line;
        [ProtoBuf.ProtoMember(9)] internal string RawText;

        public override string ToString()
        {
            return $"{Operation} {DestinationRegistryType} {Destination} {LeftOperandType} {LeftOperandValue} " +
                   $"{RightOperandType} {RightOperandValue}";
        }
    }
}