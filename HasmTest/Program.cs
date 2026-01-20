namespace HasmTest;

class Program
{
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
        
        Hasm.Processor processor = new Hasm.Processor(4, 8, 100);
        Hasm.Result result = processor.Run(program, DebugCallback, Hasm.DebugData.All);
        if (result.Error != Hasm.Error.Success)
        {
            Console.Error.WriteLine(
                $"Runtime Error: {result.Error} ({result.Error:D}) '{result.RawInstruction}' at line {result.Line}");
            Console.WriteLine(processor.DumpMemory());
        }
    }
    
    static void DebugCallback(string msg)
    {
        Console.WriteLine("[dbg] " + msg);
    }
}
