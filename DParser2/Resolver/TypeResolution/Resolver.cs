using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver.TypeResolution
{
	/// <summary>
	/// Generic class for resolve module relations and/or declarations
	/// </summary>
	public class DResolver
	{
		/// <summary>Used for code completion/symbol resolution.</summary>
		/// <param name="editor">Can be null</param>
		public static ISyntaxRegion GetScopedCodeObject(IEditorData editor)
		{
			var block = ASTSearchHelper.SearchBlockAt(editor.SyntaxTree, editor.CaretLocation);

			IStatement stmt = null;
			if (block is DMethod)
				stmt = (block as DMethod).GetSubBlockAt(editor.CaretLocation);
			
			var vis = new ScopedObjectVisitor(editor.CaretLocation);
			if (stmt != null)
				stmt.Accept(vis);
			else
				block.Accept(vis);

			return vis.IdNearCaret;
		}

		public static AbstractType ResolveType(IEditorData editor, ResolutionContext ctxt = null)
		{
			var o = GetScopedCodeObject(editor);
			if (ctxt == null)
				ctxt = ResolutionContext.Create(editor, false);

			AbstractType ret = null;

			CodeCompletion.DoTimeoutableCompletionTask(null, ctxt, () =>
			{
				ctxt.Push(editor);
				
				var optionBackup = ctxt.CurrentContext.ContextDependentOptions;
				ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

				if (o is IExpression)
					ret = ExpressionTypeEvaluation.EvaluateType((IExpression)o, ctxt, false);
				else if (o is ITypeDeclaration)
					ret = TypeDeclarationResolver.ResolveSingle((ITypeDeclaration)o, ctxt);
				else if (o is INode)
					ret = TypeDeclarationResolver.HandleNodeMatch(o as INode, ctxt);

				ctxt.CurrentContext.ContextDependentOptions = optionBackup;
			}, editor.CancelToken);

			return ret;
		}

		public static AbstractType StripAliasedTypes(AbstractType t)
		{
			var unaliasedOverload = t;
			while (unaliasedOverload is AliasedType && (unaliasedOverload as AliasedType).Base != null)
				unaliasedOverload = (unaliasedOverload as AliasedType).Base;
			return unaliasedOverload;
		}

		/// <summary>
		/// Removes all kinds of members from the given results.
		/// </summary>
		internal static AbstractType StripMemberSymbols(AbstractType r)
		{
			var ds = r as DerivedDataType;
			if (ds != null && ds.Base != null) {
				while(ds is AliasedType && ds.Base != null)
				{
					r = ds.Base;
					ds = r as DerivedDataType;
				}

				if (ds is ArrayAccessSymbol || ds is MemberSymbol || ds is DelegateCallSymbol) {
					r = ds.Base;
					ds = r as DerivedDataType;
				}

				while (ds is AliasedType && ds.Base != null)
				{
					r = ds.Base;
					ds = r as DerivedDataType;
				}

				if (r is TemplateParameterSymbol) {
					if (ds.Base == null)
						return r;
					r = ds.Base;
					ds = r as DerivedDataType;
				}

				// There's one special case to handle (TODO: are there further cases?):
				// auto o = new Class(); -- o will be MemberSymbol and its base type will be a MemberSymbol either (i.e. the constructor reference)
				if(ds is MemberSymbol && (ds as DSymbol).Definition is DMethod && (ds as DSymbol).NameHash == DMethod.ConstructorIdentifierHash)
					r = ds.Base;
			}

			return r;
		}
	}
}
