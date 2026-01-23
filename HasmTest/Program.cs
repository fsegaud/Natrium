using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace HasmTest;

class Program
{
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    static void Main(string[] args)
    {
        string sourceFile = "../../../test.hasm";
        
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
        NumberScreen screen = new NumberScreen();
        processor.PlugDevice(0, screen);
        
        if (!processor.Run(program, DebugCallback))
        {
            Hasm.Result error = processor.LastError;
            Console.Error.WriteLine( $"Runtime Error: {error.Error} ({error.Error:D}) '{error.RawInstruction}' at line {error.Line}");
            return;
        }
        
        Console.WriteLine(screen.Display);
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
        Console.WriteLine($"[dbg]-----------------------------------------------------------------------------------------");
        Console.WriteLine(program.ToBase64());
        Console.WriteLine($"[dbg]-----------------------------------------------------------------------------------------");
    }
}

public class NumberScreen : Hasm.IDevice
{
    public string Display { get; private set; } = string.Empty;
    
    public bool TryReadValue(int index, [UnscopedRef] out double value)
    {
        value = 0d;
        return false;
    }

    public bool TryWriteValue(int index, double value)
    {
        if (index == 0)
        {
            Display = value.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }
}
