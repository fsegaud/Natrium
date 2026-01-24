using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Hasm
{
    public class Processor
    {
        private readonly double[] _registers;
        private readonly double[] _stack;
#if HASM_FEATURE_MEMORY
        private readonly double[] _memory;
        private readonly uint[] _memoryBlocks;
#endif
        private readonly IDevice?[] _devices;

        private uint _stackPointer;
        private uint _returnAddress;
        private int _instructionPointer;
        
        private Program? _program;
        private Action<DebugData>? _debugCallback;

        public bool IsFinished => HasError || _instructionPointer >= _program?.Instructions.Length;

        public bool HasError => LastError.Error != Error.Success;
        public  Result LastError { get; private set; }
        
#if HASM_FEATURE_MEMORY
        public Processor(uint numRegistries = 8u, uint stackLength = 16u, uint memoryLength = 32u, uint numDevices = 0u, int frequencyHz = 0)
#else
        public Processor(uint numRegistries = 8u, uint stackLength = 16u, uint numDevices = 0u, int frequencyHz = 0)
#endif
        {
            _registers = new double[numRegistries];
            _stack = new double[stackLength];
#if  HASM_FEATURE_MEMORY
            _memory = new double[memoryLength];
            _memoryBlocks = new uint[memoryLength];
#endif
            _devices = new IDevice[numDevices];
        }

        public T? PlugDevice<T>(uint deviceSlot, T device) where T : class, IDevice
        {
            if (deviceSlot >= _devices.Length)
                return null;
            
            _devices[deviceSlot] = device;
            return device;
        }

        public bool UnplugDevice(uint deviceSlot, IDevice device)
        {
            if (deviceSlot >= _devices.Length)
                return false;
            
            _devices[deviceSlot] = null;
            return true;
        }

        [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
        private bool TrySetDestination(ref Instruction instruction, double value)
        {
            switch (instruction.DestinationRegistryType)
            {
                case Instruction.OperandType.UserRegister:
                {
                    if (instruction.Destination >= _registers.Length)
                    {
                        LastError = new Result(Error.RegisterOutOfBound, instruction);
                        return false;
                    }
                    
                    _registers[instruction.Destination] = value;
                    break;
                }
                
                case Instruction.OperandType.StackPointer:
                {
                    _stackPointer = (uint)value;
                    break;
                }
                
                case Instruction.OperandType.ReturnAddress:
                    _returnAddress = (uint)value;
                    break;
                
                case Instruction.OperandType.DeviceRegister:
                {
                    int deviceSlot = (int)instruction.Destination >> 16;
                    int deviceRegister = 0xffff & (int)instruction.Destination;
                    
                    if (deviceSlot >= _devices.Length)
                    {
                        LastError = new Result(Error.DeviceOverflow, instruction);
                        return false;
                    }
                    
                    IDevice? device = _devices[deviceSlot];
                    if (device != null)
                    {
                        if (!device.TryWriteValue(deviceRegister, value))
                        {
                            LastError = new Result(Error.DeviceFailed, instruction);
                            return false;
                        }
                    }
                    else
                    {
                        LastError = new Result(Error.DeviceUnplugged, instruction);
                        return false;
                    }

                    break;
                }
                
                case Instruction.OperandType.HexLiteral:
                case Instruction.OperandType.Literal:
                    throw new InvalidOperationException();
                
                default:
                    throw new NotImplementedException();
            }

            return true;
        }

        private bool TryGetDestination(ref Instruction instruction, out double value)
        {
            value = 0d;
            switch (instruction.DestinationRegistryType)
            {
                case Instruction.OperandType.UserRegister:
                {
                    if (instruction.Destination >= _registers.Length)
                    {
                        LastError = new Result(Error.RegisterOutOfBound, instruction);
                        return false;
                    }
                    
                    value = _registers[instruction.Destination];
                    break;
                }

                case Instruction.OperandType.StackPointer: value = _stackPointer; break;
                case Instruction.OperandType.ReturnAddress: value = _returnAddress; break;

                case Instruction.OperandType.DeviceRegister:
                {
                    int deviceSlot = (int)instruction.Destination >> 16;
                    int deviceRegister = 0xffff & (int)instruction.Destination;
                    
                    if (deviceSlot >= _devices.Length)
                    {
                        LastError = new Result(Error.DeviceOverflow, instruction);
                        return false;
                    }
                    
                    IDevice? device = _devices[deviceSlot];
                    if (device != null)
                    {
                        if (!device.TryReadValue(deviceRegister, out value))
                        {
                            LastError = new Result(Error.DeviceFailed, instruction);
                            return false;
                        }
                    }
                    else
                    {
                        LastError = new Result(Error.DeviceUnplugged, instruction);
                        return false;
                    }

                    break;
                }
                    
                case Instruction.OperandType.HexLiteral:
                case Instruction.OperandType.Literal:
                    value = instruction.Destination;
                    break;
                
                default:
                    throw new NotImplementedException();
            }

            return true;
        }

        private bool TryGetOperandValue(ref Instruction instruction, Instruction.OperandType type, ref double value)
        {
            switch (type)
            {
                case Instruction.OperandType.Literal: break;
                case Instruction.OperandType.HexLiteral: value = (int)value; break;
                case Instruction.OperandType.StackPointer: value = _stackPointer; break;
                case Instruction.OperandType.ReturnAddress: value = _returnAddress; break;
                case Instruction.OperandType.UserRegister:
                {
                    if (value < 0 || value >= _registers.Length)
                    {
                        LastError = new Result(Error.RegisterOutOfBound, instruction);
                        return false;
                    }
                    value = _registers[(int)value]; break;
                }
                
                case Instruction.OperandType.DeviceRegister:
                {
                    int deviceSlot = (int)value >> 16;
                    int deviceRegister = 0xffff & (int)value;
                    
                    if (deviceSlot >= _devices.Length)
                    {
                        LastError = new Result(Error.DeviceOverflow, instruction);
                        return false;
                    }
                    
                    IDevice? device = _devices[deviceSlot];
                    if (device != null)
                    {
                        if (!device.TryReadValue(deviceRegister, out value))
                            LastError = new Result(Error.DeviceFailed, instruction);
                    }
                    else
                        LastError = new Result(Error.DeviceUnplugged, instruction);

                    break;
                }
                
                default: throw new ArgumentOutOfRangeException();
            }

            return true;
        }

        // TODO: Make static?
        private bool TryResolveJump(Program program, ref Instruction instruction, out int destinationIndex, out uint returnAddress)
        {
            destinationIndex = 0;
            returnAddress = 0;
            
            if (!TryGetDestination(ref instruction, out double destinationValue))
                return false;

            destinationIndex = -1;
            for (var searchIndex = 0; searchIndex < program.Instructions.Length; searchIndex++)
            {
                if (program.Instructions[searchIndex].Line == (uint)destinationValue)
                {
                    destinationIndex = searchIndex;
                    break;
                }
            }

            if (destinationIndex < 0)
            {
                LastError = new Result(Error.InvalidJump, instruction);
                return false;
            }
            
            // Find next instruction (+1 wouldn't ignore blank lines and comments).
            for (var searchIndex = 0u; searchIndex < program.Instructions.Length; searchIndex++)
            {
                if (program.Instructions[searchIndex].Line > instruction.Line)
                {
                    returnAddress = program.Instructions[searchIndex].Line;
                    break;
                }
            }

            return true;
        }
        
        public void Load(Program program, Action<DebugData>? debugCallback = null)
        {
            _program = program;
            _debugCallback = debugCallback;
            
            _stackPointer = 0;
            _returnAddress = 0;
            _instructionPointer = -0;
            
            LastError = Result.Success();
            
            if (_registers.Length < program.RequiredRegisters || _stack.Length < program.RequiredStack || 
                _devices.Length < program.RequiredDevices)
                LastError = new Result(Error.RequirementsNotMet);
        }
        
        public bool Run(int cycles = int.MaxValue)
        {
            if (_program == null)
            {
                LastError = new Result(Error.ProgramNotLoaded);
                return false;
            }

            if (IsFinished)
                return true;
            
            bool breakLoop = false;
            for (int index = _instructionPointer; index < _program.Instructions.Length && !breakLoop && cycles > 0; index++, _instructionPointer++, cycles--)
            {
                Instruction instruction = _program.Instructions[index];

                double destinationValue;
                double leftOperandValue = instruction.LeftOperandValue;
                double rightOperandValue = instruction.RightOperandValue;
                
                if (!TryGetOperandValue(ref instruction, instruction.LeftOperandType, ref leftOperandValue) ||
                    !TryGetOperandValue(ref instruction, instruction.RightOperandType, ref rightOperandValue))
                    return false;
                    
                switch (instruction.Operation)
                {
                    case Operation.Nop:
                        break;
                    
                    case Operation.Move:
                        TrySetDestination(ref instruction, leftOperandValue);
                        break;
                    
                    case Operation.Add:
                        TrySetDestination(ref instruction, leftOperandValue + rightOperandValue);
                        break;
                    
                    case Operation.Subtract:
                        TrySetDestination(ref instruction, leftOperandValue - rightOperandValue);
                        break;
                    
                    case Operation.Multiply:
                        TrySetDestination(ref instruction, leftOperandValue * rightOperandValue);
                        break;
                    
                    case Operation.Divide:
                        if (rightOperandValue == 0)
                        {
                            LastError = new Result(Error.DivisionByZero, instruction);
                            return false;
                        }

                        TrySetDestination(ref instruction, leftOperandValue / rightOperandValue);
                        break;
                    
                    case Operation.SquareRoot:
                        if (leftOperandValue < 0)
                        {
                            LastError = new Result(Error.NaN, instruction);
                            return false;
                        }

                        TrySetDestination(ref instruction, Math.Sqrt(leftOperandValue));
                        break;
                    
                    case Operation.Increment:
                    {
                        if (!TryGetDestination(ref instruction, out destinationValue))
                            return false;
                        TrySetDestination(ref instruction, destinationValue + 1d);
                        break;
                    }
                    
                    case Operation.Decrement:
                    {
                        if (!TryGetDestination(ref instruction, out destinationValue))
                            return false;
                        TrySetDestination(ref instruction, destinationValue - 1d);
                        break;
                    }
                    
                    case Operation.Equal:
                        TrySetDestination(ref instruction, Math.Abs(leftOperandValue - rightOperandValue) < double.Epsilon ? 1d : 0d);
                        break;
                    
                    case Operation.NotEqual:
                        TrySetDestination(ref instruction, Math.Abs(leftOperandValue - rightOperandValue) < double.Epsilon ? 0d : 1d);
                        break;
                    
                    case Operation.GreaterThan:
                        TrySetDestination(ref instruction, leftOperandValue > rightOperandValue ? 1d : 0d);
                        break;
                    
                    case Operation.GreaterThanOrEqual:
                        TrySetDestination(ref instruction, leftOperandValue >= rightOperandValue ? 1d : 0d);
                        break;
                    
                    case Operation.LesserThan:
                        TrySetDestination(ref instruction, leftOperandValue < rightOperandValue ? 1d : 0d);
                        break;
                    
                    case Operation.LesserThanOrEqual:
                        TrySetDestination(ref instruction, leftOperandValue <= rightOperandValue ? 1d : 0d);
                        break;
                    
                    case Operation.Push:
                        if (_stackPointer >= _stack.Length)
                        {
                            LastError = new Result(Error.StackOverflow, instruction);
                            return false;
                        }

                        if (!TryGetDestination(ref instruction, out destinationValue))
                            return false;
                        _stack[_stackPointer++] = destinationValue;
                        break;
                    
                    case Operation.Pop:
                        if (_stackPointer == 0)
                        {
                            LastError = new Result(Error.StackOverflow, instruction);
                            return false;
                        }
                        TrySetDestination(ref instruction, _stack[--_stackPointer]);
                        break;
                    
                    case Operation.Peek:
                        if (_stackPointer == 0)
                        {
                            LastError = new Result(Error.StackOverflow, instruction);
                            return false;
                        }
                        TrySetDestination(ref instruction, _stack[_stackPointer - 1]);
                        break;
                    
                    case Operation.Assert:
                        if (!TryGetDestination(ref instruction, out destinationValue))
                            return false;
                        if (Math.Abs(destinationValue - leftOperandValue) > double.Epsilon)
                        {
                            LastError = new Result(Error.AssertFailed, instruction);
                            return false;
                        }
                        break;

                    case Operation.Jump:
                    {
                        // ReSharper disable once UnusedVariable
                        if (!TryResolveJump(_program, ref instruction, out int destinationIndex, out uint returnAddress))
                        {
                            LastError = new Result(Error.InvalidJump, instruction);
                            return false;
                        }
                        
                        index = destinationIndex - 1;
                        _instructionPointer = index;
                        break;
                    }

                    case Operation.JumpReturnAddress:
                    {
                        if (!TryResolveJump(_program, ref instruction, out int destinationIndex, out uint returnAddress))
                        {
                            LastError = new Result(Error.InvalidJump, instruction);
                            return false;
                        }
                        
                        index = destinationIndex - 1;
                        _instructionPointer = index;
                        if (returnAddress > 0)
                            _returnAddress = returnAddress;
                        
                        break;
                    }

                    case Operation.BranchEqual:
                    case Operation.BranchEqualReturnAddress:
                    case Operation.BranchNotEqual:
                    case Operation.BranchNotEqualReturnAddress:
                    case Operation.BranchGreaterThan:
                    case Operation.BranchGreaterThanReturnAddress:
                    case Operation.BranchGreaterThanOrEqual:
                    case Operation.BranchGreaterThanOrEqualReturnAddress:
                    case Operation.BranchLesserThan:
                    case Operation.BranchLesserThanReturnAddress:
                    case Operation.BranchLesserThanOrEqual:
                    case Operation.BranchLesserThanOrEqualReturnAddress:
                    {
                        // Resolve jump.
                        if (!TryResolveJump(_program, ref instruction, out int destinationIndex, out uint returnAddress))
                        {
                            LastError = new Result(Error.InvalidJump, instruction);
                            return false;
                        }
                        
                        // Check condition.
                        bool conditionMet = false;
                        switch (instruction.Operation)
                        {
                            case Operation.BranchEqual:
                                conditionMet = Math.Abs(leftOperandValue - rightOperandValue) < double.Epsilon;
                                returnAddress = 0;
                                 break;
                            
                            case Operation.BranchEqualReturnAddress:
                                conditionMet = Math.Abs(leftOperandValue - rightOperandValue) < double.Epsilon;
                                break;
                            
                            case Operation.BranchNotEqual:
                                conditionMet = Math.Abs(leftOperandValue - rightOperandValue) >= double.Epsilon;
                                returnAddress = 0;
                                break;
                            
                            case Operation.BranchNotEqualReturnAddress:
                                conditionMet = Math.Abs(leftOperandValue - rightOperandValue) >= double.Epsilon;
                                break;
                            
                            case Operation.BranchGreaterThan:
                                conditionMet = leftOperandValue > rightOperandValue;
                                returnAddress = 0;
                                break;
                            
                            case Operation.BranchGreaterThanReturnAddress:
                                conditionMet = leftOperandValue > rightOperandValue;
                                break;

                            case Operation.BranchGreaterThanOrEqual:
                                conditionMet = leftOperandValue >= rightOperandValue;
                                returnAddress = 0;
                                break;
                            
                            case Operation.BranchGreaterThanOrEqualReturnAddress:
                                conditionMet = leftOperandValue >= rightOperandValue;
                                break;

                            case Operation.BranchLesserThan:
                                conditionMet = leftOperandValue < rightOperandValue;
                                returnAddress = 0;
                                break;
                            
                            case Operation.BranchLesserThanReturnAddress:
                                conditionMet = leftOperandValue < rightOperandValue;
                                break;

                            case Operation.BranchLesserThanOrEqual:
                                conditionMet = leftOperandValue <= rightOperandValue;
                                returnAddress = 0;
                                break;
                            
                            case Operation.BranchLesserThanOrEqualReturnAddress:
                                conditionMet = leftOperandValue <= rightOperandValue;
                                break;

                        }
                        
                        // Apply jump if needed;
                        if (conditionMet)
                        {
                            index = destinationIndex - 1;
                            _instructionPointer = index;
                            if (returnAddress > 0)
                                _returnAddress = returnAddress;
                        }

                        break;
                    }
                    
                    case Operation.ReadWriteDevice:
                        TrySetDestination(ref instruction, leftOperandValue);
                        break;

#if HASM_FEATURE_MEMORY
                    case Operation.AllocateMemory:
                    {
                        uint mallocPointer = 0;
                        int mallocLength = (int)instruction.LeftOperandValue;
                        for (var memIndex = 1; memIndex < _memory.Length - mallocLength; memIndex++) // mem[0] unused (0=null)
                        {
                            if (_memoryBlocks[memIndex] == 0)
                            {
                                bool failed = false;
                                // Find 'length' consecutive free cells.
                                for (var mallocIndex = memIndex + 1; mallocIndex < memIndex + mallocLength; mallocIndex++)
                                {
                                    if (_memoryBlocks[mallocIndex] != 0)
                                    {
                                        memIndex = mallocIndex;
                                        failed = true;
                                        break;
                                    }
                                }
                                
                                if (!failed)
                                {
                                    mallocPointer = (uint)memIndex;
                                    // Mark cells as block (id = first cell's address).
                                    for (var mallocIndex = memIndex; mallocIndex < memIndex + mallocLength; mallocIndex++)
                                    {
                                        _memoryBlocks[mallocIndex] = mallocPointer;
                                    }
                                    
                                    TrySetDestination(ref instruction, mallocPointer);

                                    break;
                                }
                            }
                        }

                        if (mallocPointer == 0)
                        {
                            LastError = new Result(Error.OutOfMemory, instruction);
                            return false;
                        }
                        
                        break;
                    }

                    case Operation.FreeMemory:
                    {
                        TryGetDestination(ref instruction, out destinationValue);
                        
                        uint freePointer = (uint)destinationValue;
                        if (freePointer == 0)
                        {
                            LastError = new Result(Error.NullPointer, instruction);
                            return false;
                        }

                        if (_memoryBlocks[freePointer] == 0)
                        {
                            LastError = new Result(Error.MemoryAlreadyFree, instruction);
                            return false;
                        }
                        
                        if (_memoryBlocks[freePointer] != freePointer)
                        {
                            LastError = new Result(Error.MemoryViolation, instruction);
                            return false;
                        }
                        
                        uint memIndex = freePointer;
                        while (_memoryBlocks[memIndex] == freePointer)
                        {
                            _memoryBlocks[memIndex++] = 0;
                        }
                        
                        break;
                    }
#endif
                    
                    case Operation.Ret:
                        breakLoop = true;
                        break;

                    default:
                        LastError = new Result(Error.OperationNotImplemented, instruction);
                        return false;
                }

                if (LastError.Error != Error.Success)
                    return false;
                
                _debugCallback?.Invoke(GenerateDebugData(ref instruction));
            }

            return true;
        }

        public double ReadRegistry(int registry)
        {
            return registry >= 0 && registry < _registers.Length ? _registers[registry] : double.NaN;
        }

        internal DebugData GenerateDebugData(ref Instruction instruction)
        {
            DebugData data;
            
            // Instruction data.
            
            data.Line = instruction.Line;
            data.RawInstruction = instruction.RawText;
            
            // Program state.
            
            data.StackPointer = _stackPointer;
            data.ReturnAddress = _returnAddress;
                
            data.Registers = new double[_registers.Length];
            _registers.CopyTo(data.Registers, 0);
            
            data.Stack = new double[_stack.Length];
            _stack.CopyTo(data.Stack, 0);
            
#if HASM_FEATURE_MEMORY 
            data.Memory = new double[_memory.Length];
            _memory.CopyTo(data.Memory, 0);
            
            data.MemoryBlocks = new uint[_memoryBlocks.Length];
            _memoryBlocks.CopyTo(data.MemoryBlocks, 0);
#endif            
            return data;
        }
    }
}