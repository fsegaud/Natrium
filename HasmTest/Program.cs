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
        Hasm.Program? program = compiler.Compile(File.ReadAllText(sourceFile), DebugCallback, Hasm.DebugData.Binary);
        if (program == null)
        {
            Hasm.Result error = compiler.LastError;
            Console.Error.WriteLine($"Compilation Error: {error.Error} ({error.Error:D}) '{error.RawInstruction}' at line {error.Line}");
            return;
        }
        
        Hasm.Processor processor = new Hasm.Processor(numDevices: 1);

        NumberScreen screen = new NumberScreen();
        processor.PlugDevice(0, screen);
        
        if (!processor.Run(program, DebugCallback, Hasm.DebugData.All))
        {
            Hasm.Result error = processor.LastError;
            Console.Error.WriteLine(
                $"Runtime Error: {error.Error} ({error.Error:D}) '{error.RawInstruction}' at line {error.Line}");
            Console.WriteLine(processor.DumpMemory());
            return;
        }
        
        Console.WriteLine(screen.Display);
    }
    
    static void DebugCallback(string msg)
    {
        Console.WriteLine("[dbg] " + msg);
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
