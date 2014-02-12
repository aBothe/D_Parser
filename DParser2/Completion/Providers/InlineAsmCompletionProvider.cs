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
				if(!kv.Key.StartsWith("__")) // No internal pseudo-opcodes
					CompletionDataGenerator.AddTextItem(kv.Key, kv.Value);
			}
		}
	}
}

