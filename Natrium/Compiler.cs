using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Natrium
{
    public class Compiler
    {
        private readonly List<Instruction> _instructions = new List<Instruction>();
        private readonly Dictionary<string, uint> _labelToLine = new Dictionary<string, uint>();

        private string[] _rawLines = { };
        private string[] _lines = {};
        private bool[] _skipLine = {};
        
        public  Result LastError { get; private set; }
        
        public IInclusionResolver? InclusionResolver { get; set; }
        
        public Program? Compile(string input, BuildTarget buildTarget = BuildTarget.Debug)
        {
            _lines = input.Split('\n');
            _rawLines = new string[_lines.Length];
            _skipLine = new bool[_lines.Length];
            _instructions.Clear();
            _labelToLine.Clear();
            
            LastError = Result.Success();

            Program program = new Program
            {
                BuildTarget = buildTarget
            };

            // First pass.
            bool succeed = true;
            for (var index = 0u; index < _lines.Length && succeed; index++)
            {
                succeed &= Check(ParseSpaceAndTabs(index));
                succeed &= Check(ParseComments(index));
                succeed &= Check(ParseIncludes(index));
                succeed &= Check(ParseSpaceAndTabs(index));
                
                // Check if include added new lines.
                if (_skipLine.Length != _lines.Length)
                {
                    Array.Resize(ref _rawLines, _lines.Length);
                    Array.Resize(ref _skipLine, _lines.Length);
                }
                
                _rawLines[index] = _lines[index];
            }
            
            if (!succeed)
                return null;
            
            // Second pass.
            succeed = true;
            for (var index = 0u; index < _lines.Length && succeed; index++)
            {
                succeed &= Check(ParseDefines(index));
                succeed &= Check(ParseRequirements(index, ref program));
                succeed &= Check(PreParseLabels(index));
            }

            if (!succeed)
                return null;
            
            // Third pass.
            for (var index = 0u; index < _lines.Length && succeed; index++)
            {
                succeed &= Check(ParseLabels(index));
                succeed &= Check(ParseSleep(index));
                succeed &= Check(ParseJump(index));
                succeed &= Check(ParseBranching(index));
                succeed &= Check(ParseStack(index));
                succeed &= Check(ParseSelfOperations(index));
                succeed &= Check(ParseDestinationOperations(index));
                succeed &= Check(ParseUnaryOperations(index));
                succeed &= Check(ParseBinaryOperations(index));
                succeed &= Check(ParseDeviceOperations(index));
#if NATRIUM_FEATURE_MEMORY
                succeed &= Check(ParseMemoryOperations(index));
#endif
            }
            
            if (!succeed)
                return null;
            
            // Final pass.
            FixRetInstruction();
            if (!CheckIfAllInstructionProcessed())
                return null;
            
            // Post-compile.
            program.Instructions = _instructions.ToArray();
            if (buildTarget == BuildTarget.Release)
            {
                for (var index = 0; index < program.Instructions.Length; index++)
                {
                    program.Instructions[index].RawInstruction = string.Empty;
                    program.Instructions[index].PreprocessedInstruction = string.Empty;
                }
            }
            
            LastError = Result.Success();
            
            return program;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Check(bool parseResult)
        {
            return parseResult && LastError.Error == Error.Success;
        }

        private bool ParseSpaceAndTabs(uint index)
        {
            if (_skipLine[index])
                return true;
                
            _lines[index] = _lines[index].Replace("\t", " ");
            _lines[index] = _lines[index].Trim();
            _lines[index] = RegexCollection.MultipleSpaces.Replace(_lines[index], " ");
                
            if (string.IsNullOrEmpty(_lines[index]))
                _skipLine[index] = true;

            if (RegexCollection.EmptyLine.Match(_lines[index]).Success)
            {
                _lines[index] = string.Empty;
                _skipLine[index] = true;
            }

            return true;
        }

        private bool ParseComments(uint index)
        {
            if (_skipLine[index])
                return true;
                
            Match match = RegexCollection.Comments.Match(_lines[index]);
            if (match.Success)
            {
                _lines[index] = _lines[index].Replace(match.Groups["com"].Value, string.Empty).TrimEnd();

                if (string.IsNullOrEmpty(_lines[index]))
                    _skipLine[index] = true;
            }
            
            return true;
        }

        private bool ParseDefines(uint index)
        {
            if (_skipLine[index])
                return true;
            
            Match match = RegexCollection.Defines.Match(_lines[index]);
            if (match.Success)
            {
                string alias = match.Groups["alias"].Value;
                string dest = match.Groups["dest"].Value;

                for (var replIndex = 0; replIndex < _lines.Length; replIndex++)
                {
                    _lines[replIndex] = Regex.Replace(_lines[replIndex], alias.Replace("$", @"\$") + @"\b", dest);
                }
                
                _skipLine[index] = true;
            }
            
            return true;
        }

        private bool ParseRequirements(uint index, ref Program program)
        {
            if (_skipLine[index])
                return true;
                
            Match match = RegexCollection.Requirements.Match(_lines[index]);
            if (match.Success)
            {
                string type =  match.Groups["type"].Value;
                uint value = match.Groups["val"].Value.StartsWith("0x") ? 
                    uint.Parse(match.Groups["val"].Value.Substring(2), NumberStyles.HexNumber) : 
                    uint.Parse(match.Groups["val"].Value);
                    
                switch (type)
                {
                    case "registers" : program.RequiredRegisters = value; break;
                    case "stack" : program.RequiredStack = value; break;
                    case "devices" : program.RequiredDevices = value; break;
#if NATRIUM_FEATURE_MEMORY
                    case "memory" : throw new NotImplementedException();
#endif
                    default: throw new NotSupportedException();
                }

                _skipLine[index] = true;
            }

            return true;
        }
        
        private bool ParseIncludes(uint index)
        {
            if (_skipLine[index])
                return true;
            
            if (InclusionResolver == null)
            {
                LastError = new Result(Error.NoInclusionResolverProvided, index + 1);
                return false;
            }
            
            Match match = RegexCollection.Include.Match(_lines[index]);
            if (match.Success)
            {
                string opt = match.Groups["opt"].Value;
                string src =  match.Groups["src"].Value;

                string? includeContent = InclusionResolver.ResolveInclusion(src);
                if (string.IsNullOrEmpty(includeContent))
                {
                    LastError = new Result(Error.FileNotFound, index + 1);
                    return false;
                }

                if (opt == "include")
                {
                    string[] includeLines = includeContent.Split("\n");
                    string[] newLines = new string[_lines.Length + includeLines.Length];
                    Array.Copy(_lines, 0, newLines, 0, _lines.Length);
                    Array.Copy(includeLines, 0, newLines, _lines.Length, includeLines.Length);
                    _lines = newLines;
                }
                else if (opt == "patch")
                {
                    string[] includeLines = includeContent.Split("\n");
                    string[] newLines = new string[_lines.Length + includeLines.Length];
                    Array.Copy(_lines, 0, newLines, 0, index + 1);
                    Array.Copy(includeLines, 0, newLines, index + 1, includeLines.Length);
                    Array.Copy(_lines, index + 1, newLines, index + 1 + includeLines.Length, _lines.Length - index - 1);
                    _lines = newLines;
                }

                _skipLine[index] = true;
            }

            return true;
        }

        private bool PreParseLabels(uint index)
        {
            if (_skipLine[index])
                return true;
                
            Match match = RegexCollection.Labels.Match(_lines[index]);

            if (match.Success)
            {
                string label = match.Groups["label"].Value;
                uint jumpIndex = index + 1;
                // skip blank lines (e.g.: comments.)
                while (string.IsNullOrEmpty(_lines[jumpIndex]) && jumpIndex < _lines.Length)
                    jumpIndex++;
                    
                _labelToLine.Add(label, jumpIndex + 1); // +1 index to line. 
                _skipLine[index] = true;
            }

            return true;
        }

        private bool ParseLabels(uint index)
        {
            if (_skipLine[index])
                return true;
                
            Match match = RegexCollection.LabelJumps.Match(_lines[index]);
            if (match.Success)
            {
                string label = match.Groups["label"].Value;
                    
                if (!RegexCollection.LabelRegisters.Match(label).Success)
                {
                    if (!_labelToLine.TryGetValue(label, out var jumpLine))
                    {
                        LastError = new Result(Error.LabelNotFound, index + 1, _lines[index]);
                        return false;
                    }

                    _lines[index] = _lines[index].Replace(label, jumpLine.ToString()).TrimEnd();
                }

                // TODO ???
                // Do not continue, instruction will be processed later.
            }

            return true;
        }
        
        private bool ParseSelfOperations(uint index)
        {
            if (_skipLine[index])
                return true;
                
            Match match = RegexCollection.SelfOperations.Match(_lines[index]);
            if (match.Success)
            {
                string opt = match.Groups["opt"].Value;

                Instruction instruction = default;
                instruction.RawInstruction = _rawLines[index];
                instruction.PreprocessedInstruction = _lines[index];
                instruction.Line = index + 1;
                    
                switch (opt)
                {
                    case "nop": instruction.Operation = Operation.Nop; break;
                    case "yield": instruction.Operation = Operation.Yield; break;
                    case "ret": instruction.Operation = Operation.Ret; break;
                    case "die": instruction.Operation = Operation.Die; break;
                    default:
                    {
                        LastError = new Result(Error.OperationNotSupported, instruction);
                        return false;
                    }
                }

                _skipLine[index] = true;
                _instructions.Add(instruction);
            }

            return true;
        }
        
        private bool ParseSleep(uint index)
        {
            if (_skipLine[index])
                    return true;

            Match match = RegexCollection.SleepOperations.Match(_lines[index]);
            if (match.Success)
            {
                string opt = match.Groups["opt"].Value;
                string opl = match.Groups["opl"].Value;
                
                Instruction instruction = default;
                instruction.RawInstruction = _rawLines[index];
                instruction.PreprocessedInstruction = _lines[index];
                instruction.Line = index + 1;
                
                switch (opt)
                {
                    case "sleep": instruction.Operation = Operation.SleepMilliseconds; break;
                    default:
                    {
                        LastError = new Result(Error.OperationNotSupported, instruction);
                        return false;
                    }
                }

                if (opl[0] == 'r')
                {
                    instruction.LeftOperandType = Instruction.OperandType.UserRegister;
                    instruction.LeftOperandValue = uint.Parse(opl.Substring(1));
                }
                else if (opl.StartsWith("0x"))
                {
                    instruction.LeftOperandType = Instruction.OperandType.HexLiteral;
                    instruction.LeftOperandValue = uint.Parse(opl.Substring(2), NumberStyles.HexNumber);
                }
                else
                {
                    instruction.LeftOperandType = Instruction.OperandType.Literal;
                    instruction.LeftOperandValue = uint.Parse(opl, CultureInfo.InvariantCulture);
                }
                
                _skipLine[index] = true;
                _instructions.Add(instruction);
            }

            return true;
        }
        
        private bool ParseJump(uint index)
        {
            if (_skipLine[index])
                    return true;

            // TODO: replace opl with new opd (registry type).
            Match match = RegexCollection.JumpOperations.Match(_lines[index]);
            if (match.Success)
            {
                string opt = match.Groups["opt"].Value;
                string opd = match.Groups["opd"].Value;
                
                Instruction instruction = default;
                instruction.RawInstruction = _rawLines[index];
                instruction.PreprocessedInstruction = _lines[index];
                instruction.Line = index + 1;
                
                switch (opt)
                {
                    case "j": instruction.Operation = Operation.Jump; break;
                    case "jal": instruction.Operation = Operation.JumpReturnAddress; break;
                    default:
                    {
                        LastError = new Result(Error.OperationNotSupported, instruction);
                        return false;
                    }
                }

                if (opd == "ra")
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.ReturnAddress;
                }
                else if (opd == "sp")
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.StackPointer;
                }
                else if (opd[0] == 'r')
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.UserRegister;
                    instruction.Destination = uint.Parse(opd.Substring(1));
                }
                else if (opd.StartsWith("0x"))
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.HexLiteral;
                    instruction.Destination = uint.Parse(opd.Substring(2), NumberStyles.HexNumber);
                }
                else
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.Literal;
                    instruction.Destination = uint.Parse(opd, CultureInfo.InvariantCulture);
                }
                
                _skipLine[index] = true;
                _instructions.Add(instruction);
            }

            return true;
        }
        
        private bool ParseBranching(uint index)
        {
            if (_skipLine[index])
                    return true;

            // TODO: replace opl with new opd (registry type).
            Match match = RegexCollection.BranchingOperations.Match(_lines[index]);
            if (match.Success)
            {
                string opt = match.Groups["opt"].Value;
                string opd = match.Groups["opd"].Value;
                string opl = match.Groups["opl"].Value;
                string opr = match.Groups["opr"].Value;
                
                Instruction instruction = default;
                instruction.RawInstruction = _rawLines[index];
                instruction.PreprocessedInstruction = _lines[index];
                instruction.Line = index + 1;
                
                switch (opt)
                {
                    case "beq": instruction.Operation = Operation.BranchEqual; break;
                    case "beqal": instruction.Operation = Operation.BranchEqualReturnAddress; break;
                    case "bneq": instruction.Operation = Operation.BranchNotEqual; break;
                    case "bneqal": instruction.Operation = Operation.BranchNotEqualReturnAddress; break;
                    case "bne": instruction.Operation = Operation.BranchNotEqual; break;
                    case "bneal": instruction.Operation = Operation.BranchNotEqualReturnAddress; break;
                    case "bgt": instruction.Operation = Operation.BranchGreaterThan; break;
                    case "bgtal": instruction.Operation = Operation.BranchGreaterThanReturnAddress; break;
                    case "bgte": instruction.Operation = Operation.BranchGreaterThanOrEqual; break;
                    case "bgteal": instruction.Operation = Operation.BranchGreaterThanOrEqualReturnAddress; break;
                    case "blt": instruction.Operation = Operation.BranchLesserThan; break;
                    case "bltal": instruction.Operation = Operation.BranchLesserThanReturnAddress; break;
                    case "blte": instruction.Operation = Operation.BranchLesserThanOrEqual; break;
                    case "blteal": instruction.Operation = Operation.BranchLesserThanOrEqualReturnAddress; break;
                    default:
                    {
                        LastError = new Result(Error.OperationNotSupported, instruction);
                        return false;
                    }
                }

                if (opd == "ra")
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.ReturnAddress;
                }
                else if (opd == "sp")
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.StackPointer;
                }
                else if (opd[0] == 'r')
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.UserRegister;
                    instruction.Destination = uint.Parse(opd.Substring(1));
                }
                else if (opd.StartsWith("0x"))
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.HexLiteral;
                    instruction.Destination = uint.Parse(opd.Substring(2), NumberStyles.HexNumber);
                }
                else
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.Literal;
                    instruction.Destination= uint.Parse(opd, CultureInfo.InvariantCulture);
                }
                
                if (opl == "ra")
                {
                    instruction.LeftOperandType = Instruction.OperandType.ReturnAddress;
                }
                else if (opl == "sp")
                {
                    instruction.LeftOperandType = Instruction.OperandType.StackPointer;
                }
                else if (opl[0] == 'r')
                {
                    instruction.LeftOperandType = Instruction.OperandType.UserRegister;
                    instruction.LeftOperandValue = int.Parse(opl.Substring(1));
                }
                else if (opl.StartsWith("0x"))
                {
                    instruction.LeftOperandType = Instruction.OperandType.HexLiteral;
                    instruction.LeftOperandValue = int.Parse(opl.Substring(2), NumberStyles.HexNumber);
                }
                else
                {
                    instruction.LeftOperandType = Instruction.OperandType.Literal;
                    instruction.LeftOperandValue = float.Parse(opl, CultureInfo.InvariantCulture);
                }

                if (opr == "ra")
                {
                    instruction.RightOperandType = Instruction.OperandType.ReturnAddress;
                }
                else if (opr == "sp")
                {
                    instruction.RightOperandType = Instruction.OperandType.StackPointer;
                }
                else if (opr[0] == 'r')
                {
                    instruction.RightOperandType = Instruction.OperandType.UserRegister;
                    instruction.RightOperandValue = int.Parse(opr.Substring(1));
                }
                else if (opr.StartsWith("0x"))
                {
                    instruction.RightOperandType = Instruction.OperandType.HexLiteral;
                    instruction.RightOperandValue = int.Parse(opr.Substring(2), NumberStyles.HexNumber);
                }
                else
                {
                    instruction.RightOperandType = Instruction.OperandType.Literal;
                    instruction.RightOperandValue = float.Parse(opr, CultureInfo.InvariantCulture);
                }
                
                _skipLine[index] = true;
                _instructions.Add(instruction);
            }

            return true;
        }
        
        private bool ParseStack(uint index)
        {
            if (_skipLine[index])
                return true;
                
            Match match = RegexCollection.StackOperations.Match(_lines[index]);
            if (match.Success)
            {
                string opt = match.Groups["opt"].Value;
                string opd = match.Groups["opd"].Value;
                    
                Instruction instruction = default;
                instruction.RawInstruction = _rawLines[index];
                instruction.PreprocessedInstruction = _lines[index];
                instruction.Line = index + 1;
                    
                switch (opt)
                {
                    case "push": instruction.Operation = Operation.Push; break;
                    case "pop": instruction.Operation = Operation.Pop; break;
                    case "peek": instruction.Operation = Operation.Peek; break;
                    default:
                    {
                        LastError = new Result(Error.OperationNotSupported, instruction);
                        return false;
                    }
                }
                    
                if (opd == "ra")
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.ReturnAddress;
                }
                else if (opd == "sp")
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.StackPointer;
                }
                else
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.UserRegister;
                    instruction.Destination = uint.Parse(opd.Substring(1));
                }
                    
                _skipLine[index] = true;
                _instructions.Add(instruction);
            }

            return true;
        }
        
        private bool ParseDestinationOperations(uint index)
        {
            if (_skipLine[index])
                    return true;
                
            Match match = RegexCollection.DestinationOperations.Match(_lines[index]);
            if (match.Success)
            {
                string opt = match.Groups["opt"].Value;
                string opd = match.Groups["opd"].Value;
                
                Instruction instruction = default;
                instruction.RawInstruction = _rawLines[index];
                instruction.PreprocessedInstruction = _lines[index];
                instruction.Line = index + 1;
                
                switch (opt)
                {
                    case "inc": instruction.Operation = Operation.Increment; break;
                    case "dec": instruction.Operation = Operation.Decrement; break;
                    default:
                    {
                        LastError = new Result(Error.OperationNotSupported, instruction);
                        return false;
                    }
                }
                
                if (opd == "ra")
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.ReturnAddress;
                }
                else if (opd == "sp")
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.StackPointer;
                }
                else
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.UserRegister;
                    instruction.Destination = uint.Parse(opd.Substring(1));
                }
                
                _skipLine[index] = true;
                _instructions.Add(instruction);
            }

            return true;
        }
        
        private bool ParseUnaryOperations(uint index)
        {
            if (_skipLine[index])
                    return true;
                
            Match match = RegexCollection.UnaryOperations.Match(_lines[index]);
            if (match.Success)
            {
                string opt = match.Groups["opt"].Value;
                string opd = match.Groups["opd"].Value;
                string opl = match.Groups["opl"].Value;
                
                Instruction instruction = default;
                instruction.RawInstruction = _rawLines[index];
                instruction.PreprocessedInstruction = _lines[index];
                instruction.Line = index + 1;
                
                switch (opt)
                {
                    case "mov": instruction.Operation = Operation.Move; break;
                    case "round2": instruction.Operation = Operation.Round; break;
                    case "sqrt": instruction.Operation = Operation.SquareRoot; break;
                    case "cos": instruction.Operation = Operation.Cosine; break;
                    case "sin": instruction.Operation = Operation.Sine; break;
                    case "tan": instruction.Operation = Operation.Tangent; break;
                    case "acos": instruction.Operation = Operation.ArcCosine; break;
                    case "asin": instruction.Operation = Operation.ArcSine; break;
                    case "atan": instruction.Operation = Operation.ArcTangent; break;
                    case "assert": instruction.Operation = Operation.Assert; break;
                    default:
                    {
                        LastError = new Result(Error.OperationNotSupported, instruction);
                        return false;
                    }
                }
                
                if (opd == "ra")
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.ReturnAddress;
                }
                else if (opd == "sp")
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.StackPointer;
                }
                else if (opd.StartsWith("r"))
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.UserRegister;
                    instruction.Destination = uint.Parse(opd.Substring(1));
                }
                else if (opd.StartsWith("d"))
                {
                    uint deviceSlot = uint.Parse(opd.Substring(1, opd.IndexOf(".", StringComparison.InvariantCulture) - 1));
                    uint deviceRegister = uint.Parse(opd.Substring(opd.IndexOf(".", StringComparison.InvariantCulture) + 1));
                    instruction.DestinationRegistryType = Instruction.OperandType.DeviceRegister;
                    instruction.Destination = deviceSlot << 16 | deviceRegister; // TODO: Check overflow
                }
                else
                {
                    LastError = new Result(Error.SyntaxError, instruction);
                    return false;
                }
                
                // TODO: Put all that in a function, goddamit!
                if (opl == "ra")
                {
                    instruction.LeftOperandType = Instruction.OperandType.ReturnAddress;
                }
                else if (opl == "sp")
                {
                    instruction.LeftOperandType = Instruction.OperandType.StackPointer;
                }
                else if (opl[0] == 'r')
                {
                    instruction.LeftOperandType = Instruction.OperandType.UserRegister;
                    instruction.LeftOperandValue = int.Parse(opl.Substring(1));
                }
                else if (opl[0] == 'd')
                {
                    uint deviceSlot = uint.Parse(opl.Substring(1, opl.IndexOf(".", StringComparison.InvariantCulture) - 1));
                    uint deviceRegister = uint.Parse(opl.Substring(opl.IndexOf(".", StringComparison.InvariantCulture) + 1));
                    instruction.LeftOperandType = Instruction.OperandType.DeviceRegister;
                    instruction.LeftOperandValue = deviceSlot << 16 | deviceRegister; // TODO: Check overflow
                }
                else if (opl.StartsWith("0x"))
                {
                    instruction.LeftOperandType = Instruction.OperandType.HexLiteral;
                    instruction.LeftOperandValue = int.Parse(opl.Substring(2), NumberStyles.HexNumber);
                }
                else
                {
                    instruction.LeftOperandType = Instruction.OperandType.Literal;
                    instruction.LeftOperandValue = float.Parse(opl, CultureInfo.InvariantCulture);
                }
                
                _skipLine[index] = true;
                _instructions.Add(instruction);
            }

            return true;
        }

        private bool ParseBinaryOperations(uint index)
        {
            if (_skipLine[index])
                    return true;
                
            Match match = RegexCollection.BinaryOperations.Match(_lines[index]);
            if (match.Success)
            {
                string opt = match.Groups["opt"].Value;
                string opd = match.Groups["opd"].Value;
                string opl = match.Groups["opl"].Value;
                string opr = match.Groups["opr"].Value;

                Instruction instruction = default;
                instruction.RawInstruction = _rawLines[index];
                instruction.PreprocessedInstruction = _lines[index];
                instruction.Line = index + 1u;
                
                switch (opt)
                {
                    case "add": instruction.Operation = Operation.Add; break;
                    case "sub": instruction.Operation = Operation.Subtract; break;
                    case "mul": instruction.Operation = Operation.Multiply; break;
                    case "div": instruction.Operation = Operation.Divide; break;
                    case "mod": instruction.Operation = Operation.Modulo; break;
                    case "pow":  instruction.Operation = Operation.Power; break;
                    case "rnd":  instruction.Operation = Operation.RandomDouble; break;
                    case "rndi":  instruction.Operation = Operation.RandomInteger; break;
                    case "round":  instruction.Operation = Operation.Round; break;
                    case "apx": instruction.Operation = Operation.Approx; break;
                    case "min": instruction.Operation = Operation.Minimum; break;
                    case "max": instruction.Operation = Operation.Maximum; break;
                    case "eq": instruction.Operation = Operation.Equal; break;
                    case "ne": instruction.Operation = Operation.NotEqual; break;
                    case "neq": instruction.Operation = Operation.NotEqual; break;
                    case "gt": instruction.Operation = Operation.GreaterThan; break;
                    case "gte": instruction.Operation = Operation.GreaterThanOrEqual; break;
                    case "lt": instruction.Operation = Operation.LesserThan; break;
                    case "lte": instruction.Operation = Operation.LesserThanOrEqual; break;
                    default:
                    {
                        LastError = new Result(Error.OperationNotSupported, instruction);
                        return false;
                    }
                }

                if (opd == "ra")
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.ReturnAddress;
                }
                else if (opd == "sp")
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.StackPointer;
                }
                else
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.UserRegister;
                    instruction.Destination = uint.Parse(opd.Substring(1));
                }
                
                if (opl == "ra")
                {
                    instruction.LeftOperandType = Instruction.OperandType.ReturnAddress;
                }
                else if (opl == "sp")
                {
                    instruction.LeftOperandType = Instruction.OperandType.StackPointer;
                }
                else if (opl[0] == 'r')
                {
                    instruction.LeftOperandType = Instruction.OperandType.UserRegister;
                    instruction.LeftOperandValue = int.Parse(opl.Substring(1));
                }
                else if (opl.StartsWith("0x"))
                {
                    instruction.LeftOperandType = Instruction.OperandType.HexLiteral;
                    instruction.LeftOperandValue = int.Parse(opl.Substring(2), NumberStyles.HexNumber);
                }
                else
                {
                    instruction.LeftOperandType = Instruction.OperandType.Literal;
                    instruction.LeftOperandValue = float.Parse(opl, CultureInfo.InvariantCulture);
                }

                if (opr[0] == 'r')
                {
                    instruction.RightOperandType = Instruction.OperandType.UserRegister;
                    instruction.RightOperandValue = int.Parse(opr.Substring(1));
                }
                else if (opr.StartsWith("0x"))
                {
                    instruction.RightOperandType = Instruction.OperandType.HexLiteral;
                    instruction.RightOperandValue = int.Parse(opr.Substring(2), NumberStyles.HexNumber);
                }
                else
                {
                    instruction.RightOperandType = Instruction.OperandType.Literal;
                    instruction.RightOperandValue = float.Parse(opr, CultureInfo.InvariantCulture);
                }

                _skipLine[index] = true;
                _instructions.Add(instruction);
            }

            return true;
        }
        
        private bool ParseDeviceOperations(uint index)
        {
            if (_skipLine[index])
                    return true;
                
            Match readMatch = RegexCollection.ReadDeviceOperations.Match(_lines[index]);
            Match writeMatch = RegexCollection.WriteDeviceOperations.Match(_lines[index]);
            Match match = readMatch.Success ? readMatch : writeMatch;
                
            if (match.Success)
            {
                string opt = match.Groups["opt"].Value;
                string opd = match.Groups["opd"].Value;
                string opl = match.Groups["opl"].Value;
                
                Instruction instruction = default;
                instruction.RawInstruction = _rawLines[index];
                instruction.PreprocessedInstruction = _lines[index];
                instruction.Line = index + 1;
                
                switch (opt)
                {
                    case "rdev": instruction.Operation = Operation.ReadWriteDevice; break;
                    case "wdev": instruction.Operation = Operation.ReadWriteDevice; break;
                    case "rd": instruction.Operation = Operation.ReadWriteDevice; break;
                    case "wd": instruction.Operation = Operation.ReadWriteDevice; break;
                    default:
                    {
                        LastError = new Result(Error.OperationNotSupported, instruction);
                        return false;
                    }
                }
                
                if (opd.StartsWith("r"))
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.UserRegister;
                    instruction.Destination = uint.Parse(opd.Substring(1));
                }
                else if (opd.StartsWith("d"))
                {
                    uint deviceSlot = uint.Parse(opd.Substring(1, opd.IndexOf(".", StringComparison.InvariantCulture) - 1));
                    uint deviceRegister = uint.Parse(opd.Substring(opd.IndexOf(".", StringComparison.InvariantCulture) + 1));
                    instruction.DestinationRegistryType = Instruction.OperandType.DeviceRegister;
                    instruction.Destination = deviceSlot << 16 | deviceRegister; // TODO: Check overflow
                }
                else
                {
                    LastError = new Result(Error.SyntaxError, instruction);
                    return false;
                }
                
                // TODO: Put all that in a function, goddamit!
                if (opl[0] == 'r')
                {
                    instruction.LeftOperandType = Instruction.OperandType.UserRegister;
                    instruction.LeftOperandValue = int.Parse(opl.Substring(1));
                }
                else if (opl.StartsWith("d"))
                {
                    uint deviceSlot = uint.Parse(opl.Substring(1, opl.IndexOf(".", StringComparison.InvariantCulture) - 1));
                    uint deviceRegister = uint.Parse(opl.Substring(opl.IndexOf(".", StringComparison.InvariantCulture) + 1));
                    instruction.LeftOperandType = Instruction.OperandType.DeviceRegister;
                    instruction.LeftOperandValue = deviceSlot << 16 | deviceRegister; // TODO: Check overflow
                }
                else if (opl.StartsWith("0x"))
                {
                    instruction.LeftOperandType = Instruction.OperandType.HexLiteral;
                    instruction.LeftOperandValue = int.Parse(opl.Substring(2), NumberStyles.HexNumber);
                }
                else
                {
                    instruction.LeftOperandType = Instruction.OperandType.Literal;
                    instruction.LeftOperandValue = float.Parse(opl, CultureInfo.InvariantCulture);
                }
                
                _skipLine[index] = true;
                _instructions.Add(instruction);
            }

            return true;
        }
        
