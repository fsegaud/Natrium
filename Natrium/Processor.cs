using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Natrium
{
    public class Processor
    {
        private readonly double[] _registers;
        private readonly double[] _stack;
#if NATRIUM_FEATURE_MEMORY
        private readonly double[] _memory;
        private readonly uint[] _memoryBlocks;
#endif
        private readonly IDevice?[] _devices;

        private uint _stackPointer;
        private uint _returnAddress;
        private int _instructionPointer;
        
        private readonly Stopwatch _sleepWatch = new Stopwatch();
        private long _sleepTime = -1;
        private int? _watchdog;
        private uint _frame;
        
        private Program? _program;
        
        public bool IsFinished => HasError || _instructionPointer >= _program?.Instructions.Length;

        public bool HasError => LastError.Error != Error.Success;
        public  Result LastError { get; private set; }
        
        public  Action<DebugData>? DebugCallback { get; set; }
        
#if NATRIUM_FEATURE_MEMORY
        public Processor(uint numRegisters = 8u, uint stackLength = 16u, uint memoryLength = 32u, uint numDevices = 0u)
#else
        public Processor(uint numRegisters = 8u, uint stackLength = 16u, uint numDevices = 0u)
#endif
        {
            _registers = new double[numRegisters];
            _stack = new double[stackLength];
            _devices = new IDevice[numDevices];
#if  NATRIUM_FEATURE_MEMORY
            _memory = new double[memoryLength];
            _memoryBlocks = new uint[memoryLength];
#endif
        }

        public TDevice? PlugDevice<TDevice>(uint deviceSlot, TDevice device) where TDevice : class, IDevice
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
                        LastError = new Result(Error.DeviceOutOfBound, instruction);
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
                    throw new NotSupportedException(nameof(instruction.DestinationRegistryType));
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
                        LastError = new Result(Error.DeviceOutOfBound, instruction);
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
                    throw new NotSupportedException(nameof(instruction.DestinationRegistryType));
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
                        LastError = new Result(Error.DeviceOutOfBound, instruction);
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

        private bool TryResolveJump(ref Instruction instruction, out int destinationIndex, out uint returnAddress)
        {
            destinationIndex = 0;
            returnAddress = 0;
            
            if (_program == null)
            {
                return false;
            }
            
            if (!TryGetDestination(ref instruction, out double destinationValue))
                return false;

            destinationIndex = -1;
            for (var searchIndex = 0; searchIndex < _program?.Instructions.Length; searchIndex++)
            {
                if (_program.Instructions[searchIndex].Line == (uint)destinationValue)
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
            for (var searchIndex = 0u; searchIndex < _program?.Instructions.Length; searchIndex++)
            {
                if (_program.Instructions[searchIndex].Line > instruction.Line)
                {
                    returnAddress = _program.Instructions[searchIndex].Line;
                    break;
                }
            }

            return true;
        }

        public bool BreakpointReached;
        
        public void Load(Program program, int? watchdog = null, bool unplugDevices = false)
        {
            _program = program;
            _sleepTime = -1;
            _sleepWatch.Stop();
            _watchdog = watchdog;
            _frame = 0;
            
            _stackPointer = 0;
            _returnAddress = 0;
            _instructionPointer = -0;

            Array.Clear(_registers, 0, _registers.Length);
            Array.Clear(_stack, 0, _stack.Length);
#if NATRIUM_FEATURE_MEMORY
            Array.Clear(_memory, 0, _memory.Length);
            Array.Clear(_memoryBlocks, 0, _memoryBlocks.Length);
#endif
            if (unplugDevices)
            {
                Array.Clear(_devices, 0, _devices.Length);
            }

            LastError = Result.Success();
            
            if (_registers.Length < program.RequiredRegisters || _stack.Length < program.RequiredStack || 
                _devices.Length < program.RequiredDevices)
                LastError = new Result(Error.RequirementsNotMet);
        }
        
        public bool Run(int frames = int.MaxValue)
        {
            if (_program == null)
            {
                LastError = new Result(Error.ProgramNotLoaded);
                return false;
            }
            
            if (IsFinished)
                return true;

            if (_sleepTime > 0)
            {
                if (_sleepWatch.ElapsedMilliseconds < _sleepTime)
                    return true;
                _sleepTime = -1;
                _sleepWatch.Reset();
            }
            
            bool breakLoop = false;
            for (int index = _instructionPointer; index < _program.Instructions.Length && !breakLoop && frames > 0; index++, _instructionPointer++, _frame++, frames--, _watchdog--)
            {
                BreakpointReached = false;
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
                    
                    case Operation.Round:
                        TrySetDestination(ref instruction, Math.Round(leftOperandValue, (int)rightOperandValue));
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
                    
                    case Operation.Modulo:
                        if (rightOperandValue == 0)
                        {
                            LastError = new Result(Error.DivisionByZero, instruction);
                            return false;
                        }
                        
                        TrySetDestination(ref instruction, leftOperandValue % rightOperandValue);
                        break;

                    case Operation.SquareRoot:
                        if (leftOperandValue < 0)
                        {
                            LastError = new Result(Error.NaN, instruction);
                            return false;
                        }

                        TrySetDestination(ref instruction, Math.Sqrt(leftOperandValue));
                        break;
                    
                    case Operation.Power:
                        TrySetDestination(ref instruction, Math.Pow(leftOperandValue, rightOperandValue));
                        break;
                    
                    case Operation.Sine:
                        TrySetDestination(ref instruction, Math.Sin(leftOperandValue));
                        break;
                    
                    case Operation.Cosine:
                        TrySetDestination(ref instruction, Math.Cos(leftOperandValue));
                        break;
                    
                    case Operation.Tangent:
                        TrySetDestination(ref instruction, Math.Tan(leftOperandValue));
                        break;

                    case Operation.ArcSine:
                    {
                        if (leftOperandValue < -1 || leftOperandValue > 1)
                        {
                            LastError = new Result(Error.BadArguments, instruction);
                            return false;
                        }
                        
                        TrySetDestination(ref instruction, Math.Asin(leftOperandValue));
                        break;
                    }

                    case Operation.ArcCosine:
                    {
                        if (leftOperandValue < -1 || leftOperandValue > 1)
                        {
                            LastError = new Result(Error.BadArguments, instruction);
                            return false;
                        }

                        TrySetDestination(ref instruction, Math.Acos(leftOperandValue));
                        break;
                    }

                    case Operation.ArcTangent:
                    {
                        if (leftOperandValue < Math.PI * -0.5 || leftOperandValue > Math.PI * 0.5)
                        {
                            LastError = new Result(Error.BadArguments, instruction);
                            return false;
                        }
                        
                        TrySetDestination(ref instruction, Math.Atan(leftOperandValue));
                        break;
                    }

                    case Operation.RandomDouble:
                    {
                        if (leftOperandValue > rightOperandValue)
                        {
                            LastError = new Result(Error.BadArguments, instruction);
                            return false;
                        }
                        
                        Random random = new Random();
                        TrySetDestination(ref instruction, leftOperandValue + random.NextDouble() * (rightOperandValue - leftOperandValue));
                        
                        break;
                    }
                    
                    case Operation.RandomInteger:
                    {
                        if (leftOperandValue > rightOperandValue)
                        {
                            LastError = new Result(Error.BadArguments, instruction);
                            return false;
                        }
                        
                        Random random = new Random();
                        TrySetDestination(ref instruction, random.Next((int)leftOperandValue, (int)rightOperandValue + 1));
                        
                        break;
                    }

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
                    
                    case Operation.Approx:
                        TrySetDestination(ref instruction, Math.Abs(leftOperandValue - rightOperandValue) < 0.00001 ? 1d : 0d);
                        break;
                    
                    case Operation.Minimum:
                        TrySetDestination(ref instruction, Math.Min(leftOperandValue, rightOperandValue));
                        break;
                    
                    case Operation.Maximum:
                        TrySetDestination(ref instruction, Math.Max(leftOperandValue, rightOperandValue));
                        break;
                    
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
                        if (_program.BuildTarget == BuildTarget.Debug)
                        {
                            if (!TryGetDestination(ref instruction, out destinationValue))
                                return false;
                            if (Math.Abs(destinationValue - leftOperandValue) > double.Epsilon * 10000000)
                            {
                                LastError = new Result(Error.AssertFailed, instruction);
                                return false;
                            }
                        }

                        break;

                    case Operation.Jump:
                    {
                        // ReSharper disable once UnusedVariable
                        if (!TryResolveJump(ref instruction, out int destinationIndex, out uint returnAddress))
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
                        if (!TryResolveJump(ref instruction, out int destinationIndex, out uint returnAddress))
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
                        if (!TryResolveJump(ref instruction, out int destinationIndex, out uint returnAddress))
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
                    
                    case Operation.BitwiseNot:
                        TrySetDestination(ref instruction, ~(int)leftOperandValue);
                        break;
                    
                    case Operation.BitwiseAnd:
                        TrySetDestination(ref instruction, (int)leftOperandValue & (int)rightOperandValue);
                        break;
                    
                    case Operation.BitwiseOr:
                        TrySetDestination(ref instruction, (int)leftOperandValue | (int)rightOperandValue);
                        break;
                    
                    case Operation.BitwiseExclusiveOr:
                        TrySetDestination(ref instruction, (int)leftOperandValue ^ (int)rightOperandValue);
                        break;
                    
                    case Operation.BitwiseShiftLeft:
                        TrySetDestination(ref instruction, (int)leftOperandValue << (int)rightOperandValue);
                        break;
                    
                    case Operation.BitwiseShiftRight:
                        TrySetDestination(ref instruction, (int)leftOperandValue >> (int)rightOperandValue);
                        break;
                    
#if NATRIUM_FEATURE_MEMORY
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
                            LastError = new Result(Error.DoubleFree, instruction);
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

                    case Operation.SetMemory:
                    {
                        TryGetDestination(ref instruction, out destinationValue);
                        
                        uint setPointer = (uint)destinationValue;
                        if (setPointer == 0)
                        {
                            LastError = new Result(Error.NullPointer, instruction);
                            return false;
                        }
                        
                        if (_memoryBlocks[setPointer] == 0)
                        {
                            LastError = new Result(Error.UnallocatedMemory, instruction);
                            return false;
                        }

                        double value = instruction.LeftOperandValue;
                        if (!TryGetOperandValue(ref instruction, instruction.LeftOperandType, ref value))
                        {
                            return false;
                        }

                        _memory[setPointer] = value;
                        
                        break;
                    }

                    case Operation.LoadMemory: // TODO: Memory -> check bounds on all operations.
                    {
                        double loadPointer = instruction.LeftOperandValue;
                        TryGetOperandValue(ref instruction, instruction.LeftOperandType, ref loadPointer);
                        
                        uint loadPointerInt = (uint)loadPointer;
                        
                        if (loadPointerInt == 0)
                        {
                            LastError = new Result(Error.NullPointer, instruction);
                            return false;
                        }
                        
                        if (_memoryBlocks[loadPointerInt] == 0)
                        {
                            LastError = new Result(Error.UnallocatedMemory, instruction);
                            return false;
                        }
                        
                        double value = _memory[loadPointerInt];
                        if (!TrySetDestination(ref instruction, value))
                        {
                            return false;
                        }
                        
                        break;
                    }
#endif
                    case Operation.SleepMilliseconds:
                        _sleepTime = (long)leftOperandValue;
                        _sleepWatch.Restart();
                        breakLoop = true;
                        break;
                    
                    case Operation.Yield:
                        breakLoop = true;
                        break;
                    
                    case Operation.Ret:
                        _instructionPointer = int.MaxValue - 1;
                        breakLoop = true;
                        break;
                    
                    case Operation.Die:
                        _instructionPointer = int.MaxValue - 1;
                        breakLoop = true;
                        LastError = new Result(Error.DiedInPain, instruction);
                        break;
                    
                    case Operation.Breakpoint:
                        if (_program.BuildTarget == BuildTarget.Debug)
                        {
                            breakLoop = true;
                            BreakpointReached = true;
                        }

                        break;

                    default:
                        LastError = new Result(Error.OperationNotImplemented, instruction);
                        return false;
                }

                if (LastError.Error != Error.Success)
                    return false;
                
                DebugCallback?.Invoke(GenerateDebugData(_frame, ref instruction));

                if (_watchdog <= 0)
                {
                    // Infinite loop?
                    LastError = new Result(Error.WatchdogBark, instruction);
                    return false;
                }
            }

            return true;
        }

        public void Push(double value)
        {
            if (_stackPointer < _stack.Length)
                _stack[_stackPointer++] = value;
        }

        public double Pop()
        {
            return _stackPointer > 0 ? _stack[--_stackPointer] : double.NaN;
        }

        private DebugData GenerateDebugData(uint frame, ref Instruction instruction)
        {
            DebugData data;
            
            // Instruction data.

            data.Frame = frame;
            data.Line = instruction.Line;
            data.RawInstruction = instruction.RawInstruction;
            data.PreprocessedInstruction = instruction.PreprocessedInstruction;
            data.EncodedInstruction = instruction.ToString();
            
            // Program state.
            
            data.StackPointer = _stackPointer;
            data.ReturnAddress = _returnAddress;
                
            data.Registers = new double[_registers.Length];
            _registers.CopyTo(data.Registers, 0);
            
            data.Stack = new double[_stack.Length];
            _stack.CopyTo(data.Stack, 0);
            
#if NATRIUM_FEATURE_MEMORY 
            data.Memory = new double[_memory.Length];
            _memory.CopyTo(data.Memory, 0);
            
            data.MemoryBlocks = new uint[_memoryBlocks.Length];
            _memoryBlocks.CopyTo(data.MemoryBlocks, 0);
#endif            
            return data;
        }
    }
}