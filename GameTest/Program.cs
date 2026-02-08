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
        processor.DebugCallback = debugCallback;
        processor.Load(program);

        while (!processor.IsFinished)
        {
            processor.Run();
            if (processor.IsFinished)
                break;

            ColorSet colorSet = default;
            for (var y = 0; y < screen?.Height; y++)
            {
                for (var x = 0; x < screen.Width; x++)
                {
                    int index = y * screen.Width + x;
                    if (screen.Color[index] != 0)
                    {
                        GetColor(screen.Color[index], ref colorSet);
                        Console.BackgroundColor = colorSet.Background;
                        Console.ForegroundColor = colorSet.Foreground;
                        Console.Write(screen.Data[index]);
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.Write(screen.Data[index]);
                    }
                }

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

    private static void GetColor(byte consoleColorByte, ref ColorSet colorSet)
    {
        colorSet.Background = (consoleColorByte >> 4) switch
        {
            0x0 => ConsoleColor.Black,
            0x1 => ConsoleColor.Red,
            0x2 => ConsoleColor.Green,
            0x3 => ConsoleColor.Blue,
            0x4 => ConsoleColor.Cyan,
            0x5 => ConsoleColor.Magenta,
            0x6 => ConsoleColor.Yellow,
            0xa => ConsoleColor.Black,
            0xb => ConsoleColor.White,
            0xc => ConsoleColor.Gray,
            0xd => ConsoleColor.DarkGray,
            _ => throw new ArgumentOutOfRangeException(nameof(consoleColorByte), consoleColorByte, null)
        };
        
        colorSet.Foreground = (consoleColorByte & 0x0f) switch
        {
            0x0 => ConsoleColor.White,
            0x1 => ConsoleColor.Red,
            0x2 => ConsoleColor.Green,
            0x3 => ConsoleColor.Blue,
            0x4 => ConsoleColor.Cyan,
            0x5 => ConsoleColor.Magenta,
            0x6 => ConsoleColor.Yellow,
            0xa => ConsoleColor.Black,
            0xb => ConsoleColor.White,
            0xc => ConsoleColor.Gray,
            0xd => ConsoleColor.DarkGray,
            _ => throw new ArgumentOutOfRangeException(nameof(consoleColorByte), consoleColorByte, null)
        };
    }

    private struct ColorSet
    {
        public ConsoleColor Background;
        public ConsoleColor Foreground;
    }
}
