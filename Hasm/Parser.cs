using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;

// TODO: Jumps

namespace Hasm
{
    public enum Error
    {
        Success = 0,
        
        // Compiler.
        SyntaxError = 100,
        OperationNotSupported,
        
        // Processor
        OperationNotImplemented = 200,
        RegistryOutOfBound,
        DivisionByZero,
        NaN,
        StackOverflow,
        InvalidJump,
        
        AssertFailed = 900,
    }

    [Flags]
    public enum DebugData
    {
        None                = 0,
        RawInstruction      = 1 << 0,
        CompiledInstruction = 1 << 1,
        Memory              = 1 << 2,
        Separator           = 1 << 30,
        All                 = ~0
    }

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
    
    public class Processor
    {
        private readonly int _frequencyHz;
        private readonly float[] _registries;
        private readonly float[] _stack;

        private uint _stackPointer;
        private uint _returnAddress;

        public Processor(int numRegistries = 8, int stackLength = 32, int frequencyHz = 0 /* not simulated */)
        {
            _registries = new float[numRegistries];
            _stack = new float[stackLength];
            _frequencyHz = frequencyHz;
        }

        private void SetRegistry(ref Instruction instruction, float value)
        {
            switch (instruction.DestinationRegistryType)
            {
                case OperandType.UserRegistry:
                    _registries[instruction.DestinationRegistry] = value;
                    break;
                
                case OperandType.StackPointer:
                    _stackPointer = (uint)value;
                    break;
                
                case OperandType.ReturnAddress:
                    _returnAddress = (uint)value;
                    break;
                
                case OperandType.Literal:
                    throw new InvalidOperationException();
                
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool TryGetOperandValue(OperandType type, ref float value)
        {
            if (type == OperandType.UserRegistry && (value < 0 || value >= _registries.Length))
                return false;
            
            switch (type)
            {
                case OperandType.Literal: break;
                case OperandType.UserRegistry: value = _registries[(int)value]; break;
                case OperandType.StackPointer: value = _stackPointer; break;
                case OperandType.ReturnAddress: value = _returnAddress; break;
                default: throw new ArgumentOutOfRangeException();
            }

            return true;
        }
        
        public Result Run(Program program, Action<string>? debugCallback = null, DebugData debugData = DebugData.None)
        {
            for (int index = 0; index < program.Instructions.Length; index++)
            {
                Instruction instruction = program.Instructions[index];
                
                float leftOperandValue = instruction.LeftOperandValue;
                float rightOperandValue = instruction.RightOperandValue;
                
                if (!TryGetOperandValue(instruction.LeftOperandType, ref leftOperandValue) ||
                    !TryGetOperandValue(instruction.RightOperandType, ref rightOperandValue) ||
                    instruction.DestinationRegistry >= _registries.Length)
                {
                    return new Result(Error.RegistryOutOfBound, instruction);
                }

                switch (instruction.Operation)
                {
                    case Operation.Nop:
                        break;
                    
                    case Operation.Move:
                        SetRegistry(ref instruction, leftOperandValue);
                        break;
                    
                    case Operation.Add:
                        SetRegistry(ref instruction, leftOperandValue + rightOperandValue);
                        break;
                    
                    case Operation.Subtract:
                        SetRegistry(ref instruction, leftOperandValue - rightOperandValue);
                        break;
                    
                    case Operation.Multiply:
                        SetRegistry(ref instruction, leftOperandValue * rightOperandValue);
                        break;
                    
                    case Operation.Divide:
                        if (rightOperandValue == 0)
                            return new Result(Error.DivisionByZero, instruction);
                        SetRegistry(ref instruction, leftOperandValue / rightOperandValue);
                        break;
                    
                    case Operation.SquareRoot:
                        if (leftOperandValue < 0 )
                            return new Result(Error.NaN, instruction);
                        SetRegistry(ref instruction, (float)Math.Sqrt(leftOperandValue));
                        break;
                    
                    case Operation.Push:
                        if (_stackPointer >= _stack.Length)
                            return new Result(Error.StackOverflow, instruction);
                        _stack[_stackPointer++] = _registries[instruction.DestinationRegistry];
                        break;
                    
                    case Operation.Pop:
                        if (_stackPointer == 0)
                            return new Result(Error.StackOverflow, instruction);
                        SetRegistry(ref instruction, _stack[--_stackPointer]);
                        break;
                    
                    case Operation.Peek:
                        if (_stackPointer == 0)
                            return new Result(Error.StackOverflow, instruction);
                        SetRegistry(ref instruction, _stack[_stackPointer - 1]);
                        break;
                    
                    case Operation.Assert:
                        if (Math.Abs(_registries[instruction.DestinationRegistry] - leftOperandValue) > float.Epsilon)
                            return new Result(Error.AssertFailed, instruction);
                        break;

                    case Operation.Jump:
                    {
                        var foundDestination = -1;
                        for (var searchIndex = 0; searchIndex < program.Instructions.Length; searchIndex++)
                        {
                            if (program.Instructions[searchIndex].Line == (int)leftOperandValue)
                            {
                                foundDestination = searchIndex;
                                break;
                            }
                        }

                        if (foundDestination < 0)
                            return new Result(Error.InvalidJump, instruction);
                        index = foundDestination - 1;
                        break;
                    }

                    case Operation.JumpReturnAddress:
                    {
                        var foundDestination = -1;
                        for (var searchIndex = 0; searchIndex < program.Instructions.Length; searchIndex++)
                        {
                            if (program.Instructions[searchIndex].Line == (int)leftOperandValue)
                            {
                                foundDestination = searchIndex;
                                break;
                            }
                        }

                        if (foundDestination < 0)
                            return new Result(Error.InvalidJump, instruction);
                        index = foundDestination - 1;

                        _returnAddress = instruction.Line + 1;
                        
                        break;
                    }

                    default:
                        return new Result(Error.OperationNotImplemented, instruction);
                }
                
                if ((debugData & DebugData.RawInstruction) > 0)
                    debugCallback?.Invoke($"processor > Raw[{instruction.Line:d4}]: " + instruction.RawText);
                
                if ((debugData & DebugData.CompiledInstruction) > 0)
                    debugCallback?.Invoke($"processor > Cmp[{instruction.Line:d4}]: " + instruction);
                
                if ((debugData & DebugData.Memory) > 0)
                    debugCallback?.Invoke($"processor > Mem[{instruction.Line:d4}]: " + DumpMemory());
                
                if ((debugData & DebugData.Separator) > 0)
                    debugCallback?.Invoke("-------------------------------------------------------------------------" +
                                          "-----------------------------------------------");
                
                if (_frequencyHz > 0)
                    Thread.Sleep(1000 / _frequencyHz);
            }

            return Result.Success();
        }

        public float ReadRegistry(int registry)
        {
            return registry >= 0 && registry < _registries.Length ? _registries[registry] : float.NaN;
        }

        public string DumpMemory()
        {
            return $"Sp: {_stackPointer:d4} Ra: {_returnAddress:d4} " + 
                   $"Registries: {string.Join(" ", _registries)} " +
                   $"Stack: {string.Join(" ", _stack)} ";
        }
    }

    public class Compiler
    {
        public Result Compile(string input, ref Program program, Action<string>? debugCallback = null, DebugData debugData = DebugData.None)
        {
            List<Instruction> instructions = new List<Instruction>();
            
            string[] lines = input.Split('\n');
            for (var index = 0u; index < lines.Length; index++)
            {
                // Pre-parse.
                lines[index] =  lines[index].Trim();
                
                if (string.IsNullOrEmpty(lines[index]))
                    continue;
                
                //  Empty.

                Regex regex = new Regex(@"^[\s\t]*$");
                Match match = regex.Match(lines[index]);

                if (match.Success)
                    continue;
                
                // Comments.

                regex = new Regex(@".*(?<com>[;#].*)"); // TODO: One Regex object per expression.
                match = regex.Match(lines[index]);

                if (match.Success)
                {
                    lines[index] = lines[index].Replace(match.Groups["com"].Value, string.Empty).TrimEnd();

                    if (string.IsNullOrEmpty(lines[index]))
                        continue;
                }
                
                // Self operations.

                regex = new Regex(@"^(?<opt>nop)$");
                match = regex.Match(lines[index]);

                if (match.Success)
                {
                    string opt = match.Groups["opt"].Value;

                    Instruction instruction = default;
                    instruction.RawText = lines[index];
                    instruction.Line = index + 1;
                    
                    switch (opt)
                    {
                        case "nop": instruction.Operation = Operation.Nop; break;
                        default: return new Result(Error.OperationNotSupported, instruction);
                    }

                    instructions.Add(instruction);
                    
                    if ((debugData & DebugData.CompiledInstruction) > 0)
                        debugCallback?.Invoke("compiler > " + instruction);

                    continue;
                }
                
                // Stuff...
                
                // TODO: replace opl with new opd (registry type).
                regex = new Regex(@"^(?<opt>j|jra)\s+(?<opl>r\d+\b|ra|sp|[1-9]\d*\b)$");
                match = regex.Match(lines[index]);
                
                if (match.Success)
                {
                    string opt = match.Groups["opt"].Value;
                    string opl = match.Groups["opl"].Value;
                    
                    Instruction instruction = default;
                    instruction.RawText = lines[index];
                    instruction.Line = index + 1;
                    
                    switch (opt)
                    {
                        case "j": instruction.Operation = Operation.Jump; break;
                        case "jra": instruction.Operation = Operation.JumpReturnAddress; break;
                        default: return new Result(Error.OperationNotSupported, instruction);
                    }

                    if (opl == "ra")
                    {
                        instruction.LeftOperandType = OperandType.ReturnAddress;
                    }
                    else if (opl == "sp")
                    {
                        instruction.LeftOperandType = OperandType.StackPointer;
                    }
                    else if (opl[0] == 'r')
                    {
                        instruction.LeftOperandType = OperandType.UserRegistry;
                        instruction.LeftOperandValue = int.Parse(opl.Substring(1));
                    }
                    else
                    {
                        instruction.LeftOperandType = OperandType.Literal;
                        instruction.LeftOperandValue = float.Parse(opl, CultureInfo.InvariantCulture);
                    }
                    
                    instructions.Add(instruction);
                    
                    if ((debugData & DebugData.CompiledInstruction) > 0)
                        debugCallback?.Invoke("compiler > " + instruction);

                    continue;
                }
                
                // Registry operations.
                
                regex = new Regex(@"^(?<opt>push|pop|peek)\s+(?<opd>r\d+\b|ra|sp)$");
                match = regex.Match(lines[index]);

                if (match.Success)
                {
                    string opt = match.Groups["opt"].Value;
                    string opd = match.Groups["opd"].Value;
                    
                    Instruction instruction = default;
                    instruction.RawText = lines[index];
                    instruction.Line = index + 1;
                    
                    switch (opt)
                    {
                        case "push": instruction.Operation = Operation.Push; break;
                        case "pop": instruction.Operation = Operation.Pop; break;
                        case "peek": instruction.Operation = Operation.Peek; break;
                        default: return new Result(Error.OperationNotSupported, instruction);
                    }
                    
                    if (opd == "ra")
                    {
                        instruction.DestinationRegistryType = OperandType.ReturnAddress;
                    }
                    else if (opd == "sp")
                    {
                        instruction.DestinationRegistryType = OperandType.StackPointer;
                    }
                    else
                    {
                        instruction.DestinationRegistryType = OperandType.UserRegistry;
                        instruction.DestinationRegistry = uint.Parse(opd.Substring(1));
                    }
                    
                    instructions.Add(instruction);
                    
                    if ((debugData & DebugData.CompiledInstruction) > 0)
                        debugCallback?.Invoke("compiler > " + instruction);

                    continue;
                }

                // Unary operations.

                // regex = new Regex(@"(?<opt>mov|sqrt)\s+(?<opd>r\d+)\s+(?<opl>r?\d+[.]?\d*)");
                regex = new Regex(@"^(?<opt>mov|sqrt|assert)\s+(?<opd>r\d+\b|ra|sp)\s+(?<opl>-?\d+[.]?\d*|r\d+\b|ra|sp)$");
                match = regex.Match(lines[index]);

                if (match.Success)
                {
                    string opt = match.Groups["opt"].Value;
                    string opd = match.Groups["opd"].Value;
                    string opl = match.Groups["opl"].Value;
                    
                    Instruction instruction = default;
                    instruction.RawText = lines[index];
                    instruction.Line = index + 1;
                    
                    switch (opt)
                    {
                        case "mov": instruction.Operation = Operation.Move; break;
                        case "sqrt": instruction.Operation = Operation.SquareRoot; break;
                        case "assert": instruction.Operation = Operation.Assert; break;
                        default: return new Result(Error.OperationNotSupported, instruction);
                    }
                    
                    if (opd == "ra")
                    {
                        instruction.DestinationRegistryType = OperandType.ReturnAddress;
                    }
                    else if (opd == "sp")
                    {
                        instruction.DestinationRegistryType = OperandType.StackPointer;
                    }
                    else
                    {
                        instruction.DestinationRegistryType = OperandType.UserRegistry;
                        instruction.DestinationRegistry = uint.Parse(opd.Substring(1));
                    }
                    
                    // TODO: Put all that in a function, goddamit!
                    if (opl == "ra")
                    {
                        instruction.LeftOperandType = OperandType.ReturnAddress;
                    }
                    else if (opl == "sp")
                    {
                        instruction.LeftOperandType = OperandType.StackPointer;
                    }
                    else if (opl[0] == 'r')
                    {
                        instruction.LeftOperandType = OperandType.UserRegistry;
                        instruction.LeftOperandValue = int.Parse(opl.Substring(1));
                    }
                    else
                    {
                        instruction.LeftOperandType = OperandType.Literal;
                        instruction.LeftOperandValue = float.Parse(opl, CultureInfo.InvariantCulture);
                    }
                    
                    instructions.Add(instruction);
                    
                    if ((debugData & DebugData.CompiledInstruction) > 0)
                        debugCallback?.Invoke("compiler > " + instruction);

                    continue;
                }

                // Binary operations.

                // regex = new Regex(
                //     @"^(?<opt>add|sub|mul|div)\s+(?<opd>r\d+)\s+(?<opl>r?\d+[.]?\d*)\s+(?<opr>r?\d+[.]?\d*)$");
                regex = new Regex(
                    @"^(?<opt>add|sub|mul|div)\s+(?<opd>r\d+\b|ra|sp)\s+(?<opl>-?\d+[.]?\d*|r\d+\b|ra|sp)\s+(?<opr>-?\d+[.]?\d*|r\d+\b|ra|sp)$");
                match = regex.Match(lines[index]);

                if (match.Success)
                {
                    string opt = match.Groups["opt"].Value;
                    string opd = match.Groups["opd"].Value;
                    string opl = match.Groups["opl"].Value;
                    string opr = match.Groups["opr"].Value;

                    Instruction instruction = default;
                    instruction.RawText = lines[index];
                    instruction.Line = index + 1u;
                    
                    switch (opt)
                    {
                        case "add": instruction.Operation = Operation.Add; break;
                        case "sub": instruction.Operation = Operation.Subtract; break;
                        case "mul": instruction.Operation = Operation.Multiply; break;
                        case "div": instruction.Operation = Operation.Divide; break;
                        default: return new Result(Error.OperationNotSupported, instruction);
                    }

                    

                    if (opd == "ra")
                    {
                        instruction.DestinationRegistryType = OperandType.ReturnAddress;
                    }
                    else if (opd == "sp")
                    {
                        instruction.DestinationRegistryType = OperandType.StackPointer;
                    }
                    else
                    {
                        instruction.DestinationRegistryType = OperandType.UserRegistry;
                        instruction.DestinationRegistry = uint.Parse(opd.Substring(1));
                    }
                    
                    if (opl == "ra")
                    {
                        instruction.LeftOperandType = OperandType.ReturnAddress;
                    }
                    else if (opl == "sp")
                    {
                        instruction.LeftOperandType = OperandType.StackPointer;
                    }
                    else if (opl[0] == 'r')
                    {
                        instruction.LeftOperandType = OperandType.UserRegistry;
                        instruction.LeftOperandValue = int.Parse(opl.Substring(1));
                    }
                    else
                    {
                        instruction.LeftOperandType = OperandType.Literal;
                        instruction.LeftOperandValue = float.Parse(opl, CultureInfo.InvariantCulture);
                    }

                    if (opr[0] == 'r')
                    {
                        instruction.RightOperandType = OperandType.UserRegistry;
                        instruction.RightOperandValue = int.Parse(opr.Substring(1));
                    }
                    else
                    {
                        instruction.RightOperandType = OperandType.Literal;
                        instruction.RightOperandValue = float.Parse(opr, CultureInfo.InvariantCulture);
                    }

                    instructions.Add(instruction);
                    
                    if ((debugData & DebugData.CompiledInstruction) > 0)
                        debugCallback?.Invoke("compiler > " + instruction);

                    continue;
                }

                return new Result(Error.SyntaxError, index + 1, lines[index]);
            }

            program.Instructions = instructions.ToArray();

            return Result.Success();
        }
    }
    
    public class Program
    {
        internal Instruction[] Instructions = Array.Empty<Instruction>();
    }

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
    }

    internal enum OperandType
    {
        Literal,
        UserRegistry,
        StackPointer,
        ReturnAddress
    }
    
    internal struct Instruction
    {
        internal Operation Operation;

        internal OperandType DestinationRegistryType;
        internal uint DestinationRegistry;
        
        internal OperandType LeftOperandType;
        internal float LeftOperandValue;
        
        internal OperandType RightOperandType;
        internal float RightOperandValue;

        internal uint Line;
        internal string RawText;

        public override string ToString()
        {
            return $"{Operation} {DestinationRegistryType} {DestinationRegistry} {LeftOperandType} {LeftOperandValue} " +
                   $"{RightOperandType} {RightOperandValue}";
        }
    }
}
