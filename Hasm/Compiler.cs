using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Hasm
{
    public class Compiler
    {
        public Result Compile(string input, ref Program program, Action<string>? debugCallback = null, DebugData debugData = DebugData.None)
        {
            List<Instruction> instructions = new List<Instruction>();
            Dictionary<string, uint> labelToLine = new Dictionary<string, uint>();
            
            string[] lines = input.Split('\n');

            for (var index = 0u; index < lines.Length; index++)
            {
                // Trim.
                
                lines[index] =  lines[index].Trim();
                if (string.IsNullOrEmpty(lines[index]))
                    continue;
                
                // Comments.

                Regex regex = new Regex(@".*(?<com>[;#].*)"); // TODO: One Regex object per expression.
                Match match = regex.Match(lines[index]);

                if (match.Success)
                {
                    lines[index] = lines[index].Replace(match.Groups["com"].Value, string.Empty).TrimEnd();

                    if (string.IsNullOrEmpty(lines[index]))
                        continue;
                }
                
                // Requires.
                
                regex = new Regex(@"^@req:(?<type>r|s)(?<val>\d+)");
                match = regex.Match(lines[index]);

                if (match.Success)
                {
                    string type =  match.Groups["type"].Value;
                    uint value = uint.Parse(match.Groups["val"].Value);
                    
                    switch (type)
                    {
                        case "r" : program.RequiredRegisters = value; break;
                        case "s" : program.RequiredStack = value; break;
                        default: throw new NotImplementedException();
                    }
                    
                    lines[index] = string.Empty; // Consume.
                    continue;
                }
                
                // Pre-parse: labels(1).
                
                regex = new Regex(@"^(?<label>[A-Za-z_]+)\s*:$");
                match = regex.Match(lines[index]);

                if (match.Success)
                {
                    string label = match.Groups["label"].Value;
                    uint jumpIndex = index + 2;
                    
                    labelToLine.Add(label, jumpIndex);
                    lines[index] = string.Empty; // Consume.
                }
            }
            
            for (var index = 0u; index < lines.Length; index++)
            {
                // Empty lines.
                if (string.IsNullOrEmpty(lines[index]))
                    continue;

                Regex regex = new Regex(@"^[\s\t]*$");
                Match match = regex.Match(lines[index]);

                if (match.Success)
                    continue;
                
                // Pre-parse: labels(2).
                
                regex = new Regex(@"^(?<opt>j|jra)\s+(?<label>[A-Za-z_]+\b)$");
                match = regex.Match(lines[index]);

                if (match.Success)
                {
                    string opt = match.Groups["opt"].Value;
                    string label = match.Groups["label"].Value;

                    regex = new Regex(@"^ra|r\d+$"); // registries are not labels.
                    if (!regex.Match(label).Success)
                    {
                        if (!labelToLine.TryGetValue(label, out var jumpLine))
                            return new Result(Error.LabelNotFound, index + 1, lines[index]);

                        lines[index] = $"{opt} {jumpLine}";
                    }

                    // Do not continue, instruction will be processed later.
                }
                
                // Self operations.

                regex = new Regex(@"^(?<opt>nop|ret)$");
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
                        case "ret": instruction.Operation = Operation.Ret; break;
                        default: return new Result(Error.OperationNotSupported, instruction);
                    }

                    instructions.Add(instruction);
                    
                    if ((debugData & DebugData.RawInstruction) > 0)
                        debugCallback?.Invoke("compiler > " + instruction.RawText);
                    
                    if ((debugData & DebugData.CompiledInstruction) > 0)
                        debugCallback?.Invoke("compiler > " + instruction);
                    
                    if ((debugData & DebugData.Separator) > 0)
                        debugCallback?.Invoke("-------------------------------------------------------------------------" +
                                              "-----------------------------------------------");

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
                    
                    instructions.Add(instruction);
                    
                    if ((debugData & DebugData.RawInstruction) > 0)
                        debugCallback?.Invoke("compiler > " + instruction.RawText);
                    
                    if ((debugData & DebugData.CompiledInstruction) > 0)
                        debugCallback?.Invoke("compiler > " + instruction);
                    
                    if ((debugData & DebugData.Separator) > 0)
                        debugCallback?.Invoke("-------------------------------------------------------------------------" +
                                              "-----------------------------------------------");

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
                    
                    instructions.Add(instruction);
                    
                    if ((debugData & DebugData.RawInstruction) > 0)
                        debugCallback?.Invoke("compiler > " + instruction.RawText);
                    
                    if ((debugData & DebugData.CompiledInstruction) > 0)
                        debugCallback?.Invoke("compiler > " + instruction);
                    
                    if ((debugData & DebugData.Separator) > 0)
                        debugCallback?.Invoke("-------------------------------------------------------------------------" +
                                              "-----------------------------------------------");

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
                    
                    instructions.Add(instruction);
                    
                    if ((debugData & DebugData.RawInstruction) > 0)
                        debugCallback?.Invoke("compiler > " + instruction.RawText);
                    
                    if ((debugData & DebugData.CompiledInstruction) > 0)
                        debugCallback?.Invoke("compiler > " + instruction);
                    
                    if ((debugData & DebugData.Separator) > 0)
                        debugCallback?.Invoke("-------------------------------------------------------------------------" +
                                              "-----------------------------------------------");

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

                    instructions.Add(instruction);
                    
                    if ((debugData & DebugData.RawInstruction) > 0)
                        debugCallback?.Invoke("compiler > " + instruction.RawText);
                    
                    if ((debugData & DebugData.CompiledInstruction) > 0)
                        debugCallback?.Invoke("compiler > " + instruction);
                    
                    if ((debugData & DebugData.Separator) > 0)
                        debugCallback?.Invoke("-------------------------------------------------------------------------" +
                                              "-----------------------------------------------");

                    continue;
                }

                return new Result(Error.SyntaxError, index + 1, lines[index]);
            }
            
            // Special 'ret' instruction at end.
            if (instructions.Count == 0 || instructions[^1].Operation != Operation.Ret)
            {
                Instruction instruction = new Instruction()
                {
                    RawText = "ret",
                    Operation = Operation.Ret,
                    Line = instructions.Count == 0 ? 0 : instructions[^1].Line + 3
                };
                    
                instructions.Add(instruction);
                
                if ((debugData & DebugData.RawInstruction) > 0)
                    debugCallback?.Invoke("compiler > " + instruction.RawText);
                    
                if ((debugData & DebugData.CompiledInstruction) > 0)
                    debugCallback?.Invoke("compiler > " + instruction);
                    
                if ((debugData & DebugData.Separator) > 0)
                    debugCallback?.Invoke("-------------------------------------------------------------------------" +
                                          "-----------------------------------------------");
            }

            program.Instructions = instructions.ToArray();
            
            if ((debugData & DebugData.Binary) > 0)
                debugCallback?.Invoke("compiler > " + program.ToBase64());
            
            if ((debugData & DebugData.Separator) > 0)
                debugCallback?.Invoke("-------------------------------------------------------------------------" +
                                      "-----------------------------------------------");

            return Result.Success();
        }
    }
}