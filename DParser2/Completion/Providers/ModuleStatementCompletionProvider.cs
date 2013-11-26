using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Parser;

namespace D_Parser.Completion
{
	class ModuleStatementCompletionProvider : AbstractCompletionProvider
	{
		public ModuleStatementCompletionProvider(ICompletionDataGenerator dg) : base(dg){}
		
		protected override void BuildCompletionDataInternal(IEditorData Editor, char enteredChar)
		{
			CompletionDataGenerator.AddModule(Editor.SyntaxTree);
		}
	}
}
