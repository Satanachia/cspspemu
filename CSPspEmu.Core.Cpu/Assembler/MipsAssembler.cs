﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSPspEmu.Core.Cpu.Table;
using CSPspEmu.Core.Memory;
using CSPspEmu.Core.Utils;
using CSharpUtils.Arrays;
using CSharpUtils.Streams;

namespace CSPspEmu.Core.Cpu.Assembler
{
	public class MipsAssembler
	{
		protected Stream OutputStream;
		protected BinaryWriter BinaryWriter;
		protected BinaryReader BinaryReader;
		protected Dictionary<String, InstructionInfo> Instructions;

		public static IArray<Instruction> StaticAssembleInstructions(string Program)
		{
			var Memory = new MemoryStream();
			var Instructions = new StreamStructArrayWrapper<Instruction>(Memory);
			new MipsAssembler(Memory).Assemble(Program);
			return Instructions;
		}

		public MipsAssembler(Stream OutputStream)
		{
			this.Instructions = InstructionTable.ALL.ToDictionary((InstructionInfo) => InstructionInfo.Name);
			this.OutputStream = OutputStream;
			this.BinaryWriter = new BinaryWriter(this.OutputStream);
			this.BinaryReader = new BinaryReader(this.OutputStream);
		}

		protected static bool IsIdent(char C)
		{
			return ((C == '_') || (C == '%') || (C == '+') || (C == '-') || (C >= '0' && C <= '9') || (C >= 'a' && C <= 'z') || (C >= 'A' && C <= 'Z'));
		}

		protected static bool IsSpace(char C)
		{
			return ((C == ' ') || (C == '\t'));
		}

		/*
		public static IEnumerable<String> TokenizeRegex(String Line)
		{
			var Matches = new Regex(@"(\+\d+|-\d+|[%\w]+|\S)", RegexOptions.Compiled).Matches(Line);
			var Ret = new String[Matches.Count];
			for (int n = 0; n < Matches.Count; n++) Ret[n] = Matches[n].Value;
			return Ret;
		}
		*/

		public static IEnumerable<String> TokenizeFast(String Line)
		{
			var Parts = new List<String>();
			for (int n = 0; n < Line.Length; n++)
			{
				if (IsIdent(Line[n]))
				{
					int m = n;
					for (; n < Line.Length && IsIdent(Line[n]); n++) { }
					Parts.Add(Line.Substr(m, n - m));
					n--;
				}
				else
				{
					if (!IsSpace(Line[n]))
					{
						Parts.Add("" + Line[n]);
					}
				}
			}
			return Parts;
		}

		public static IEnumerable<String> Tokenize(String Line)
		{
			return TokenizeFast(Line);
		}


		public static List<Tuple<String, String>> MatchFormat(String Format, String Line)
		{
			var Matches = new List<Tuple<String, String>>();

			var FormatChunks = new Queue<String>(Tokenize(Format));
			var LineChunks = new Queue<String>(Tokenize(Line));

			while (FormatChunks.Count > 0 && LineChunks.Count > 0)
			{
				var CurrentFormat = FormatChunks.Dequeue();
				var CurrentLine = LineChunks.Dequeue();

				switch (CurrentFormat)
				{
					default:
						if (CurrentFormat[0] == '%')
						{
							Matches.Add(new Tuple<String, String>(CurrentFormat, CurrentLine));
						}
						else
						{
							if (CurrentLine != CurrentFormat) throw (new InvalidDataException());
						}
						break;
				}
			}

			if (LineChunks.Count > 0)
			{
				throw (new InvalidDataException("Unexpected token '" + LineChunks.Dequeue() + "' on '" + Line + "' for format '" + Format + "'"));
			}

			return Matches;
		}

		public static Dictionary<String, String> MatchFormatDictionary(String Format, String Line)
		{
			var Dictionary = new Dictionary<String, String>();
			foreach (var Pair in MatchFormat(Format, Line))
			{
				Dictionary[Pair.Item1] = Pair.Item2;
			}
			return Dictionary;
		}

