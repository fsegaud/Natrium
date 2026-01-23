namespace Hasm
{
    public struct DebugData
    {
        public uint Line;
        public string RawInstruction;
        public double[] Registers;
        public double[] Stack;
        public uint StackPointer;
        public uint ReturnAddress;
#if HASM_FEATURE_MEMORY 
        public double[] Memory;
        public uint[] MemoryBlocks;
#endif
    }
}