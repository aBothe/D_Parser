using System;
using System.Collections.Generic;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Refactoring
{
	public abstract class ImportDirectiveCreator
	{
		/// <summary>
		/// Note: May throws various exceptions
		/// </summary>
		public static void CreateImportDirectiveForHighlightedSymbol(IEditorData editor, ImportDirectiveCreator IOInterface)
		{
			bool importRequired = false;
			var matches = ImportDirectiveCreator.TryFindingSelectedIdImportIndependently(editor, out importRequired);

			INode selection = null;
			// If there are multiple types, show a list of those items
			if (matches == null || matches.Length == 0)
				throw new Exception("No symbol found!");
			else if (matches.Length == 1)
				selection = matches[0];
			else if((selection = IOInterface.HandleMultipleResults(matches)) == null)
				return;

			if (selection == null)
				throw new Exception("Selected symbol must not be null.");

			var mod = selection.NodeRoot as DModule;
			if (mod == null)
				throw new Exception("Node not assigned to a parent syntax tree. Abort operation.");

			if (mod == editor.SyntaxTree)
				throw new Exception("Symbol is part of the current module. No import required!");

			if (!importRequired)
				throw new Exception("Symbol imported already. No further import required!");

			GenerateImportStatementForNode (selection, editor, IOInterface.InsertIntoCode);
		}

		public static void GenerateImportStatementForNode(INode n, IEditorData ed, Action<CodeLocation, string> ci)
		{
			var loc = new CodeLocation(0, DParser.FindLastImportStatementEndLocation(ed.SyntaxTree, ed.ModuleCode).Line+1);
			ci(loc, "import " + (n.NodeRoot as DModule).ModuleName + ";\n");
		}

		public static INode[] TryFindingSelectedIdImportIndependently(IEditorData ed, out bool importRequired, bool tryResolveNormally = true)
		{
			importRequired = false;
			var l = new List<INode>();

			var ctxt = ResolutionContext.Create(ed);
			ctxt.ContextIndependentOptions = ResolutionOptions.DontResolveBaseTypes |
				ResolutionOptions.DontResolveAliases | ResolutionOptions.NoTemplateParameterDeduction;

			// Get scoped object.
			var o = DResolver.GetScopedCodeObject(ed, ctxt, DResolver.AstReparseOptions.AlsoParseBeyondCaret);

			if (o == null)
				throw new Exception("No identifier selected");

			// Try to resolve it using the usual (strictly filtered) way.
			if (tryResolveNormally)
			{
				AbstractType[] t = null;

				if (o is IExpression)
					t = new[] { Evaluation.EvaluateType((IExpression)o, ctxt) };
				else if (o is ITypeDeclaration)
					t = TypeDeclarationResolver.Resolve((ITypeDeclaration)o, ctxt);

				if (t != null && t.Length != 0 && t[0] != null)
				{
					foreach (var at in t)
						if (at is DSymbol)
							l.Add(((DSymbol)at).Definition);

					return l.ToArray();
				}
			}

			// If no results:

			// Extract a concrete id from that syntax object. (If access expression/nested decl, use the inner-most one)
			int idHash=0;

			chkAgain:
			if (o is ITypeDeclaration)
			{
				var td = ((ITypeDeclaration)o).InnerMost;

				if (td is IdentifierDeclaration)
					idHash = ((IdentifierDeclaration)td).IdHash;
				else if (td is TemplateInstanceExpression)
					idHash = ((TemplateInstanceExpression)td).TemplateIdHash;
			}
			else if (o is IExpression)
			{
				var x = (IExpression)o;

				while (x is PostfixExpression)
					x = ((PostfixExpression)x).PostfixForeExpression;

				if (x is IdentifierExpression && ((IdentifierExpression)x).IsIdentifier)
					idHash = ((IdentifierExpression)x).ValueStringHash;
				else if (x is TemplateInstanceExpression)
					idHash = ((TemplateInstanceExpression)x).TemplateIdHash;
				else if (x is NewExpression)
				{
					o = ((NewExpression)x).Type;
					goto chkAgain;
				}
			}

			if (idHash == 0)
				throw new Exception("No extractable identifier found");

			// Rawly scan through all modules' roots of the parse cache to find that id.
			foreach(var pc in ed.ParseCache)
				foreach (var mod in pc)
				{
					if (mod.NameHash == idHash)
						l.Add(mod);

					var ch = mod[idHash];
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


		public abstract INode HandleMultipleResults(INode[] results);
		public abstract void InsertIntoCode(CodeLocation location, string codeToInsert);
	}
}
