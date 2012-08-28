using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Completion;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Refactoring
{
	public class ImportGen
	{

		public static INode[] TryFindingSelectedIdImportIndependently(IEditorData ed)
		{
			var l = new List<INode>();

			var ctxt = new ResolverContextStack(ed.ParseCache, new ResolverContext { ScopedBlock = ed. });

			var o = DResolver.GetScopedCodeObject(ed, ctxt, DResolver.AstReparseOptions.AlsoParseBeyondCaret);

			/*
			 * - Get scoped object.
			 * - Try to resolve it using the usual (strictly filtered) way.
			 * - If no results:
			 * - Extract a concrete id from that syntax object. (If access expression/nested decl, use the inner-most one)
			 * - Rawly scan through all modules' roots of the parse cache to find that id. (Later on, mind mixins and protection attributes (etc?))
			 * - Enlist and return all matches - the remaining high-level part is done in the IDE.
			 */

			return l.ToArray();
		}
	}
}
