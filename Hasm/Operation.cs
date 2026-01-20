namespace Hasm
{
    internal enum Operation
    {
        Nop = 0,
        Move,
        Add,
        Subtract,
        Multiply,
        Divide,
        SquareRoot,
        Push,
        Pop,
        Peek,
        Jump,
        JumpReturnAddress,
        
        Assert = 100,
        
        Ret = 1000,
    }
}