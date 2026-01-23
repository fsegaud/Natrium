namespace Hasm
{
    internal enum Operation
    {
        Nop = 0,
        Move,
        Add,
        Subtract,
        Increment,
        Decrement,
        Multiply,
        Divide,
        SquareRoot,
        Push,
        Pop,
        Peek,
        Jump,
        JumpReturnAddress,
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LesserThan,
        LesserThanOrEqual,
        BranchEqual,
        BranchEqualReturnAddress,
        BranchNotEqual,
        BranchNotEqualReturnAddress,
        BranchGreaterThan,
        BranchGreaterThanReturnAddress,
        BranchGreaterThanOrEqual,
        BranchGreaterThanOrEqualReturnAddress,
        BranchLesserThan,
        BranchLesserThanReturnAddress,
        BranchLesserThanOrEqual,
        BranchLesserThanOrEqualReturnAddress,
#if HASM_FEATURE_MEMORY
        AllocateMemory,
        FreeMemory,
#endif
        
        Assert = 100,
        
        Ret = 1000,
    }
}