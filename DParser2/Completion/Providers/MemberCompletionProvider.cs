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
using System.Threading.Tasks;
using System;

namespace D_Parser.Completion.Providers
{
	class MemberCompletionProvider : AbstractCompletionProvider, IResolvedTypeVisitor
	{
		ResolutionContext ctxt;
		public ISyntaxRegion AccessExpression;
		public IBlockNode ScopedBlock;
		public MemberFilter MemberFilter = MemberFilter.All;
		IEditorData ed;

		public MemberCompletionProvider(ICompletionDataGenerator cdg, ISyntaxRegion sr, IBlockNode b) : base(cdg) {
			AccessExpression = sr;
			ScopedBlock = b;
		}

		protected override void BuildCompletionDataInternal(IEditorData Editor, char enteredChar)
		{
			ed = Editor;
			ctxt = ResolutionContext.Create(Editor, false);

			AbstractType t = null;
			CodeCompletion.DoTimeoutableCompletionTask(CompletionDataGenerator,ctxt,() =>
			{
				try
				{
					ctxt.Push(Editor);
					if (AccessExpression is IExpression)
						t = ExpressionTypeEvaluation.EvaluateType(AccessExpression as IExpression, ctxt);
					else if (AccessExpression is ITypeDeclaration)
						t = TypeDeclarationResolver.ResolveSingle(AccessExpression as ITypeDeclaration, ctxt);
				}catch(Exception ex)
				{
					Logger.LogWarn("Error during member completion", ex);
				}
			}, Editor.CancelToken);

			if (t == null) //TODO: Add after-space list creation when an unbound . (Dot) was entered which means to access the global scope
				return;

			t.Accept(this);
		}

		void GenUfcsAndStaticProperties(AbstractType t)
		{
			if (t.NonStaticAccess && CompletionOptions.Instance.ShowUFCSItems)
				CodeCompletion.DoTimeoutableCompletionTask(null, ctxt, () =>
					{
						try
						{
							foreach (var ufcsItem in UFCSResolver.TryResolveUFCS(t, 0, ed.CaretLocation, ctxt))
								CompletionDataGenerator.Add((ufcsItem as DSymbol).Definition);
						}
						catch(Exception ex)
						{
							Logger.LogWarn("Error during ufcs completion resolution", ex);
						}
					});
			StaticProperties.ListProperties(CompletionDataGenerator, ctxt, MemberFilter, t, t.NonStaticAccess);
		}

		public void VisitPrimitiveType(PrimitiveType pt)
		{
			GenUfcsAndStaticProperties(pt);
		}

		public void VisitPointerType(PointerType pt)
		{
			if(pt.Base is PrimitiveType)
				GenUfcsAndStaticProperties(pt);
			else if (pt.Base != null)
				pt.Base.Accept(this);
		}

		public void VisitArrayType(ArrayType at)
		{
			VisitAssocArrayType(at);
		}

		public void VisitAssocArrayType(AssocArrayType aa)
		{
			GenUfcsAndStaticProperties(aa);
		}

		public void VisitDelegateCallSymbol(DelegateCallSymbol dg)
		{
			if (dg.Base != null)
				dg.Base.Accept(this);
			else
				GenUfcsAndStaticProperties(dg);
		}

		public void VisitArrayAccessSymbol(ArrayAccessSymbol aas)
		{
			if (aas.Base != null)
				aas.Base.Accept(this);
			else
				GenUfcsAndStaticProperties(aas);
		}

		public void VisitDelegateType(DelegateType dg)
		{
			GenUfcsAndStaticProperties(dg);
		}

		public void VisitAliasedType(AliasedType at)
		{
			if (at.Base != null)
				at.Base.Accept(this);
			else
				GenUfcsAndStaticProperties(at);
		}

		void VisitTemplateIntermediateType(TemplateIntermediateType tr)
		{
			// Cases:

			// myVar. (located in basetype definition)		<-- Show everything
			// this. 										<-- Show everything
			// myVar. (not located in basetype definition) 	<-- Show public and public static members
			// super. 										<-- Show all base type members
			// myClass. (not located in myClass)			<-- Show all static members
			// myClass. (located in myClass)				<-- Show all static members

			MemberCompletionEnumeration.EnumChildren(CompletionDataGenerator, ctxt, tr, MemberFilter);

			GenUfcsAndStaticProperties(tr);
		}

		public void VisitEnumType(EnumType en)
		{
			foreach (var e in en.Definition)
				CompletionDataGenerator.Add(e);
			// TODO: Enlist ufcs items&stat props here aswell?
		}

		public void VisitStructType(StructType t)
		{
			VisitTemplateIntermediateType(t);
		}

		public void VisitUnionType(UnionType t)
		{
			VisitTemplateIntermediateType(t);
		}

		public void VisitClassType(ClassType t)
		{
			VisitTemplateIntermediateType(t);
		}

		public void VisitInterfaceType(InterfaceType t)
		{
			VisitTemplateIntermediateType(t);
		}

		public void VisitTemplateType(TemplateType t)
		{
			VisitTemplateIntermediateType(t);
		}

		public void VisitMixinTemplateType(MixinTemplateType t)
		{
			VisitTemplateType(t);
		}

		public void VisitEponymousTemplateType(EponymousTemplateType t)
		{
			if (t.Base != null)
				t.Base.Accept(this);
			else
				GenUfcsAndStaticProperties(t);
		}

		public void VisitStaticProperty(StaticProperty p)
		{
			VisitMemberSymbol(p);
		}

		public void VisitMemberSymbol(MemberSymbol mrr)
		{
			if (mrr.Base != null)
				mrr.Base.Accept(this);
			else
				GenUfcsAndStaticProperties(mrr);
		}

		public void VisitTemplateParameterSymbol(TemplateParameterSymbol tps)
		{
			if (tps.Base != null)
				tps.Base.Accept(this);
			else
			{
				var tpp = tps.Parameter is TemplateThisParameter ? (tps.Parameter as TemplateThisParameter).FollowParameter : tps.Parameter;
				if (tpp is TemplateTupleParameter)
					StaticProperties.ListProperties(CompletionDataGenerator, ctxt, MemberFilter, tps, true);
			}
		}

		public void VisitModuleSymbol(ModuleSymbol tr)
		{/*
			if (isVariableInstance) // WHY?
				return;
			*/
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

		public void VisitPackageSymbol(PackageSymbol mpr)
		{
			foreach (var kv in mpr.Package.Packages)
				CompletionDataGenerator.AddPackage(kv.Value.Name);

			foreach (var kv in mpr.Package.Modules)
				CompletionDataGenerator.AddModule(kv.Value);
		}

		public void VisitDTuple(DTuple tps)
		{
			GenUfcsAndStaticProperties(tps);
		}

		public void VisitUnknownType(UnknownType t)
		{
			// Error
		}

		public void VisitAmbigousType(AmbiguousType t)
		{
			// Error?
			foreach (var subType in t.Overloads)
				subType.Accept(this);
		}
	}
}