#if NATRIUM_FEATURE_MEMORY
        private bool ParseMemoryOperations(uint index)
        {
            if (_skipLine[index])
                    return true;
                
            Match matchMalloc = RegexCollection.AllocateMemory.Match(_lines[index]);
            Match matchFree = RegexCollection.FreeMemory.Match(_lines[index]);
            
            if (matchMalloc.Success)
            {
                string opd = matchMalloc.Groups["opd"].Value;
                string opl = matchMalloc.Groups["opl"].Value;
                
                Instruction instruction = default;
                instruction.RawText = _lines[index];
                instruction.Line = index + 1;

                instruction.Operation = Operation.AllocateMemory;
                
                instruction.DestinationRegistryType = Instruction.OperandType.UserRegister;
                instruction.Destination = uint.Parse(opd.Substring(1));
                
                instruction.LeftOperandType = Instruction.OperandType.Literal;
                instruction.LeftOperandValue = float.Parse(opl, CultureInfo.InvariantCulture);
                
                _skipLine[index] = true;
                _instructions.Add(instruction);
            }
            else if (matchFree.Success)
            {
                string opd = matchFree.Groups["opd"].Value;
                
                Instruction instruction = default;
                instruction.RawText = _lines[index];
                instruction.Line = index + 1;

                instruction.Operation = Operation.FreeMemory;
                
                instruction.DestinationRegistryType = Instruction.OperandType.UserRegister;
                instruction.Destination = uint.Parse(opd.Substring(1));
                
                _skipLine[index] = true;
                _instructions.Add(instruction);
            }

            return true;
        }