		public static int ParseFprName(String RegisterName)
		{
			if (RegisterName[0] == 'f')
			{
				return Convert.ToInt32(RegisterName.Substring(1));
			}
			throw (new InvalidDataException());
		}

		public static int ParseGprName(String RegisterName)
		{
			if (RegisterName[0] == 'r')
			{
				return Convert.ToInt32(RegisterName.Substring(1));
			}
			throw(new InvalidDataException("Invalid Register Name '" + RegisterName + "'"));
		}

		public int ParseIntegerConstant(String Value)
		{
			return NumberUtils.ParseIntegerConstant(Value);
		}

		public Instruction AssembleInstruction(String Line)
		{
			return AssembleInstructions(Line)[0];
		}

		public enum PatchType
		{
			REL_16 = 0,
			ABS_26 = 1,
			ABS_32 = 2,
		}

		public class Patch {
			public uint Address;
			public PatchType Type;
			public String LabelName;
		}

		public Instruction[] AssembleInstructions(String Line)
		{
			uint PC = 0;
			return AssembleInstructions(ref PC, Line, null);
		}

		public Instruction[] AssembleInstructions(ref uint PC, String Line, List<Patch> Patches)
		{
			Line = Line.Trim();
			if (Line.Length == 0) return new Instruction[] {};
			var LineTokens = Line.Split(new char[] { ' ', '\t' }, 2);
			var InstructionName = LineTokens[0].ToLower();
			InstructionInfo InstructionInfo;
			if (Instructions.TryGetValue(InstructionName, out InstructionInfo))
			{
				var Instruction = new Instruction()
				{
					Value = InstructionInfo.Value & InstructionInfo.Mask,
				};
				var Matches = MatchFormat(InstructionInfo.AsmEncoding, (LineTokens.Length > 1) ? LineTokens[1] : "");
				foreach (var Match in Matches)
				{
					var Key = Match.Item1;
					var Value = Match.Item2;

					switch (Key)
					{
						case "%S": Instruction.FS = ParseFprName(Value); break;
						case "%D": Instruction.FD = ParseFprName(Value); break;
						case "%T": Instruction.FT = ParseFprName(Value); break;

						case "%J":
						case "%s": Instruction.RS = ParseGprName(Value); break;
						case "%d": Instruction.RD = ParseGprName(Value); break;
						case "%t": Instruction.RT = ParseGprName(Value); break;

						case "%a": Instruction.POS = (uint)ParseIntegerConstant(Value); break;
						case "%ne": Instruction.SIZE_E = (uint)ParseIntegerConstant(Value); break;
						case "%ni": Instruction.SIZE_I = (uint)ParseIntegerConstant(Value); break;

						case "%p": Instruction.RD = (int)ParseIntegerConstant(Value); break;

						case "%C": Instruction.CODE = (uint)ParseIntegerConstant(Value); break;
						case "%i": Instruction.IMM = ParseIntegerConstant(Value); break;
						case "%I": Instruction.IMMU = (uint)ParseIntegerConstant(Value); break;

						case "%j": Patches.Add(new Patch() { Address = PC, LabelName = Value, Type = PatchType.ABS_26 }); break;
						case "%O": Patches.Add(new Patch() { Address = PC, LabelName = Value, Type = PatchType.REL_16 }); break;

						default: throw (new InvalidDataException("Unknown format '" + Key + "' <-- (" + InstructionInfo.AsmEncoding + ")"));
					}
				}
				/*
				if ((InstructionInfo.InstructionType & InstructionType.B) != 0)
				{
					//Patches.Add(new Patch() { Address = PC, LabelName =  });
				}
				else if ((InstructionInfo.InstructionType & InstructionType.Jump) != 0)
				{
				}
				*/
				PC += 4;
				return new Instruction[] { Instruction };
			}
			else
			{
				switch (InstructionName)
				{
					case "nop":
					{
						//return AssembleInstructions(ref PC, "sll r0, r0, r0");
						return AssembleInstructions(ref PC, "and r0, r0, r0", Patches);
					}
					case "b":
					{
						var Info = MatchFormatDictionary("%O", LineTokens[1]);
						return AssembleInstructions(ref PC, "beq r0, r0, " + Info["%O"], Patches);
					}
					case "li":
					{
						var Info = MatchFormatDictionary("%d, %i", LineTokens[1]);
						var DestReg = Info["%d"];
						var Value = ParseIntegerConstant(Info["%i"]);
						// Needs LUI
						if ((short)Value != Value)
						{
							var List = new List<Instruction>();
							List.AddRange(AssembleInstructions(ref PC, "lui " + DestReg + ", " + ((Value >> 16) & 0xFFFF), Patches));
							List.AddRange(AssembleInstructions(ref PC, "ori " + DestReg + ", " + DestReg + ", " + (Value & 0xFFFF), Patches));
							//Console.WriteLine(List.ToJson());
							return List.ToArray();
						}
						else
						{
							return AssembleInstructions(ref PC, "addi " + DestReg + ", r0, " + Value, Patches);
						}
					}
					default:
						throw (new InvalidOperationException("Unknown instruction type '" + InstructionName + "'"));
				}
			}
		}

