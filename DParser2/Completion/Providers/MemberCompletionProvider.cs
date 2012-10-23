using System.Collections.Generic;
using D_Parser.Completion.Providers;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Misc;

namespace D_Parser.Completion
{
	public class MemberCompletionProvider : AbstractCompletionProvider
	{
		ResolverContextStack ctxt;
		public PostfixExpression_Access AccessExpression;
		public IStatement ScopedStatement;
		public IBlockNode ScopedBlock;

		public MemberCompletionProvider(ICompletionDataGenerator cdg) : base(cdg) { }

		public enum ItemVisibility
		{
			None = 0,
			StaticOnly = 1,
			NonPrivate = 2,
			NonProtected = 4,
			NonPackage = 8
		}

		protected override void BuildCompletionDataInternal(IEditorData Editor, string EnteredText)
		{
			ctxt = ResolverContextStack.Create(Editor);
			var ex = AccessExpression.AccessExpression == null ? AccessExpression.PostfixForeExpression : AccessExpression;

			ctxt.PushNewScope(ScopedBlock).ScopedStatement = ScopedStatement;
			var r = Evaluation.EvaluateType(ex, ctxt);
			ctxt.Pop();

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

				var vis = isVariableInstance ? 
					(HaveSameAncestors(tr.Definition, currentlyScopedBlock) ? ItemVisibility.None : ItemVisibility.NonProtected) : 
						ItemVisibility.StaticOnly;
				AdjustPrivatePackageFilter(tr.Definition, currentlyScopedBlock, ref vis);

				// Cases:

				// myVar. (located in basetype definition)		<-- Show everything
				// this. 										<-- Show everything
				// myVar. (not located in basetype definition) 	<-- Show public and public static members
				// super. 										<-- Show all base type members
				// myClass. (not located in myClass)			<-- Show all static members
				// myClass. (located in myClass)				<-- Show all static members

				BuildCompletionData(tr, vis);

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
				CompletionDataGenerator.Add(kv.Key);

			foreach (var kv in mpr.Package.Modules)
				CompletionDataGenerator.Add(kv.Key, kv.Value);
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

		void AdjustPrivatePackageFilter(INode n, INode n2, ref ItemVisibility vis)
		{
			var curLevelAst = n.NodeRoot as IAbstractSyntaxTree;
			var nRoot = n2.NodeRoot as IAbstractSyntaxTree;

			if (curLevelAst != nRoot && curLevelAst != null && nRoot != null)
			{
				vis |= ItemVisibility.NonPrivate;

				if (ModuleNameHelper.ExtractPackageName(curLevelAst.ModuleName) !=
					ModuleNameHelper.ExtractPackageName(nRoot.ModuleName))
					vis |= ItemVisibility.NonPackage;
			}
		}

		void BuildCompletionData(UserDefinedType tr, ItemVisibility visMod)
		{
			var n = tr.Definition;
			if (n is DClassLike) // Add public static members of the class and including all base classes
			{
				var propertyMethodsToIgnore = new List<string>();

				var curlevel = tr;
				var tvisMod = visMod;
				while (curlevel != null)
				{
					foreach (var i in curlevel.Definition as DBlockNode)
					{
						var dn = i as DNode;

						if (i != null && dn == null)
						{
							CompletionDataGenerator.Add(i);
							continue;
						}

						bool add = true;

						if (tvisMod == 0 || dn.IsStatic || (!(dn is DVariable) || ((DVariable)dn).IsConst) || IsTypeNode(i))
							add = true;
						else
						{
							if (tvisMod.HasFlag(ItemVisibility.StaticOnly))
								add = false;
							else
							{
								if (tvisMod.HasFlag(ItemVisibility.NonPrivate))
									add = !dn.ContainsAttribute(DTokens.Private);
								if (tvisMod.HasFlag(ItemVisibility.NonProtected))
									add = !dn.ContainsAttribute(DTokens.Protected);
								if (tvisMod.HasFlag(ItemVisibility.NonPackage))
									add = !dn.ContainsAttribute(DTokens.Package);
							}
						}

						if (add)
						{
							if (CanItemBeShownGenerally(dn))
							{
								// Convert @property getters&setters to one unique property
								if (dn is DMethod && dn.ContainsPropertyAttribute())
								{
									if (!propertyMethodsToIgnore.Contains(dn.Name))
									{
										var dm = dn as DMethod;
										bool isGetter = dm.Parameters.Count < 1;

										var virtPropNode = new DVariable();

										virtPropNode.AssignFrom(dn);

										if (!isGetter)
											virtPropNode.Type = dm.Parameters[0].Type;

										CompletionDataGenerator.Add(virtPropNode);

										propertyMethodsToIgnore.Add(dn.Name);
									}
								}
								else
									CompletionDataGenerator.Add(dn);
							}

							// Add members of anonymous enums
							else if (dn is DEnum && string.IsNullOrEmpty(dn.Name))
							{
								foreach (var k in dn as DEnum)
									CompletionDataGenerator.Add(k);
							}
						}
					}

					if ((curlevel = curlevel.Base as UserDefinedType) != null)
					{
						AdjustPrivatePackageFilter(n, curlevel.Definition, ref tvisMod);
						tvisMod &= ~ItemVisibility.NonProtected;
					}
				}
			}
			else if (n is DEnum)
			{
				var de = n as DEnum;

				foreach (var i in de)
					if (i is DEnumValue)
						CompletionDataGenerator.Add(i);
			}
		}
	}
}
