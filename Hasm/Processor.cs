using System;
using System.Threading;

namespace Hasm
{
    public class Processor
    {
        private readonly int _frequencyHz;
        private readonly float[] _registers;
        private readonly float[] _stack;

        private uint _stackPointer;
        private uint _returnAddress;

        public Processor(int numRegistries = 8, int stackLength = 32, int frequencyHz = 0 /* not simulated */)
        {
            _registers = new float[numRegistries];
            _stack = new float[stackLength];
            _frequencyHz = frequencyHz;
        }

        private void SetRegistry(ref Instruction instruction, float value)
        {
            switch (instruction.DestinationRegistryType)
            {
                case Instruction.OperandType.UserRegistry:
                    _registers[instruction.DestinationRegistry] = value;
                    break;
                
                case Instruction.OperandType.StackPointer:
                    _stackPointer = (uint)value;
                    break;
                
                case Instruction.OperandType.ReturnAddress:
                    _returnAddress = (uint)value;
                    break;
                
                case Instruction.OperandType.Literal:
                    throw new InvalidOperationException();
                
                default:
                    throw new NotImplementedException();
            }
        }
        
        // TODO: something smart, not related to Instruction, and more generic.
        private float GetRegistry(ref Instruction instruction)
        {
            switch (instruction.DestinationRegistryType)
            {
                case Instruction.OperandType.UserRegistry:
                    return _registers[instruction.DestinationRegistry];
                
                case Instruction.OperandType.StackPointer:
                    return _stackPointer;
                
                case Instruction.OperandType.ReturnAddress:
                    return _returnAddress;
                
                case Instruction.OperandType.Literal:
                    throw new InvalidOperationException();
                
                default:
                    throw new NotImplementedException();
            }
        }

        private bool TryGetOperandValue(Instruction.OperandType type, ref float value)
        {
            if (type == Instruction.OperandType.UserRegistry && (value < 0 || value >= _registers.Length))
                return false;
            
            switch (type)
            {
                case Instruction.OperandType.Literal: break;
                case Instruction.OperandType.UserRegistry: value = _registers[(int)value]; break;
                case Instruction.OperandType.StackPointer: value = _stackPointer; break;
                case Instruction.OperandType.ReturnAddress: value = _returnAddress; break;
                default: throw new ArgumentOutOfRangeException();
            }

            return true;
        }
        
        public Result Run(Program program, Action<string>? debugCallback = null, DebugData debugData = DebugData.None)
        {
            if (_registers.Length < program.RequiredRegisters || _stack.Length < program.RequiredStack)
                return new Result(Error.RequirementsNotMet);
            
            bool breakLoop = false;
            for (int index = 0; index < program.Instructions.Length && !breakLoop; index++)
            {
                Instruction instruction = program.Instructions[index];
                
                float leftOperandValue = instruction.LeftOperandValue;
                float rightOperandValue = instruction.RightOperandValue;
                
                if (!TryGetOperandValue(instruction.LeftOperandType, ref leftOperandValue) ||
                    !TryGetOperandValue(instruction.RightOperandType, ref rightOperandValue) ||
                    instruction.DestinationRegistry >= _registers.Length)
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
                    
                    case Operation.Increment:
                        SetRegistry(ref instruction, GetRegistry(ref instruction) + 1f);
                        break;
                    
                    case Operation.Decrement:
                        SetRegistry(ref instruction, GetRegistry(ref instruction) - 1f);
                        break;
                    
                    case Operation.Equal:
                        SetRegistry(ref instruction, Math.Abs(leftOperandValue - rightOperandValue) < float.Epsilon ? 1f : 0f);
                        break;
                    
                    case Operation.NotEqual:
                        SetRegistry(ref instruction, Math.Abs(leftOperandValue - rightOperandValue) < float.Epsilon ? 0f : 1f);
                        break;
                    
                    case Operation.GreaterThan:
                        SetRegistry(ref instruction, leftOperandValue > rightOperandValue ? 1f : 0f);
                        break;
                    
                    case Operation.GreaterThanOrEqual:
                        SetRegistry(ref instruction, leftOperandValue >= rightOperandValue ? 1f : 0f);
                        break;
                    
                    case Operation.LesserThan:
                        SetRegistry(ref instruction, leftOperandValue < rightOperandValue ? 1f : 0f);
                        break;
                    
                    case Operation.LesserThanOrEqual:
                        SetRegistry(ref instruction, leftOperandValue <= rightOperandValue ? 1f : 0f);
                        break;
                    
                    case Operation.Push:
                        if (_stackPointer >= _stack.Length)
                            return new Result(Error.StackOverflow, instruction);
                        _stack[_stackPointer++] = _registers[instruction.DestinationRegistry];
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
                        if (Math.Abs(_registers[instruction.DestinationRegistry] - leftOperandValue) > float.Epsilon)
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

                        // Find next instruction (+1 wouldn't ignore blank lines and comments).
                        for (var searchIndex = 0u; searchIndex < program.Instructions.Length; searchIndex++)
                        {
                            if (program.Instructions[searchIndex].Line > instruction.Line + 1)
                            {
                                _returnAddress = program.Instructions[searchIndex].Line;
                                break;
                            }
                        }

                        break;
                    }
                    
                    case Operation.Ret:
                        breakLoop = true;
                        break;

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
            return registry >= 0 && registry < _registers.Length ? _registers[registry] : float.NaN;
        }

        public string DumpMemory()
        {
            return $"Sp: {_stackPointer:d4} Ra: {_returnAddress:d4} " + 
                   $"Registries: {string.Join(" ", _registers)} " +
                   $"Stack: {string.Join(" ", _stack)} ";
        }
    }
}