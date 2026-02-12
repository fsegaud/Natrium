namespace Natrium
{
    public enum Error
    {
        Success = 0,
        
        // Compiler.
        SyntaxError = 100,
        OperationNotSupported,
        LabelNotFound,
        NoInclusionResolverProvided,
        FileNotFound,
        
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
        UnallocatedMemory,
        NullPointer,
        DoubleFree,
#endif
        DiedInPain = 300,
        AssertFailed,
        WatchdogBark,
    }
}