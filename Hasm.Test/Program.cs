using System.Diagnostics.CodeAnalysis;

namespace Hasm.Test;

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
        
        BuildTarget buildTarget = args.Contains("--target-debug") ? BuildTarget.Debug : BuildTarget.Release;
        Action<DebugData>? debugCallback = args.Contains("--debug") ? ConsoleHelper.DebugCallback : null;
        bool showInfo = args.Contains("--info");
        int? watchdog = args.Contains("--no-watchdog") ? null : 0x1000;
        
        TestConfiguration? testConfiguration = TestConfiguration.Load(testConfigurationFile);
        if (testConfiguration?.TestDescriptors == null)
        {
            Console.Error.WriteLine($"Could not load {testConfigurationFile}");
            return -1;
        }
        
        Compiler compiler = new Compiler();
        Processor processor = new Processor(8, 8, 2);
        processor.PlugDevice(0, new TestDevice());

        int failures = 0;
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
            Hasm.Program? program = compiler.Compile(srcContent, buildTarget);
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
            processor.Load(Hasm.Program.FromBase64(program.ToBase64()), debugCallback, watchdog);
            if (test.RuntimeError == Error.RequirementsNotMet && processor.LastError.Error != Error.RequirementsNotMet)
            {
                ConsoleHelper.PrintFailedTest(test.SourceFile, processor.LastError, "Runtime");
                failures++;
                continue;
            }
            
            // Run.
            while (!processor.IsFinished)
            {
                processor.Run(16);
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