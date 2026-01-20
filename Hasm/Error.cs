namespace Hasm
{
    public enum Error
    {
        Success = 0,
        
        // Compiler.
        SyntaxError = 100,
        OperationNotSupported,
        
        // Processor
        RequirementsNotMet = 200,
        OperationNotImplemented,
        RegistryOutOfBound,
        DivisionByZero,
        NaN,
        StackOverflow,
        InvalidJump,
        LabelNotFound,
        
        AssertFailed = 900,
    }
}