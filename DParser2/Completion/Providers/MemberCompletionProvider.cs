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

namespace D_Parser.Completion
{
	class MemberCompletionProvider : AbstractCompletionProvider
	{
		ResolutionContext ctxt;
		public PostfixExpression_Access AccessExpression;
		public IStatement ScopedStatement;
		public IBlockNode ScopedBlock;
		public MemberFilter MemberFilter = MemberFilter.All;

		public MemberCompletionProvider(ICompletionDataGenerator cdg) : base(cdg) { }

		protected override void BuildCompletionDataInternal(IEditorData Editor, char enteredChar)
		{
			ctxt = ResolutionContext.Create(Editor.ParseCache, new ConditionalCompilationFlags(Editor), ScopedBlock, ScopedStatement);
			ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			var ex = AccessExpression.AccessExpression == null ? AccessExpression.PostfixForeExpression : AccessExpression;

			var r = DResolver.StripAliasSymbol(Evaluation.EvaluateType(ex, ctxt));

			if (r == null) //TODO: Add after-space list creation when an unbound . (Dot) was entered which means to access the global scope
				return;

			BuildCompletionData(r, ScopedBlock);

			if(CompletionOptions.Instance.ShowUFCSItems && (MemberFilter & MemberFilter.Methods) != 0 &&
				!(r is UserDefinedType || r is PrimitiveType || r is PackageSymbol || r is ModuleSymbol))
				UFCSCompletionProvider.Generate(r, ctxt, Editor, CompletionDataGenerator);
		}

		void BuildCompletionData(
			AbstractType rr,
			IBlockNode currentlyScopedBlock,
			bool isVariableInstance = false,
			AbstractType resultParent = null)
		{
			if (rr == null)
				return;

			if(rr.DeclarationOrExpressionBase is ITypeDeclaration)
				isVariableInstance |= (rr.DeclarationOrExpressionBase as ITypeDeclaration).ExpressesVariableAccess;

			if (rr is ArrayAccessSymbol)
			{
				isVariableInstance = true;
				rr = (rr as ArrayAccessSymbol).Base;
			}

			if (rr is MemberSymbol)
				BuildCompletionData((MemberSymbol)rr, currentlyScopedBlock, isVariableInstance);

			// A module path has been typed
			else if (!isVariableInstance && rr is ModuleSymbol)
				BuildCompletionData((ModuleSymbol)rr);

			else if (rr is PackageSymbol)
				BuildCompletionData((PackageSymbol)rr);

			#region A type was referenced directly
			else if (rr is EnumType)
			{
				var en = (EnumType)rr;

				foreach (var e in en.Definition)
					CompletionDataGenerator.Add(e);
			}

			else if (rr is TemplateIntermediateType)
			{
				var tr = (TemplateIntermediateType)rr;

				if (tr.DeclarationOrExpressionBase is TokenExpression)
				{
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

				BuildCompletionData(tr, isVariableInstance);
			}
			#endregion

			else if (rr is PointerType)
			{
				var pt = (PointerType)rr;
				if (!(pt.Base is PrimitiveType && pt.Base.DeclarationOrExpressionBase is PointerDecl))
					BuildCompletionData(pt.Base, currentlyScopedBlock, true, pt);
			}

			else
				StaticProperties.ListProperties(CompletionDataGenerator, MemberFilter, rr, isVariableInstance);
		}

		void BuildCompletionData(MemberSymbol mrr, IBlockNode currentlyScopedBlock, bool isVariableInstance = false)
		{
			if (mrr.Base != null)
					BuildCompletionData(mrr.Base, 
						currentlyScopedBlock,
						isVariableInstance ||
						(mrr.Definition is DVariable && !(mrr is AliasedType) || // True if we obviously have a variable handled here. Otherwise depends on the samely-named parameter..
						mrr.Definition is DMethod),
						mrr); 
			else
				StaticProperties.ListProperties(CompletionDataGenerator, MemberFilter, mrr, false);
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
			StaticProperties.ListProperties(CompletionDataGenerator, MemberFilter, tr, showInstanceItems);
		}
	}
}
