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
				CompletionDataGenerator.AddTextItem(kv.Key, kv.Value);

			CompletionDataGenerator.AddTextItem("naked", "Omits function call's entry/exit instructions");
		}
	}
}

