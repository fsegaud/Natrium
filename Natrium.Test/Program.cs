using System.Diagnostics.CodeAnalysis;

namespace Natrium.Test;

static class Program
{
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    private static int Main(string[] args)
    {
        // Parse args.
        string? testConfigurationFile = args.Length > 0 ? args[0] : null;
        if (string.IsNullOrEmpty(testConfigurationFile))
        {
            Console.Error.WriteLine("No test configuration file specified.");
            return -1;
        }
        
        BuildTarget buildTarget = args.Contains("--release") || args.Contains("-r") ? BuildTarget.Release : BuildTarget.Debug;
        Action<DebugData>? debugCallback = args.Contains("--trace") || args.Contains("-t") ? ConsoleHelper.DebugCallback : null;
        bool showInfo = args.Contains("--show-info") || args.Contains("-i");
        int? watchdog = args.Contains("--no-watchdog") ? null : 0x1000;
        
        TestConfiguration? testConfiguration = TestConfiguration.Load(testConfigurationFile);
        if (testConfiguration?.TestDescriptors == null)
        {
            Console.Error.WriteLine($"Could not load {testConfigurationFile}");
            return -1;
        }
        
        Compiler compiler = new Compiler();
        compiler.InclusionResolver = new DirectoryInclusionResolver("src");
        Processor processor = new Processor(8, 8, 8,2);
        processor.DebugCallback = debugCallback;
        processor.PlugDevice(0, new TestDevice());

        int failures = 0;
        bool interactive = false;
        foreach (var test in testConfiguration.TestDescriptors)
        {
            string srcContent;
            try
            {
                srcContent = File.ReadAllText(test.SourceFile);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                continue;
            }
            
            // Compile.
            Natrium.Program? program = compiler.Compile(srcContent, buildTarget);
            if (compiler.LastError.Error == test.CompilerError)
            {
                if (showInfo && program != null)
                    ConsoleHelper.PrintProgramInfo(program);
                
                ConsoleHelper.PrintPassedTest(test.SourceFile, "Compile");
            }
            else if (compiler.LastError.Error != test.CompilerError)
            {
                ConsoleHelper.PrintFailedTest(test.SourceFile, compiler.LastError, "Compile");
                failures++;
            }

            if (program == null || compiler.LastError.Error != Error.Success)
                continue;
            
            // Load (and test serialization/deserialization).
            processor.Load(Natrium.Program.FromBase64(program.ToBase64()), watchdog);
            if (test.RuntimeError == Error.RequirementsNotMet && processor.LastError.Error != Error.RequirementsNotMet)
            {
                ConsoleHelper.PrintFailedTest(test.SourceFile, processor.LastError, "Runtime");
                failures++;
                continue;
            }
            
            // Run.
            while (!processor.IsFinished)
            {
                if (interactive)
                {
                    Console.WriteLine("Press any key to continue, R to resume.");
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.R)
                    {
                        interactive = false;
                        processor.DebugCallback = debugCallback;
                    }
                    
                    processor.Run(1);
                }
                else
                {
                    processor.Run(64);
                }

                if (processor.BreakpointReached)
                {
                    interactive = true;
                    processor.DebugCallback = ConsoleHelper.DebugCallback;
                    Console.WriteLine("Breakpoint reached.");
                }
            }
            
            if (processor.LastError.Error != test.RuntimeError)
            {
                ConsoleHelper.PrintFailedTest(test.SourceFile, processor.LastError,  "Runtime");
                failures++;
                continue;
            }
            
            ConsoleHelper.PrintPassedTest(test.SourceFile, "Runtime");
        }
        
        if (failures > 0)
            Console.WriteLine($"{failures} tests failed.");
        else
            Console.WriteLine($"All tests passed.");

        return failures;
    }
}