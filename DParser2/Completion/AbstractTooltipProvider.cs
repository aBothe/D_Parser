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
		public static AbstractTooltipContent[] BuildToolTip(IEditorData Editor)
		{
			try
			{
				var l = new List<AbstractTooltipContent>();

				var ctxt=ResolutionContext.Create(Editor);
				var o = DResolver.GetScopedCodeObject(Editor, ctxt, DResolver.AstReparseOptions.AlsoParseBeyondCaret);

				var x = o as IExpression;
				AbstractType[] rr = null;
				if (x != null)
				{
					var v = Evaluation.EvaluateValue(x, ctxt);
					if(v!=null && !(v is ErrorValue))
						l.Add(BuildTooltipContent(v));
					else
					  rr = Evaluation.EvaluateTypes((IExpression)o, ctxt);
				}
				else if(o is ITypeDeclaration)
					rr = TypeDeclarationResolver.Resolve((ITypeDeclaration)o, ctxt);

				if (rr != null)
					foreach (var res in rr)
						l.Add(BuildTooltipContent(res));

				return l.Count == 0 ? null : l.ToArray();
			}
			catch { }
			return null;
		}

		static AbstractTooltipContent BuildTooltipContent(ISemantic res)
		{
			if (res is ISymbolValue) {
				var sv = res as ISymbolValue;

				return new AbstractTooltipContent { 
					ResolveResult = res,
					Title = "(" + sv.RepresentedType + ") "+sv.ToCode()
				};
			}

			// Only show one description for items sharing descriptions
			string description = res is DSymbol ? ((DSymbol)res).Definition.Description : "";

			return new AbstractTooltipContent
			{
				ResolveResult = res,
				Title = (res is ModuleSymbol ? ((ModuleSymbol)res).Definition.FileName : res.ToString()),
				Description = description
			};
		}
	}
}
