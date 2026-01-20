namespace Hasm
{
    public struct Result
    {
        public readonly Error Error;
        public readonly string? RawInstruction;
        public readonly uint Line;

        internal static Result Success()
        {
            return new Result(Error.Success);
        }
        
        internal Result(Error error, Instruction instruction)
        {
            Error = error;
            RawInstruction = instruction.RawText;
            Line = instruction.Line;
        }
        
        internal Result(Error error, uint line = 0, string? rawInstruction = null)
        {
            Error = error;
            RawInstruction = rawInstruction;
            Line = line;
        }
    }
}