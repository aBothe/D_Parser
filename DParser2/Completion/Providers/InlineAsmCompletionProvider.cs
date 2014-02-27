using System;
using D_Parser.Dom.Statements;
using System.Collections.Generic;

namespace D_Parser.Completion.Providers
{
	class InlineAsmCompletionProvider : AbstractCompletionProvider
	{
		private struct ItemData
		{
			public readonly string IconID;
			public readonly string DisplayName;
			public readonly string Description;

			public ItemData(string icon, string name, string description)
			{
				this.IconID = icon;
				this.DisplayName = name;
				this.Description = description;
			}
		}
		private static readonly ItemData[] completionItemData;

		static InlineAsmCompletionProvider()
		{
			var dt = new List<ItemData>();

			foreach (var kv in AsmStatement.InstructionStatement.OpCodeCompletionTable)
			{
				string icon = "d-asm-";
				if (AsmStatement.InstructionStatement.OpCode64BitOnly[kv.Key])
					icon += "x64";
				else
					icon += "x86";

				switch (AsmStatement.InstructionStatement.OpCodeInstructionSets[kv.Key])
				{
					case AsmStatement.InstructionStatement.IS.X86:
						break;
					case AsmStatement.InstructionStatement.IS.FPU:
						icon += "-fpu";
						break;
					case AsmStatement.InstructionStatement.IS.MMX:
						icon += "-mmx";
						break;
					case AsmStatement.InstructionStatement.IS.SSE:
						icon += "-sse";
						break;
					case AsmStatement.InstructionStatement.IS.SSE2:
						icon += "-sse2";
						break;
					case AsmStatement.InstructionStatement.IS.SSE3:
						icon += "-sse3";
						break;
					case AsmStatement.InstructionStatement.IS.SSSE3:
						icon += "-ssse3";
						break;
					case AsmStatement.InstructionStatement.IS.SSE41:
						icon += "-sse4.1";
						break;
					case AsmStatement.InstructionStatement.IS.SSE42:
						icon += "-sse4.2";
						break;
					case AsmStatement.InstructionStatement.IS.AVX:
						icon += "-avx";
						break;
					default:
						throw new Exception("Unknown instruction set! Woops!");
				}

				dt.Add(new ItemData(string.Intern(icon), kv.Key, kv.Value));
			}
			dt.Add(new ItemData("md-keyword", "align", "Inserts nops such that the start of the next instruction lies on an N-byte boundary."));
			dt.Add(new ItemData("md-keyword", "even", "Inserts nops such that the next instruction begins on an even byte boundary."));
			dt.Add(new ItemData("md-keyword", "naked", "Omits the function's entry/exit instructions."));

			dt.Add(new ItemData("md-keyword", "db", "Inserts raw 8-bit words into the instruction stream."));
			dt.Add(new ItemData("md-keyword", "ds", "Inserts raw 16-bit words into the instruction stream."));
			dt.Add(new ItemData("md-keyword", "di", "Inserts raw 32-bit words into the instruction stream."));
			dt.Add(new ItemData("md-keyword", "dl", "Inserts raw 64-bit words into the instruction stream."));
			dt.Add(new ItemData("md-keyword", "df", "Inserts raw 32-bit floats into the instruction stream."));
			dt.Add(new ItemData("md-keyword", "dd", "Inserts raw 64-bit doubles into the instruction stream."));
			dt.Add(new ItemData("md-keyword", "de", "Inserts raw 80-bit extended reals into the instruction stream."));

			completionItemData = dt.ToArray();
		}

		readonly AbstractStatement gs;

		public InlineAsmCompletionProvider (AbstractStatement gs,ICompletionDataGenerator gen) : base(gen)
		{
			this.gs = gs;
		}

		protected override void BuildCompletionDataInternal (IEditorData ed, char enteredChar)
		{
			foreach (var v in completionItemData)
				CompletionDataGenerator.AddIconItem(v.IconID, v.DisplayName, v.Description);
		}
	}
}

