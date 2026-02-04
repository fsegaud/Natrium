namespace Natrium
{
    public enum Error
    {
        Success = 0,
        
        // Compiler.
        SyntaxError = 100,
        OperationNotSupported,
        LabelNotFound,
        FileNotFound,
        IoError,
        
        // Processor
        ProgramNotLoaded = 200,
        RequirementsNotMet,
        OperationNotImplemented,
        RegisterOutOfBound,
        DivisionByZero,
        NaN,
        BadArguments,
        StackOverflow,
        InvalidJump,
        DeviceOutOfBound,
        DeviceUnplugged,
        DeviceFailed,
#if NATRIUM_FEATURE_MEMORY
        OutOfMemory,
        MemoryViolation,
        NullPointer,
        DoubleFree,
#endif
        DiedInPain = 300,
        AssertFailed,
        WatchdogBark,
    }
}