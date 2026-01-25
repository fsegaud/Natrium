using System.Diagnostics.CodeAnalysis;
namespace HasmTest;

class Program
{
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    static void Main(string[] args)
    {
        string sourceFile = "../../../hasm_src/test.hasm";
        
        Hasm.Compiler compiler = new Hasm.Compiler();
        Hasm.Program? program = compiler.Compile(File.ReadAllText(sourceFile));
        if (program == null)
        {
            Hasm.Result error = compiler.LastError;
            Console.Error.WriteLine($"Compilation Error: {error.Error} ({error.Error:D}) '{error.RawInstruction}' at line {error.Line}");
            return;
        }

        PrintProgramInfo(program);
        
        Hasm.Processor processor = new Hasm.Processor(numDevices: 1);
        processor.PlugDevice(0, new Hasm.Devices.Eeprom(32));
        processor.Load(program, DebugCallback);

        while (!processor.IsFinished)
        {
            processor.Run();
        }
        
        if (processor.HasError)
        {
            Hasm.Result error = processor.LastError;
            Console.Error.WriteLine(
                $"Runtime Error: {error.Error} ({error.Error:D}) '{error.RawInstruction}' at line {error.Line}");
        }
    }
    
    static void DebugCallback(Hasm.DebugData data)
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

    static void PrintProgramInfo(Hasm.Program program)
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
}