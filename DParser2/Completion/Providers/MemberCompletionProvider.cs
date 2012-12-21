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
	public class MemberCompletionProvider : AbstractCompletionProvider
	{
		ResolutionContext ctxt;
		public PostfixExpression_Access AccessExpression;
		public IStatement ScopedStatement;
		public IBlockNode ScopedBlock;

		public MemberCompletionProvider(ICompletionDataGenerator cdg) : base(cdg) { }

		protected override void BuildCompletionDataInternal(IEditorData Editor, string EnteredText)
		{
			ctxt = ResolutionContext.Create(Editor.ParseCache, new ConditionalCompilationFlags(Editor), ScopedBlock, ScopedStatement);
			
			var ex = AccessExpression.AccessExpression == null ? AccessExpression.PostfixForeExpression : AccessExpression;

			var r = Evaluation.EvaluateType(ex, ctxt);

			if (r == null) //TODO: Add after-space list creation when an unbound . (Dot) was entered which means to access the global scope
				return;

			BuildCompletionData(r, ScopedBlock);

			if(Editor.Options.ShowUFCSItems && 
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

				if (resultParent == null)
					StaticTypePropertyProvider.AddGenericProperties(rr, CompletionDataGenerator, tr.Definition);

				StaticTypePropertyProvider.AddClassTypeProperties(CompletionDataGenerator, tr.Definition);
			}
			#endregion

			#region Things like int. or char.
			else if (rr is PrimitiveType)
			{
				var primType = (PrimitiveType)rr;

				if (primType.TypeToken > 0)
				{
					// Determine whether float by the var's base type
					bool isFloat = DTokens.BasicTypes_FloatingPoint[primType.TypeToken];

					if (resultParent == null)
						StaticTypePropertyProvider.AddGenericProperties(rr, CompletionDataGenerator, null, true);

					// Float implies integral props
					if (DTokens.BasicTypes_Integral[primType.TypeToken] || isFloat)
						StaticTypePropertyProvider.AddIntegralTypeProperties(primType.TypeToken, rr, CompletionDataGenerator, null, isFloat);

					if (isFloat)
						StaticTypePropertyProvider.AddFloatingTypeProperties(primType.TypeToken, rr, CompletionDataGenerator, null);
				}
			}
			#endregion

			else if (rr is PointerType)
			{
				var pt = (PointerType)rr;
				if (!(pt.Base is PrimitiveType && pt.Base.DeclarationOrExpressionBase is PointerDecl))
					BuildCompletionData(pt.Base, currentlyScopedBlock, true, pt);
			}

			else if (rr is AssocArrayType)
			{
				var ar = (AssocArrayType)rr;
				var ad = ar.TypeDeclarationOf as ArrayDecl;

				if (ar is ArrayType)
					StaticTypePropertyProvider.AddArrayProperties(rr, CompletionDataGenerator, ad);
				else
					StaticTypePropertyProvider.AddAssocArrayProperties(rr, CompletionDataGenerator, ad);
			}

			else if(rr is DelegateType)
				StaticTypePropertyProvider.AddDelegateProperties((DelegateType)rr, CompletionDataGenerator);
		}

		void BuildCompletionData(MemberSymbol mrr, IBlockNode currentlyScopedBlock, bool isVariableInstance = false)
		{
			if (mrr.Base != null)
					BuildCompletionData(mrr.Base, 
						currentlyScopedBlock,
						mrr is AliasedType ? isVariableInstance : true, // True if we obviously have a variable handled here. Otherwise depends on the samely-named parameter..
						mrr); 
			else
				StaticTypePropertyProvider.AddGenericProperties(mrr, CompletionDataGenerator, mrr.Definition, false);
		}

		void BuildCompletionData(PackageSymbol mpr)
		{
			foreach (var kv in mpr.Package.Packages)
				CompletionDataGenerator.AddPackage(kv.Key);

			foreach (var kv in mpr.Package.Modules)
				CompletionDataGenerator.AddModule(kv.Value,kv.Key);
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

				if (di.IsPublic && CanItemBeShownGenerally(i))
					CompletionDataGenerator.Add(i);
			}
		}

		void BuildCompletionData(UserDefinedType tr, bool showInstanceItems)
		{
			MemberCompletionEnumeration.EnumChildren(CompletionDataGenerator, ctxt, tr, showInstanceItems);
		}
	}
}
