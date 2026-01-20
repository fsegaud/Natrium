using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Hasm
{
    public class Compiler
    {
        private readonly List<Instruction> _instructions = new List<Instruction>();
        private readonly Dictionary<string, uint> _labelToLine = new Dictionary<string, uint>();
        
        private string[] _lines = {};
        private bool[] _skipLine = {};

        private DebugData _debugData;
        private Action<string>? _debugCallback;
        
        public  Result LastError { get; private set; }
        
        public Program? Compile(string input, Action<string>? debugCallback = null, DebugData debugData = DebugData.None)
        {
            _debugData = debugData;
            _debugCallback = debugCallback;
            
            _lines = input.Split('\n');
            _skipLine = new bool[_lines.Length];
            _instructions.Clear();
            _labelToLine.Clear();

            Program program = new Program();
            
            // First pass.
            bool succeed = true;
            for (var index = 0u; index < _lines.Length && succeed; index++)
            {
                succeed &= Check(ParseSpaceAndTabs(index));
                succeed &= Check(ParseComments(index));
                succeed &= Check(ParseRequirements(index, ref program));
                succeed &= Check(PreParseLabels(index));
            }

            if (!succeed)
                return null;
            
            // Second pass.
            for (var index = 0u; index < _lines.Length && succeed; index++)
            {
                succeed &= Check(ParseLabels(index));
                succeed &= Check(ParseJump(index));
                succeed &= Check(ParseStack(index));
                succeed &= Check(ParseSelfOperations(index));
                succeed &= Check(ParseUnaryOperations(index));
                succeed &= Check(ParseBinaryOperations(index));
            }
            
            if (!succeed)
                return null;
            
            // Final pass.
            FixRetInstruction();
            if (!CheckIfAllInstructionProcessed())
                return null;
            
            // Post-compile.
            program.Instructions = _instructions.ToArray();
            LastError = Result.Success();
            
            if ((debugData & DebugData.Binary) > 0)
                debugCallback?.Invoke("compiler > " + program.ToBase64());
            
            if ((debugData & DebugData.Separator) > 0)
                debugCallback?.Invoke("-------------------------------------------------------------------------" +
                                      "-----------------------------------------------");
            
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
                
            _lines[index] =  _lines[index].Trim();
                
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

        private bool ParseRequirements(uint index, ref Program program)
        {
            if (_skipLine[index])
                return true;
                
            Match match = RegexCollection.Requirements.Match(_lines[index]);
            if (match.Success)
            {
                string type =  match.Groups["type"].Value;
                uint value = uint.Parse(match.Groups["val"].Value);
                    
                switch (type)
                {
                    case "r" : program.RequiredRegisters = value; break;
                    case "s" : program.RequiredStack = value; break;
                    default: throw new NotSupportedException();
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
                uint jumpIndex = index + 2;
                    
                _labelToLine.Add(label, jumpIndex);
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
                string opt = match.Groups["opt"].Value;
                string label = match.Groups["label"].Value;
                    
                if (!RegexCollection.LabelRegisters.Match(label).Success)
                {
                    if (!_labelToLine.TryGetValue(label, out var jumpLine))
                    {
                        LastError = new Result(Error.LabelNotFound, index + 1, _lines[index]);
                        return false;
                    }

                    _lines[index] = $"{opt} {jumpLine}";
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
                instruction.RawText = _lines[index];
                instruction.Line = index + 1;
                    
                switch (opt)
                {
                    case "nop": instruction.Operation = Operation.Nop; break;
                    case "ret": instruction.Operation = Operation.Ret; break;
                    default:
                    {
                        LastError = new Result(Error.OperationNotSupported, instruction);
                        return false;
                    }
                }

                _skipLine[index] = true;
                _instructions.Add(instruction);
                LogDebug(instruction);
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
                string opl = match.Groups["opl"].Value;
                
                Instruction instruction = default;
                instruction.RawText = _lines[index];
                instruction.Line = index + 1;
                
                switch (opt)
                {
                    case "j": instruction.Operation = Operation.Jump; break;
                    case "jra": instruction.Operation = Operation.JumpReturnAddress; break;
                    default:
                    {
                        LastError = new Result(Error.OperationNotSupported, instruction);
                        return false;
                    }
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
                    instruction.LeftOperandType = Instruction.OperandType.UserRegistry;
                    instruction.LeftOperandValue = int.Parse(opl.Substring(1));
                }
                else
                {
                    instruction.LeftOperandType = Instruction.OperandType.Literal;
                    instruction.LeftOperandValue = float.Parse(opl, CultureInfo.InvariantCulture);
                }
                
                _skipLine[index] = true;
                _instructions.Add(instruction);
                LogDebug(instruction);
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
                instruction.RawText = _lines[index];
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
                    instruction.DestinationRegistryType = Instruction.OperandType.UserRegistry;
                    instruction.DestinationRegistry = uint.Parse(opd.Substring(1));
                }
                    
                _skipLine[index] = true;
                _instructions.Add(instruction);
                LogDebug(instruction);
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
                instruction.RawText = _lines[index];
                instruction.Line = index + 1;
                
                switch (opt)
                {
                    case "mov": instruction.Operation = Operation.Move; break;
                    case "sqrt": instruction.Operation = Operation.SquareRoot; break;
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
                else
                {
                    instruction.DestinationRegistryType = Instruction.OperandType.UserRegistry;
                    instruction.DestinationRegistry = uint.Parse(opd.Substring(1));
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
                    instruction.LeftOperandType = Instruction.OperandType.UserRegistry;
                    instruction.LeftOperandValue = int.Parse(opl.Substring(1));
                }
                else
                {
                    instruction.LeftOperandType = Instruction.OperandType.Literal;
                    instruction.LeftOperandValue = float.Parse(opl, CultureInfo.InvariantCulture);
                }
                
                _skipLine[index] = true;
                _instructions.Add(instruction);
                LogDebug(instruction);
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
                instruction.RawText = _lines[index];
                instruction.Line = index + 1u;
                
                switch (opt)
                {
                    case "add": instruction.Operation = Operation.Add; break;
                    case "sub": instruction.Operation = Operation.Subtract; break;
                    case "mul": instruction.Operation = Operation.Multiply; break;
                    case "div": instruction.Operation = Operation.Divide; break;
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
                    instruction.DestinationRegistryType = Instruction.OperandType.UserRegistry;
                    instruction.DestinationRegistry = uint.Parse(opd.Substring(1));
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
                    instruction.LeftOperandType = Instruction.OperandType.UserRegistry;
                    instruction.LeftOperandValue = int.Parse(opl.Substring(1));
                }
                else
                {
                    instruction.LeftOperandType = Instruction.OperandType.Literal;
                    instruction.LeftOperandValue = float.Parse(opl, CultureInfo.InvariantCulture);
                }

                if (opr[0] == 'r')
                {
                    instruction.RightOperandType = Instruction.OperandType.UserRegistry;
                    instruction.RightOperandValue = int.Parse(opr.Substring(1));
                }
                else
                {
                    instruction.RightOperandType = Instruction.OperandType.Literal;
                    instruction.RightOperandValue = float.Parse(opr, CultureInfo.InvariantCulture);
                }

                _skipLine[index] = true;
                _instructions.Add(instruction);
                LogDebug(instruction);
            }

            return true;
        }

        private void FixRetInstruction()
        {
            // Special 'ret' instruction at end.
            if (_instructions.Count == 0 || _instructions[^1].Operation != Operation.Ret)
            {
                Instruction instruction = new Instruction()
                {
                    RawText = "ret",
                    Operation = Operation.Ret,
                    Line = _instructions.Count == 0 ? 0 : _instructions[^1].Line + 3
                };
                
                _instructions.Add(instruction);
                LogDebug(instruction);
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
        
        private void LogDebug(Instruction instruction)
        {
            if ((_debugData & DebugData.RawInstruction) > 0)
                _debugCallback?.Invoke("compiler > " + instruction.RawText);
                    
            if ((_debugData & DebugData.CompiledInstruction) > 0)
                _debugCallback?.Invoke("compiler > " + instruction);
                    
            if ((_debugData & DebugData.Separator) > 0)
                _debugCallback?.Invoke("-------------------------------------------------------------------------" +
                                       "-----------------------------------------------");
        }

        private static class RegexCollection
        {
            internal static readonly Regex EmptyLine = new Regex(@"^[\s\t]*$");
            internal static readonly Regex Comments = new Regex(@".*(?<com>[;#].*)");
            internal static readonly Regex Requirements = new Regex(@"^@req:(?<type>r|s)(?<val>\d+)"); 
            
            internal static readonly Regex Labels = new Regex(@"^(?<label>[A-Za-z_]+)\s*:$"); 
            internal static readonly Regex LabelJumps = new Regex(@"^(?<opt>j|jra)\s+(?<label>[A-Za-z_]+\b)$");
            internal static readonly Regex LabelRegisters = new Regex(@"^ra|r\d+$");
            
            internal static readonly Regex SelfOperations = new Regex(@"^(?<opt>nop|ret)$"); 
            internal static readonly Regex JumpOperations = new Regex(@"^(?<opt>j|jra)\s+(?<opl>r\d+\b|ra|sp|[1-9]\d*\b)$");
            internal static readonly Regex StackOperations = new Regex(@"^(?<opt>push|pop|peek)\s+(?<opd>r\d+\b|ra|sp)$"); 
            internal static readonly Regex UnaryOperations = new Regex(@"^(?<opt>mov|sqrt|assert)\s+(?<opd>r\d+\b|ra|sp)\s+(?<opl>-?\d+[.]?\d*|r\d+\b|ra|sp)$"); 
            internal static readonly Regex BinaryOperations = new Regex(@"^(?<opt>add|sub|mul|div)\s+(?<opd>r\d+\b|ra|sp)\s+(?<opl>-?\d+[.]?\d*|r\d+\b|ra|sp)\s+(?<opr>-?\d+[.]?\d*|r\d+\b|ra|sp)$"); 
        }
    }
}
