namespace Hasm.Test;

public static class ConsoleHelper
{
    public static void DebugCallback(Hasm.DebugData data)
    {
        Console.WriteLine($"[dbg]    ln: {data.Line:d4} > {data.RawInstruction}");
        Console.WriteLine($"[dbg]    ra: {data.ReturnAddress:d4}   registers: {string.Join(' ',  data.Registers)}");
        Console.WriteLine($"[dbg]    sp: {data.StackPointer:d4}       stack: {string.Join(' ', data.Stack)}");
#if HASM_FEATURE_MEMORY
        Console.WriteLine($"[dbg]                  memory: {string.Join(' ',  data.Memory)}");
        Console.WriteLine($"[dbg]               memblocks: {string.Join(' ',  data.MemoryBlocks)}");
#endif
        Console.WriteLine($"[dbg]-----------------------------------------------------------------------------------------");
    }

    public static void PrintProgramInfo(Hasm.Program program)
    {
        string b64 = program.ToBase64();
        Console.WriteLine($"[dbg]-----------------------------------------------------------------------------------------");
        Console.WriteLine($"length: {b64.Length}    req_registers: {program.RequiredRegisters}    " +
                          $"req_stack: {program.RequiredStack}    req_devices: {program.RequiredDevices}    " +
#if HASM_FEATURE_MEMORY
                          $"req_memory: {program.RequiredMemory}" +
#endif
                          $"\n{b64}");
        Console.WriteLine($"[dbg]-----------------------------------------------------------------------------------------");
    }

    public static void PrintPassedTest(string testName, string stage)
    {
        Console.WriteLine($"[PASSED] {stage} {testName}");
    }
    
    public static void PrintFailedTest(string testName, Result result, string stage)
    {
        Console.WriteLine($"[FAILED] {stage} {testName}. {result.Error} ({(int)result.Error}) at line {result.Line}: {result.RawInstruction}");
    }
}