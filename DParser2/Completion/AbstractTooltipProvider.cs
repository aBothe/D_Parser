using System.Collections.Generic;
using D_Parser.Dom.Statements;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Dom;

namespace D_Parser.Completion
{
	/// <summary>
	/// Encapsules tooltip content.
	/// If there are more than one tooltip contents, there are more than one resolve results
	/// </summary>
	public class AbstractTooltipContent
	{
		public ISemantic ResolveResult;
		public string Title;
		public string Description;
	}

	public class AbstractTooltipProvider
	{
		public static List<AbstractTooltipContent> BuildToolTip(IEditorData Editor)
		{
			DResolver.NodeResolutionAttempt att;
			var rr = DResolver.ResolveTypeLoosely(Editor, out att);

			if (rr == null || rr.Length < 1)
				return null;

			var l = new List<AbstractTooltipContent>();
			foreach (var res in rr)
				l.Add(BuildTooltipContent(res));

			return l;
		}

		static AbstractTooltipContent BuildTooltipContent(ISemantic res)
		{
			// Only show one description for items sharing descriptions
			var ds = res as DSymbol;
			var description = ds != null ? ds.Definition.Description : "";

			return new AbstractTooltipContent
			{
				ResolveResult = res,
				Title = BuildTooltipTitle(res),
				Description = description
			};
		}

		static string BuildTooltipTitle(ISemantic res)
		{
			if (res is TypeValue)
				res = (res as TypeValue).RepresentedType;
			else if (res is ISymbolValue) {
				var sv = res as ISymbolValue;
				return "(" + BuildTooltipTitle(sv.RepresentedType) + ") " + sv.ToCode ();
			}
			else if (res is PackageSymbol)
				return "(Package) " + (res as PackageSymbol).Package.ToString ();

			var ds = res as DSymbol;
			if (ds == null)
				return string.Empty;

			if (ds is ModuleSymbol)
				return "(Module) " + (ds as ModuleSymbol).Definition.FileName;
				
			var bt = DResolver.StripMemberSymbols (ds.Base);
			if ((ds is MemberSymbol || ds is TemplateParameterSymbol) && bt != null) {
				return string.Format("{1}\r\n(Deduced Type: {0})", bt.ToString(), ds.Definition.ToString());
			} else
				return ds.ToCode ();
		}
	}
}
