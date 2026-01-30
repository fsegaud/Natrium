using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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
        
        Action<DebugData>? debugCallback = null;
        if (args.Contains("--debug"))
            debugCallback = ConsoleHelper.DebugCallback;
        
        bool showInfo = args.Contains("--info");
        bool noWatchdog = args.Contains("--no-watchdog");
        
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
            Hasm.Program? program = compiler.Compile(srcContent);
            if (compiler.LastError.Error == test.CompilerError)
            {
                if (showInfo && program != null)
                    ConsoleHelper.PrintProgramInfo(program);
                
                ConsoleHelper.PrintPassedTest(Path.GetFileName(test.SourceFile), "Compile");
            }
            else if (compiler.LastError.Error != test.CompilerError)
            {
                ConsoleHelper.PrintFailedTest(Path.GetFileName(test.SourceFile), compiler.LastError, "Compile");
                failures++;
            }

            if (program == null || compiler.LastError.Error != Error.Success)
                continue;
            
            // Load (and test serialization/deserialization).
            processor.Load(Hasm.Program.FromBase64(program.ToBase64()), debugCallback);
            if (test.RuntimeError == Error.RequirementsNotMet && processor.LastError.Error != Error.RequirementsNotMet)
            {
                ConsoleHelper.PrintFailedTest(Path.GetFileName(test.SourceFile), processor.LastError, "Runtime");
                failures++;
                continue;
            }
            
            // Run.
            while (!processor.IsFinished)
            {
                processor.Run(watchdog: noWatchdog ? null : 0x100000);
            }
            
            if (processor.LastError.Error != test.RuntimeError)
            {
                ConsoleHelper.PrintFailedTest(Path.GetFileName(test.SourceFile), processor.LastError,  "Runtime");
                failures++;
                continue;
            }
            
            ConsoleHelper.PrintPassedTest(Path.GetFileName(test.SourceFile), "Runtime");
        }
        
        if (failures > 0)
            Console.WriteLine($"{failures} tests failed.");
        else
            Console.WriteLine($"All tests passed.");

        return failures;
    }

    private class TestConfiguration
    {
#pragma warning disable CS0649 // Field is assigned through json.
        public TestDescriptor[]? TestDescriptors;
#pragma warning restore CS0649

        public static TestConfiguration? Load(string path)
        {
            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
            
            return JsonConvert.DeserializeObject<TestConfiguration>(json);
        }
        
        public class TestDescriptor
        {
#pragma warning disable CS0649 // Field is assigned through json.
            public required string SourceFile;
            [JsonConverter(typeof(StringEnumConverter))]public required Error CompilerError;
            [JsonConverter(typeof(StringEnumConverter))]public required Error RuntimeError;
#pragma warning restore CS0649
        }
    }

    private class TestDevice : IDevice
    {
        private double _value;
        
        public bool TryReadValue(int index, out double value)
        {
            value = _value;
            return index > 0;
        }

        public bool TryWriteValue(int index, double value)
        {
            _value = index * value;
            return index > 0;
        }
    }
}