namespace GameTest;

public static class Program
{
    private const string SrcFile = "src/main.na";

    public static void Main(string[] args)
    {
        Action<Natrium.DebugData>? debugCallback = args.Contains("--trace") || args.Contains("-t") ? ConsoleHelper.DebugCallback : null;
        
        Natrium.Compiler compiler = new Natrium.Compiler();
        compiler.InclusionResolver = new Natrium.DirectoryInclusionResolver("src");
        Natrium.Program? program = compiler.Compile(File.ReadAllText(SrcFile));
        if (program == null)
        {
            ConsoleHelper.PrintFailedTest(SrcFile, compiler.LastError, "Compiler");
            return;
        }
        
        Natrium.Processor processor = new Natrium.Processor(16, 16, 2);
        Natrium.Devices.Screen? screen = processor.PlugDevice(0, new Natrium.Devices.Screen(24, 8));
        Natrium.Devices.Keyboard? keyboard = processor.PlugDevice(1, new Natrium.Devices.Keyboard());
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
