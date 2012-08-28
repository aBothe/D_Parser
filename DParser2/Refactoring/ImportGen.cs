using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Completion;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Parser;

namespace D_Parser.Refactoring
{
	public class ImportGen
	{

		public static INode[] TryFindingSelectedIdImportIndependently(IEditorData ed, out bool importRequired)
		{
			importRequired = false;
			var l = new List<INode>();

			var ctxt = new ResolverContextStack(ed.ParseCache, new ResolverContext())
			{
				ContextIndependentOptions = ResolutionOptions.ReturnMethodReferencesOnly | 
				ResolutionOptions.DontResolveBaseTypes | 
				ResolutionOptions.DontResolveAliases | ResolutionOptions.NoTemplateParameterDeduction
			};
			ctxt.ScopedBlock = DResolver.SearchBlockAt(ed.SyntaxTree, ed.CaretLocation, out ctxt.CurrentContext.ScopedStatement);

			// Get scoped object.
			var o = DResolver.GetScopedCodeObject(ed, ctxt, DResolver.AstReparseOptions.AlsoParseBeyondCaret);

			if (o == null)
				throw new Exception("No identifier selected");

			// Try to resolve it using the usual (strictly filtered) way.
			AbstractType[] t=null;

			if (o is IExpression)
				t = new[] { Evaluation.EvaluateType((IExpression)o, ctxt) };
			else if(o is ITypeDeclaration)
				t = TypeDeclarationResolver.Resolve((ITypeDeclaration)o, ctxt);

			if (t != null && t.Length != 0 && t[0]!=null)
			{
				foreach (var at in t)
					if (at is DSymbol)
						l.Add(((DSymbol)at).Definition);

				return l.ToArray();
			}

			// If no results:

			// Extract a concrete id from that syntax object. (If access expression/nested decl, use the inner-most one)

			string id = null;

			if (o is ITypeDeclaration)
			{
				var td = ((ITypeDeclaration)o).InnerMost;

				if (td is IdentifierDeclaration)
					id = ((IdentifierDeclaration)td).Id;
				else if (td is TemplateInstanceExpression)
					id = ((TemplateInstanceExpression)td).TemplateIdentifier.Id;
			}
			else if (o is IExpression)
			{
				var x = (IExpression)o;

				while (x is PostfixExpression)
					x = ((PostfixExpression)x).PostfixForeExpression;

				if (x is IdentifierExpression && ((IdentifierExpression)x).IsIdentifier)
					id = (string)((IdentifierExpression)x).Value;
				else if (x is TemplateInstanceExpression)
					id = ((TemplateInstanceExpression)x).TemplateIdentifier.Id;
			}

			if (string.IsNullOrEmpty(id))
				throw new Exception("No extractable identifier found");

			// Rawly scan through all modules' roots of the parse cache to find that id.
			foreach(var pc in ed.ParseCache)
				foreach (var mod in pc)
				{
					if (mod.Name == id)
						l.Add(mod);

					var ch = mod[id];
					if(ch!=null)
						foreach (var c in ch)
						{
							var dn = c as DNode;

							if (dn == null || !dn.ContainsAttribute(DTokens.Package, DTokens.Private, DTokens.Protected))
								l.Add(c);
						}

					//TODO: Mixins
				}

			importRequired = true;
			return l.ToArray();
		}
	}
}
