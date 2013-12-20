
namespace D_Parser.Completion.Providers
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
