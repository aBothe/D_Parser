using System;
using D_Parser.Dom.Statements;

namespace D_Parser.Completion.Providers
{
	class InlineAsmCompletionProvider : AbstractCompletionProvider
	{
		readonly AbstractStatement gs;

		public InlineAsmCompletionProvider (AbstractStatement gs,ICompletionDataGenerator gen) : base(gen)
		{
			this.gs = gs;
		}

		protected override void BuildCompletionDataInternal (IEditorData ed, char enteredChar)
		{
			foreach (var kv in AsmStatement.InstructionStatement.OpCodeCompletionTable)
			{
				switch (AsmStatement.InstructionStatement.OpCodeInstructionSets[kv.Key])
				{
					case AsmStatement.InstructionStatement.IS.SSE42:
						CompletionDataGenerator.AddIconItem("d-asm-x86-sse4.2", kv.Key, kv.Value);
						break;
					default:
						CompletionDataGenerator.AddTextItem(kv.Key, kv.Value);
						break;
				}
			}

			CompletionDataGenerator.AddTextItem("naked", "Omits function call's entry/exit instructions");
		}
	}
}

