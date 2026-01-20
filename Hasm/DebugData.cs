namespace Hasm
{
    [System.Flags]
    public enum DebugData
    {
        None                = 0,
        Binary              = 1 << 0,
        RawInstruction      = 1 << 1,
        CompiledInstruction = 1 << 2,
        Memory              = 1 << 3,
        Separator           = 1 << 30,
        All                 = ~0
    }
}