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

			foreach (var desc in AsmInstructionStatement.OpCodeMap.Values)
			{
				string icon = "d-asm-";
				if (desc.Is64BitOnly)
					icon += "x64";
				else
					icon += "x86";

				switch (desc.InstructionSet)
				{
					case AsmInstructionStatement.IS.X86:
						break;
					case AsmInstructionStatement.IS.FPU:
						icon += "-fpu";
						break;
					case AsmInstructionStatement.IS.MMX:
						icon += "-mmx";
						break;
					case AsmInstructionStatement.IS.SSE:
						icon += "-sse";
						break;
					case AsmInstructionStatement.IS.SSE2:
						icon += "-sse2";
						break;
					case AsmInstructionStatement.IS.SSE3:
						icon += "-sse3";
						break;
					case AsmInstructionStatement.IS.SSSE3:
						icon += "-ssse3";
						break;
					case AsmInstructionStatement.IS.SSE41:
						icon += "-sse4.1";
						break;
					case AsmInstructionStatement.IS.SSE42:
						icon += "-sse4.2";
						break;
					case AsmInstructionStatement.IS.AVX:
						icon += "-avx";
						break;
					default:
						throw new Exception("Unknown instruction set! Woops!");
				}

				dt.Add(new ItemData(string.Intern(icon), desc.Name, desc.Description));
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

