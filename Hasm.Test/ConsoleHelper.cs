namespace Hasm.Test;

public static class ConsoleHelper
{
    private static DebugData _prevData; 
    
    public static void DebugCallback(DebugData data)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("-----------------------------------------------------------------------------------------");
        Console.ResetColor();
        
        Console.Write("    ");
        Console.BackgroundColor = ConsoleColor.Gray;
        Console.ForegroundColor = ConsoleColor.Black;
        Console.Write($" {data.PreprocessedInstruction} ");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(" [ ");
        Console.ResetColor();
        Console.Write(data.RawInstruction);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(" | ");
        Console.ResetColor();
        Console.Write(data.EncodedInstruction);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(" ]");
        Console.ResetColor();
        Console.WriteLine();
        
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("    ln: ");
        Console.ResetColor();
        Console.Write($"{data.Line:d4}");
        Console.Write("    ");
        
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("    frame: ");
        Console.ResetColor();
        Console.Write($"{data.Frame}");
        Console.WriteLine();
        
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("    ra: ");
        Console.ResetColor();
        if (_prevData.ReturnAddress != data.ReturnAddress)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write($"{data.ReturnAddress:d4}");
            Console.ResetColor();
        }
        else
        {
            Console.Write($"{data.ReturnAddress:d4}");
        }
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("    registers: ");
        Console.ResetColor();
        for (var i = 0; i < data.Registers.Length; i++)
        {
            if (_prevData.Registers != null &&_prevData.Registers.Length == data.Registers.Length && 
                Math.Abs(_prevData.Registers[i] - data.Registers[i]) > double.Epsilon)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write($"{data.Registers[i]}");
                Console.ResetColor();
                Console.Write(" ");
            }
            else
            {
                Console.Write($"{data.Registers[i]} ");
            }
        }
        
        Console.WriteLine();
        
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("    sp: ");
        Console.ResetColor();
        if (_prevData.StackPointer != data.StackPointer)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write($"{data.StackPointer:d4}   ");
            Console.ResetColor();
        }
        else
        {
            Console.Write($"{data.StackPointer:d4}   ");
        }
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("     stack: ");
        Console.ResetColor();
        for (var i = 0; i < data.Stack.Length; i++)
        {
            if (_prevData.Stack != null &&_prevData.Stack.Length == data.Stack.Length && 
                Math.Abs(_prevData.Stack[i] - data.Stack[i]) > double.Epsilon)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write($"{data.Stack[i]}");
                Console.ResetColor();
                Console.Write(" ");
            }
            else
            {
                Console.Write($"{data.Stack[i]}");
                Console.Write(" ");
            }
        }
        
        Console.WriteLine();
        
#if HASM_FEATURE_MEMORY
        Console.WriteLine($"[dbg]                  memory: {string.Join(' ',  data.Memory)}");
        Console.WriteLine($"[dbg]               memblocks: {string.Join(' ',  data.MemoryBlocks)}");
#endif

        _prevData = data;
    }

    public static void PrintProgramInfo(Hasm.Program program)
    {
        string b64 = program.ToBase64();
        
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("-----------------------------------------------------------------------------------------");
        Console.ResetColor();
        
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("    length: ");
        Console.ResetColor();
        Console.Write($"{b64.Length}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("    req_registers: ");
        Console.ResetColor();
        Console.Write($"{program.RequiredRegisters}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("    req_stack: ");
        Console.ResetColor();
        Console.Write($"{program.RequiredStack}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("    req_devices: ");
        Console.ResetColor();
        Console.Write($"{program.RequiredDevices}");
#if HASM_FEATURE_MEMORY
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("    req_memory: ");
        Console.ResetColor();
        Console.Write($"{program.RequiredMemory}");
#endif
        Console.WriteLine();
        
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("    base64: ");
        Console.ResetColor();
        Console.WriteLine(b64);
    }

    public static void PrintPassedTest(string testName, string stage)
    {
        Console.Write("[");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("PASSED");
        Console.ResetColor();
        Console.Write("]");
        Console.WriteLine($" {stage} {testName}");
    }
    
    public static void PrintFailedTest(string testName, Result result, string stage)
    {
        Console.Write("[");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("FAILED");
        Console.ResetColor();
        Console.Write("]");
        Console.WriteLine($" {stage} {testName} -> {result.Error} ({(int)result.Error}) at line {result.Line}: {result.RawInstruction}");
    }
}