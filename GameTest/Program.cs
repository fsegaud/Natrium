namespace GameTest;

public static class Program
{
    private const string SrcFile = "hasm-src/main.hasm";

    public static void Main(string[] args)
    {
        Action<Hasm.DebugData>? debugCallback = args.Contains("--debug") ? ConsoleHelper.DebugCallback : null;
        
        Hasm.Compiler compiler = new Hasm.Compiler();
        Hasm.Program? program = compiler.Compile(File.ReadAllText(SrcFile), srcPath: Path.GetDirectoryName(SrcFile) ?? string.Empty);
        if (program == null)
        {
            ConsoleHelper.PrintFailedTest(SrcFile, compiler.LastError, "Compiler");
            return;
        }
        
        Hasm.Processor processor = new Hasm.Processor(16, 16, 2);
        VirtualScreen? screen = processor.PlugDevice(0, new VirtualScreen(24, 8));
        VirtualKeyboard? keyboard = processor.PlugDevice(1, new VirtualKeyboard());
        processor.Load(program, debugCallback);

        while (!processor.IsFinished)
        {
            processor.Run();
            if (processor.IsFinished)
                break;
            
            for (var y = 0; y < screen?.Height; y++)
            {
                for (var x = 0; x < screen.Width; x++)
                    Console.Write(screen.Data[y * screen.Width + x]);
                Console.WriteLine();
            }

            keyboard?.KeyCode = (int)Console.ReadKey(true).Key;
            
            Console.Clear();
        }
        
        if (processor.HasError)
        {
            ConsoleHelper.PrintFailedTest(SrcFile, processor.LastError, "Runtime");
        }
    }
}
