using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Hasm.Test;

static class Program
{
    private const string TestConfigurationFile = "test-config.json";
    
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    private static int Main(string[] args)
    {

        Compiler compiler = new Compiler();
        Processor processor = new Processor(8, 8, 2);
        processor.PlugDevice(0, new TestDevice());

        TestConfiguration? testConfiguration = TestConfiguration.Load(TestConfigurationFile);
        if (testConfiguration?.TestDescriptors == null)
        {
            Console.Error.WriteLine($"Could not load {TestConfigurationFile}");
            return -1;
        }

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
            
            Hasm.Program? program = compiler.Compile(srcContent);
            if (compiler.LastError.Error == test.CompilerError)
            {
                ConsoleHelper.PrintPassedTest(Path.GetFileName(test.SourceFile), "Compile");
            }
            else if (compiler.LastError.Error != test.CompilerError)
            {
                ConsoleHelper.PrintFailedTest(Path.GetFileName(test.SourceFile), compiler.LastError, "Compile");
                failures++;
            }

            if (program == null || compiler.LastError.Error != Error.Success)
                continue;
            
            processor.Load(program);
            if (test.RuntimeError == Error.RequirementsNotMet && processor.LastError.Error != Error.RequirementsNotMet)
            {
                ConsoleHelper.PrintFailedTest(Path.GetFileName(test.SourceFile), processor.LastError, "Runtime");
                failures++;
                continue;
            }
            
            processor.Run();
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
        public TestDescriptor[]? TestDescriptors;

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
            public string SourceFile;
            [JsonConverter(typeof(StringEnumConverter))]public Error CompilerError;
            [JsonConverter(typeof(StringEnumConverter))]public Error RuntimeError;
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