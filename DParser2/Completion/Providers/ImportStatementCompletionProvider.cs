using System.Collections.Generic;
using D_Parser.Dom;
namespace D_Parser.Completion.Providers
{
	/// <summary>
	/// import io = |;
	/// import |;
	/// </summary>
	class ImportStatementCompletionProvider : AbstractCompletionProvider
	{
		readonly ImportStatement.Import imp;

		public ImportStatementCompletionProvider(
			ICompletionDataGenerator gen, 
			ImportStatement.Import imp)
			: base(gen)
		{
			this.imp = imp;
		}

		protected override void BuildCompletionDataInternal(IEditorData Editor, char enteredChar)
		{
			if(Editor.ParseCache == null)
				return;

			string pack = null;

			if (imp.ModuleIdentifier != null && imp.ModuleIdentifier.InnerDeclaration != null)
			{
				pack = imp.ModuleIdentifier.InnerDeclaration.ToString();

				// Will occur after an initial dot  
				if (string.IsNullOrEmpty(pack))
					return;
			}

			foreach (var p in Editor.ParseCache.LookupPackage(pack))
			{
				if (p == null)
					continue;

				foreach (var kv_pack in p.Packages)
					CompletionDataGenerator.AddPackage(kv_pack.Value.Name);

				foreach (var kv_mod in p.Modules)
					CompletionDataGenerator.AddModule(kv_mod.Value);
			}
		}
	}
}
