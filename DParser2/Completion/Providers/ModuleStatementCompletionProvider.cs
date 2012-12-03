using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Parser;

namespace D_Parser.Completion
{
	internal class ModuleStatementCompletionProvider : AbstractCompletionProvider
	{
		public ModuleStatementCompletionProvider(ICompletionDataGenerator dg) : base(dg){}
		
		protected override void BuildCompletionDataInternal(IEditorData Editor, string EnteredText)
		{
			CompletionDataGenerator.Add(Editor.SyntaxTree.ModuleName, Editor.SyntaxTree, "");
		}
	}
}
