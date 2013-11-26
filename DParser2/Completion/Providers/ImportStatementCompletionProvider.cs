using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Resolver;
using D_Parser.Resolver.ASTScanner;

namespace D_Parser.Completion.Providers
{
	class ImportStatementCompletionProvider : AbstractCompletionProvider
	{
		readonly ImportStatement.Import imp;
		readonly ImportStatement.ImportBindings impBind;

		public ImportStatementCompletionProvider(
			ICompletionDataGenerator gen, 
			ImportStatement.Import imp)
			: base(gen)
		{
			this.imp = imp;
		}

		public ImportStatementCompletionProvider(
			ICompletionDataGenerator gen, 
			ImportStatement.ImportBindings imbBind)
			: base(gen)
		{
			this.impBind = imbBind;
			imp = impBind.Module;
		}

		protected override void BuildCompletionDataInternal(IEditorData Editor, char enteredChar)
		{
			if(Editor.ParseCache == null)
				return;

			if (impBind != null)
			{
				DModule mod = null;

				var modName = imp.ModuleIdentifier.ToString (true);
				foreach (var pc in Editor.ParseCache)
					if ((mod = pc.GetModule (modName)) != null)
						break;

				if (mod == null)
					return;

				var ctxt = ResolutionContext.Create (Editor);

				/*
				 * Show all members of the imported module
				 * + public imports 
				 * + items of anonymous enums
				 */

				MemberCompletionEnumeration.EnumChildren(CompletionDataGenerator, ctxt, mod, true, MemberFilter.All);

				return;
			}


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