		public void Assemble(String Lines)
		{
			var Labels = new Dictionary<String, uint>();
			var Patches = new List<Patch>();

			foreach (var Line in Lines.Split(new char[] { '\n' }).Select(Str => Str.Trim()).Where(Str => Str.Length > 0))
			{
				// Strip comments.
				var Parts = Line.Split(new string[] { ";", "#" }, 2, StringSplitOptions.None);
				var RealLine = Parts[0].Trim();

				// Directive
				if (Line[0] == '.')
				{
					var LineTokens = Line.Split(new char[] { ' ', '\t' }, 2);
					switch (LineTokens[0])
					{
						case ".code":
							OutputStream.Position = ParseIntegerConstant(LineTokens[1]);
							break;
						default:
							throw (new NotImplementedException("Unsupported directive '" + LineTokens[0] + "'"));
					}
				}
				else
				{
					// Label
					if (RealLine.Substr(-1) == ":")
					{
						Labels[RealLine.Substr(0, -1).Trim()] = (uint)OutputStream.Position;
					}
					// Instruction
					else
					{
						uint PC = (uint)OutputStream.Position;
						var Instructions = AssembleInstructions(ref PC, RealLine, Patches);
						foreach (var Instruction in Instructions)
						{
							BinaryWriter.Write(Instruction.Value);
						}
					}
				}
			}

			foreach (var Patch in Patches)
			{
				if (Labels.ContainsKey(Patch.LabelName))
				{
					var LabelAddress = Labels[Patch.LabelName];
					Instruction Instruction;

					OutputStream.Position = Patch.Address; Instruction = (Instruction)BinaryReader.ReadUInt32();
					{
						switch (Patch.Type)
						{
							case PatchType.REL_16:
								Instruction.IMM = ((int)LabelAddress - (int)Patch.Address - 4) / 4;
								break;
							case PatchType.ABS_26:
								Console.Write(String.Format("0x{0:X} : {1}", (LabelAddress & PspMemory.MemoryMask) / 4, Patch.LabelName));
								Instruction.JUMP = (LabelAddress & PspMemory.MemoryMask) / 4;
								break;
							case PatchType.ABS_32:
								Instruction.Value = LabelAddress;
								break;
						}
					}
					OutputStream.Position = Patch.Address; BinaryWriter.Write(Instruction.Value);
				}
				else
				{
					throw(new KeyNotFoundException("Can't find label '" + Patch.LabelName + "'"));
				}
			}
		}
	}
}