#endif

        private void FixRetInstruction()
        {
            // Special 'ret' instruction at end.
            if (_instructions.Count == 0 || _instructions[^1].Operation != Operation.Ret)
            {
                Instruction instruction = new Instruction()
                {
                    RawInstruction = "ret",
                    PreprocessedInstruction = "ret",
                    Operation = Operation.Ret,
                    Line = _instructions.Count == 0 ? 0 : _instructions[^1].Line + 3
                };
                
                _instructions.Add(instruction);
            }
        }

        private bool CheckIfAllInstructionProcessed()
        {
            for (var index = 0u; index < _lines.Length; index++)
            {
                if (!_skipLine[index])
                {
                    LastError = new Result(Error.SyntaxError, index + 1, _lines[index]);
                    return false;
                }
            }

            return true;
        }

        private static class RegexCollection
        {
            internal static readonly Regex EmptyLine = new Regex(@"^[\s\t]*$");
            internal static readonly Regex MultipleSpaces = new Regex(@"\s\s+");
            internal static readonly Regex Comments = new Regex(@"^[^;]*(?<com>;+.*)$");
            internal static readonly Regex Defines = new Regex(@"^.define\s+(?<alias>\$[A-Za-z0-9_]+)\s(?<dest>(?:r\d+|d\d+\.\d+|-?\d+[.]?\d*|r\d+\b|0x[0-9a-fA-F]+\b))$");
            internal static readonly Regex Requirements = new Regex(@"^.require\s+(?<type>registers|stack|devices|memory)\s+(?<val>\d+|0x[0-9a-fA-F]+\b)$"); 
            internal static readonly Regex Include = new Regex(@"^\.(?<opt>include|patch)\s+(?<src>.+\b)$"); 
            internal static readonly Regex Labels = new Regex(@"^(?<label>[A-Za-z_][A-Za-z0-9_]+)\s*:$"); 
            internal static readonly Regex LabelJumps = new Regex(@"^(?<opt>j|jal|beq|beqal|bneq|bneqal|bne|bneal|bgt|bgtal|bgte|bgteal|blt|bltal|blte|blteal)\s+(?<label>[A-Za-z_][A-Za-z0-9_]+\b).*$");
            internal static readonly Regex LabelRegisters = new Regex(@"^ra|r\d+$");
            
            internal static readonly Regex SleepOperations = new Regex(@"^(?<opt>sleep)\s+(?<opl>r\d+\b|[1-9]\d*\b|0x[0-9a-fA-F]+\d*\b)$");
            internal static readonly Regex JumpOperations = new Regex(@"^(?<opt>j|jal)\s+(?<opd>r\d+\b|ra|[1-9]\d*\b|0x[0-9a-fA-F]+\d*\b)$");
            internal static readonly Regex BranchingOperations = new Regex(@"^(?<opt>beq|beqal|bneq|bneqal|bne|bneal|bgt|bgtal|bgte|bgteal|blt|bltal|blte|blteal)\s+(?<opd>r\d+\b|ra|sp|[1-9]\d*\b|0x[0-9a-fA-F]+\b)\s+(?<opl>-?\d+[.]?\d*|r\d+\b|0x[0-9a-fA-F]+\b|ra|sp)\s+(?<opr>-?\d+[.]?\d*|r\d+\b|0x[0-9a-fA-F]+\b|ra|sp)$");
            internal static readonly Regex StackOperations = new Regex(@"^(?<opt>push|pop|peek)\s+(?<opd>r\d+\b|0x[0-9a-fA-F]+\b|ra|sp)$");
            internal static readonly Regex SelfOperations = new Regex(@"^(?<opt>nop|ret|die|yield)$");
            internal static readonly Regex DestinationOperations = new Regex(@"^(?<opt>inc|dec)\s+(?<opd>r\d+\b|ra|sp)$"); 
            internal static readonly Regex UnaryOperations = new Regex(@"^(?<opt>mov|sqrt|cos|sin|tan|acos|asin|atan|assert)\s+(?<opd>r\d+\b||ra|sp)\s+(?<opl>-?\d+[.]?\d*|r\d+\b|0x[0-9a-fA-F]+\b|ra|sp)$");
            internal static readonly Regex BinaryOperations = new Regex(@"^(?<opt>add|sub|mul|div|mod|pow|rnd|rndi|round|apx|min|max|eq|ne|neq|gt|gte|lt|lte)\s+(?<opd>r\d+\b|ra|sp)\s+(?<opl>-?\d+[.]?\d*|r\d+\b|0x[0-9a-fA-F]+\b|ra|sp)\s+(?<opr>-?\d+[.]?\d*|r\d+\b|0x[0-9a-fA-F]+\b|ra|sp)$");
            internal static readonly Regex ReadDeviceOperations = new Regex(@"^(?<opt>rdev|rd)\s+(?<opd>r\d+\b)\s+(?<opl>d\d+\.\d+)$");
            internal static readonly Regex WriteDeviceOperations = new Regex(@"^(?<opt>wdev|wd)\s+(?<opd>d\d+\.\d+\b)\s+(?<opl>r\d+\b|-?\d+[.]?\d*|r\d+\b|0x[0-9a-fA-F]+\b)$");

#if NATRIUM_FEATURE_MEMORY
            internal static readonly Regex AllocateMemory = new Regex(@"^(?<opt>malloc)\s+(?<opd>r\d+\b)\s+(?<opl>\d+)$"); 
            internal static readonly Regex FreeMemory = new Regex(@"^(?<opt>free)\s+(?<opd>r\d+\b)$");
#endif
        }
    }
}
