using System.Collections.Generic;
using D_Parser.Completion.Providers;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Completion.Providers
{
	class MemberCompletionProvider : AbstractCompletionProvider
	{
		ResolutionContext ctxt;
		public ISyntaxRegion AccessExpression;
		public IStatement ScopedStatement;
		public IBlockNode ScopedBlock;
		public MemberFilter MemberFilter = MemberFilter.All;
		IEditorData ed;

		public MemberCompletionProvider(ICompletionDataGenerator cdg, ISyntaxRegion sr, IBlockNode b, IStatement stmt) : base(cdg) {
			AccessExpression = sr;
			ScopedBlock = b;
			ScopedStatement = stmt;
		}

		protected override void BuildCompletionDataInternal(IEditorData Editor, char enteredChar)
		{
			ed = Editor;
			ctxt = ResolutionContext.Create(Editor.ParseCache, new ConditionalCompilationFlags(Editor), ScopedBlock, ScopedStatement);
			ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			AbstractType t;

			if (AccessExpression is IExpression)
				t = Evaluation.EvaluateType (AccessExpression as IExpression, ctxt);
			else if (AccessExpression is ITypeDeclaration)
				t = TypeDeclarationResolver.ResolveSingle (AccessExpression as ITypeDeclaration, ctxt);
			else
				return;

			t = DResolver.StripAliasSymbol (t);

			if (t == null) //TODO: Add after-space list creation when an unbound . (Dot) was entered which means to access the global scope
				return;

			BuildCompletionData(t);
		}

		void BuildCompletionData(
			AbstractType rr,
			bool isVariableInstance = false,
			AbstractType resultParent = null)
		{
			if (rr == null)
				return;

			if(rr.DeclarationOrExpressionBase is ITypeDeclaration)
				isVariableInstance |= (rr.DeclarationOrExpressionBase as ITypeDeclaration).ExpressesVariableAccess;

			if (rr is ArrayAccessSymbol || rr is DelegateCallSymbol)
			{
				isVariableInstance = true;
				rr = (rr as DerivedDataType).Base;
			}

			if (rr is TemplateParameterSymbol) {
				var tps = rr as TemplateParameterSymbol;
				if (tps.Base == null) {
					var tpp = tps.Parameter is TemplateThisParameter ? (tps.Parameter as TemplateThisParameter).FollowParameter : tps.Parameter;
					if (tpp is TemplateTupleParameter) {
						StaticProperties.ListProperties (CompletionDataGenerator, MemberFilter, tps, true);
						return;
					}
				}
			}

			var mrr = rr as MemberSymbol;
			if (mrr != null && mrr.Base != null) {
				BuildCompletionData(mrr.Base,
					isVariableInstance ||
					(mrr.Definition is DVariable && !(mrr is AliasedType) || // True if we obviously have a variable handled here. Otherwise depends on the samely-named parameter..
						mrr.Definition is DMethod),	mrr);
			}

			// A module path has been typed
			else if (!isVariableInstance && rr is ModuleSymbol)
				BuildCompletionData ((ModuleSymbol)rr);
			else if (rr is PackageSymbol)
				BuildCompletionData ((PackageSymbol)rr);

			#region A type was referenced directly
			else if (rr is EnumType) {
				var en = (EnumType)rr;

				foreach (var e in en.Definition)
					CompletionDataGenerator.Add (e);
				// TODO: Enlist ufcs items&stat props here aswell?
			} else if (rr is TemplateIntermediateType) {
				var tr = (TemplateIntermediateType)rr;

				if (tr.DeclarationOrExpressionBase is TokenExpression) {
					int token = ((TokenExpression)tr.DeclarationOrExpressionBase).Token;

					isVariableInstance = token == DTokens.This || token == DTokens.Super;
				}

				// Cases:

				// myVar. (located in basetype definition)		<-- Show everything
				// this. 										<-- Show everything
				// myVar. (not located in basetype definition) 	<-- Show public and public static members
				// super. 										<-- Show all base type members
				// myClass. (not located in myClass)			<-- Show all static members
				// myClass. (located in myClass)				<-- Show all static members

				BuildCompletionData (tr, isVariableInstance);
			}
			#endregion

			else if (rr is PointerType) {
				var pt = (PointerType)rr;
				if (!(pt.Base is PrimitiveType && pt.Base.DeclarationOrExpressionBase is PointerDecl))
					BuildCompletionData (pt.Base, true, pt);
			} else {
				if (isVariableInstance)
					GenUfcsCompletionItems (rr);
				StaticProperties.ListProperties (CompletionDataGenerator, MemberFilter, rr, isVariableInstance);
			}
		}

		void BuildCompletionData(PackageSymbol mpr)
		{
			foreach (var kv in mpr.Package.Packages)
				CompletionDataGenerator.AddPackage(kv.Value.Name);

			foreach (var kv in mpr.Package.Modules)
				CompletionDataGenerator.AddModule(kv.Value);
		}

		void BuildCompletionData(ModuleSymbol tr)
		{
			foreach (var i in tr.Definition)
			{
				var di = i as DNode;
				if (di == null)
				{
					if (i != null)
						CompletionDataGenerator.Add(i);
					continue;
				}

				if (di.IsPublic && CanItemBeShownGenerally(i) && AbstractVisitor.CanAddMemberOfType(MemberFilter, i))
					CompletionDataGenerator.Add(i);
			}
		}

		void BuildCompletionData(UserDefinedType tr, bool showInstanceItems)
		{
			MemberCompletionEnumeration.EnumChildren(CompletionDataGenerator, ctxt, tr, showInstanceItems, MemberFilter);
			if (showInstanceItems)
				GenUfcsCompletionItems (tr);
			StaticProperties.ListProperties(CompletionDataGenerator, MemberFilter, tr, showInstanceItems);
		}

		void GenUfcsCompletionItems(AbstractType t)
		{
			if(CompletionOptions.Instance.ShowUFCSItems)
				foreach (var ufcsItem in UFCSResolver.TryResolveUFCS(t, 0, ed.CaretLocation, ctxt))
					CompletionDataGenerator.Add ((ufcsItem as DSymbol).Definition);
		}
	}
}
