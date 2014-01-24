using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Resolver.ASTScanner;
using System.Linq;
namespace D_Parser.Completion.Providers
{
	/// <summary>
	/// import std.stdio : |, foo = |;
	/// </summary>
	class SelectiveImportCompletionProvider : AbstractCompletionProvider
	{
		readonly ImportStatement.Import import;

		public SelectiveImportCompletionProvider(ICompletionDataGenerator gen, ImportStatement.Import imp) : base(gen) {
			import = imp;
		}

		protected override void BuildCompletionDataInternal(IEditorData Editor, char enteredChar)
		{
			if (Editor.ParseCache == null)
				return;

			var module = Editor.ParseCache.LookupModuleName(import.ModuleIdentifier.ToString(true)).FirstOrDefault();

			if (module == null)
				return;

			var ctxt = ResolutionContext.Create(Editor);

			/*
			 * Show all members of the imported module
			 * + public imports 
			 * + items of anonymous enums
			 */

			MemberCompletionEnumeration.EnumChildren(CompletionDataGenerator, ctxt, module, true, MemberFilter.All);

			return;
		}
	}
}
