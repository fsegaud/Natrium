using System.Diagnostics.CodeAnalysis;
namespace Hasm.Test;

// TODO: /0 sqrt<0 errors req dev

static class Program
{
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    private static void Main(string[] args)
    {
        Compiler compiler = new Compiler();
        Processor processor = new Processor(8, 8, 2);
        
        var srcFiles = Directory.GetFiles("../../../hasm-src/testing", "*.hasm");
        foreach (var srcFile in srcFiles)
        {
            string srcContent;
            try
            {
                srcContent = File.ReadAllText(srcFile);
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return;
            }
            
            Hasm.Program? program = compiler.Compile(srcContent);
            if (program == null || compiler.LastError.Error != Error.Success)
            {
                ConsoleHelper.PrintFailedTest(Path.GetFileName(srcFile), compiler.LastError);
                break;
            }
            
            processor.Load(program, ConsoleHelper.DebugCallback);
            if (!processor.Run() || processor.LastError.Error != Error.Success)
            {
                ConsoleHelper.PrintFailedTest(Path.GetFileName(srcFile), processor.LastError);
                break;
            }
            
            ConsoleHelper.PrintPassedTest(Path.GetFileName(srcFile));
        }
    }
}