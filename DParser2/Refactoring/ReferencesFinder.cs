using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Refactoring
{
	public class ReferencesFinder : DeepASTVisitor
	{
		#region Properties
		readonly ResolutionContext ctxt;
		readonly List<ISyntaxRegion> l = new List<ISyntaxRegion>();
		readonly INode symbol;
		readonly DModule ast;
		readonly string searchId;
		readonly int searchHash;
		#endregion

		#region Constructor / External
		ReferencesFinder(INode symbol, DModule ast, ResolutionContext ctxt)
		{
			this.ast = ast;
			this.symbol = symbol;
			searchId = symbol.Name;
			searchHash = symbol.NameHash;
			this.ctxt = ctxt;
		}

		public static IEnumerable<ISyntaxRegion> Scan(INode symbol, ResolutionContext ctxt, bool includeDefinition = true)
		{
			return Scan(symbol.NodeRoot as DModule, symbol, ctxt, includeDefinition);
		}

		/// <summary>
		/// </summary>
		/// <param name="ast">The syntax tree to scan</param>
		/// <param name="symbol">Might not be a child symbol of ast</param>
		/// <param name="ctxt">The context required to search for symbols</param>
		/// <returns></returns>
		public static IEnumerable<ISyntaxRegion> Scan(DModule ast, INode symbol, ResolutionContext ctxt, bool includeDefinition = true)
		{
			if (ast == null || symbol == null || ctxt == null)
				return null;

			ctxt.PushNewScope(ast);

			var f = new ReferencesFinder(symbol, ast, ctxt);

			f.S(ast);

			ctxt.Pop();

			var nodeRoot = symbol.NodeRoot as DModule;
			if (includeDefinition && nodeRoot != null && nodeRoot.FileName == ast.FileName)
			{
				var dc = symbol.Parent as DClassLike;
				if (dc != null && dc.ClassType == D_Parser.Parser.DTokens.Template &&
					dc.Name == symbol.Name)
				{
					f.l.Insert(0, new IdentifierDeclaration(dc.NameHash)
					{
						Location = dc.NameLocation,
						EndLocation = new CodeLocation(dc.NameLocation.Column + dc.Name.Length, dc.NameLocation.Line)
					});
				}

				f.l.Insert(0, new IdentifierDeclaration(symbol.NameHash)
				{
					Location = symbol.NameLocation,
					EndLocation = new CodeLocation(symbol.NameLocation.Column + symbol.Name.Length,	symbol.NameLocation.Line)
				});
			}

			return f.l;
		}
		#endregion

		protected override void OnScopeChanged(Dom.Statements.IStatement scopedStatement)
		{
			ctxt.CurrentContext.Set(ctxt.CurrentContext.ScopedBlock,scopedStatement);
		}

		protected override void OnScopeChanged(IBlockNode scopedBlock)
		{
			ctxt.CurrentContext.Set(scopedBlock);
		}

		protected override void Handle(ISyntaxRegion o)
		{
			Handle(o,null);
		}

		protected void Handle(ISyntaxRegion o, DSymbol resolvedSymbol)
		{
			if (o is IdentifierDeclaration)
			{
				var id = (IdentifierDeclaration)o;

				if (id.IdHash != searchHash)
					return;

				if (resolvedSymbol == null)
					resolvedSymbol = TypeDeclarationResolver.ResolveSingle(id, ctxt) as DSymbol;
			}
			else if (o is TemplateInstanceExpression)
			{
				var tix = (TemplateInstanceExpression)o;

				if (tix.TemplateIdHash != searchHash)
					return;

				if (resolvedSymbol == null)
					resolvedSymbol = Evaluation.EvaluateType(tix, ctxt) as DSymbol;
			}
			else if (o is IdentifierExpression)
			{
				var id = (IdentifierExpression)o;

				if (!id.IsIdentifier || id.ValueStringHash != searchHash)
					return;

				if (resolvedSymbol == null)
					resolvedSymbol = Evaluation.EvaluateType(id, ctxt) as DSymbol;
			}
			else if (o is PostfixExpression_Access)
			{
				var acc = (PostfixExpression_Access)o;

				if ((acc.AccessExpression is IdentifierExpression &&
					(acc.AccessExpression as IdentifierExpression).ValueStringHash != searchHash) ||
				(acc.AccessExpression is TemplateInstanceExpression &&
				((TemplateInstanceExpression)acc.AccessExpression).TemplateIdHash != searchHash))
				{
					Handle(acc.PostfixForeExpression, null);
					return;
				}
				else if (acc.AccessExpression is NewExpression)
				{
					var nex = (NewExpression)acc.AccessExpression;

					if ((nex.Type is IdentifierDeclaration &&
						((IdentifierDeclaration)nex.Type).IdHash != searchHash) ||
						(nex.Type is TemplateInstanceExpression &&
						((TemplateInstanceExpression)acc.AccessExpression).TemplateIdHash != searchHash))
					{
						Handle(acc.PostfixForeExpression, null);
						return;
					}
					// Are there other types to test for?
				}

				var s = resolvedSymbol ?? Evaluation.EvaluateType(acc, ctxt) as DerivedDataType;

				if (s is DSymbol)
				{
					if (((DSymbol)s).Definition == symbol)
						l.Add(acc.AccessExpression);
				}
				else if (s == null || !(s.Base is DSymbol))
					return;

				// Scan down for other possible symbols
				Handle(acc.PostfixForeExpression, s.Base as DSymbol);
				return;
			}

			// The resolved node must be equal to the symbol definition that is looked for.
			if (resolvedSymbol == null ||
				resolvedSymbol.Definition != symbol)
				return;

			l.Add(o);
		}
	}
}
