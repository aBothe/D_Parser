using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver
{
	public class DTypeToTypeDeclVisitor : IResolvedTypeVisitor<ITypeDeclaration>
	{

		public static ITypeDeclaration GenerateTypeDecl(AbstractType t)
		{
			return t != null ? new DTypeToTypeDeclVisitor().AcceptType(t) : null;
		}

		private DTypeToTypeDeclVisitor() { }

		ITypeDeclaration AcceptType(AbstractType t)
		{
			if (t == null)
				return null;

			var aliasTag = t.Tag as TypeDeclarationResolver.AliasTag;
			if (aliasTag != null)
			{
				return new IdentifierDeclaration(aliasTag.aliasDefinition.NameHash);
			}

			var td = t.Accept(this);

			if(t.Modifier != 0)
				td = new MemberFunctionAttributeDecl(t.Modifier) { InnerType = td };

			return td ?? t.DeclarationOrExpressionBase as ITypeDeclaration;
		}

		public ITypeDeclaration VisitPrimitiveType(PrimitiveType t)
		{
			return new DTokenDeclaration(t.TypeToken);
		}

		public ITypeDeclaration VisitPointerType(PointerType t)
		{
			return new PointerDecl(AcceptType(t.Base));
		}

		public ITypeDeclaration VisitArrayType(ArrayType t)
		{
			return new ArrayDecl
			{
				ValueType = AcceptType(t.Base),
				KeyExpression = t.IsStaticArray ? new IdentifierExpression(t.FixedLength, LiteralFormat.Scalar) : null
			};
		}

		public ITypeDeclaration VisitAssocArrayType(AssocArrayType t)
		{
			return new ArrayDecl
			{
				ValueType = AcceptType(t.ValueType),
				KeyType = AcceptType(t.KeyType)
			};
		}

		public ITypeDeclaration VisitDelegateCallSymbol(DelegateCallSymbol t)
		{
			return AcceptType(t.Delegate);
		}

		public ITypeDeclaration VisitDelegateType(DelegateType t)
		{
			var dd = new DelegateDeclaration
			{
				ReturnType = AcceptType(t.ReturnType),
				IsFunction = t.IsFunction
			};
			
			if (t.Parameters != null)
				foreach (var p in t.Parameters)
					dd.Parameters.Add(new DVariable { Type = AcceptType(p) });

			return dd;
		}

		ITypeDeclaration VisitDSymbol(DSymbol t)
		{
			var def = t.Definition;
			ITypeDeclaration td = new IdentifierDeclaration(def != null ? def.NameHash : 0);

			if (def != null && t.DeducedTypes != null && def.TemplateParameters != null)
			{
				var args = new List<IExpression>();
				foreach (var tp in def.TemplateParameters)
				{
					IExpression argEx = null;
					foreach (var tps in t.DeducedTypes)
						if (tps != null && tps.Parameter == tp)
						{
							if (tps.ParameterValue != null)
							{
								//TODO: Convert ISymbolValues back to IExpression
							}
							else
								argEx = new TypeDeclarationExpression(AcceptType(tps));
							break;
						}

					args.Add(argEx ?? new IdentifierExpression(tp.NameHash));
				}

				td = new TemplateInstanceExpression(td) { Arguments = args.ToArray() };
			}

			var ret = td;

			while (def != (def = def.Parent as DNode) &&
				def != null && !(def is DModule))
			{
				td = td.InnerDeclaration = new IdentifierDeclaration(def.NameHash);
			}

			return ret;
		}

		public ITypeDeclaration VisitAliasedType(AliasedType t)
		{
			return VisitDSymbol(t);
		}

		public ITypeDeclaration VisitEnumType(EnumType t)
		{
			return VisitDSymbol(t);
		}

		public ITypeDeclaration VisitStructType(StructType t)
		{
			return VisitDSymbol(t);
		}

		public ITypeDeclaration VisitUnionType(UnionType t)
		{
			return VisitDSymbol(t);
		}

		public ITypeDeclaration VisitClassType(ClassType t)
		{
			return VisitDSymbol(t);
		}

		public ITypeDeclaration VisitInterfaceType(InterfaceType t)
		{
			return VisitDSymbol(t);
		}

		public ITypeDeclaration VisitTemplateType(TemplateType t)
		{
			return VisitDSymbol(t);
		}

		public ITypeDeclaration VisitMixinTemplateType(MixinTemplateType t)
		{
			return VisitDSymbol(t);
		}

		public ITypeDeclaration VisitEponymousTemplateType(EponymousTemplateType t)
		{
			return VisitDSymbol(t);
		}

		public ITypeDeclaration VisitStaticProperty(StaticProperty t)
		{
			return VisitDSymbol(t);
		}

		public ITypeDeclaration VisitMemberSymbol(MemberSymbol t)
		{
			return VisitDSymbol(t);
		}

		public ITypeDeclaration VisitTemplateParameterSymbol(TemplateParameterSymbol t)
		{
			if (t.Base != null)
				return AcceptType(t.Base);

			return (t.DeclarationOrExpressionBase as ITypeDeclaration ?? new IdentifierDeclaration(t.Parameter.NameHash));
		}

		public ITypeDeclaration VisitArrayAccessSymbol(ArrayAccessSymbol t)
		{
			var ad = new ArrayDecl { ValueType = AcceptType(t.Base) };

			if (t.DeclarationOrExpressionBase is IExpression)
				ad.KeyExpression = t.DeclarationOrExpressionBase as IExpression;
			else
				ad.KeyType = t.DeclarationOrExpressionBase as ITypeDeclaration;

			return ad;
		}

		public ITypeDeclaration VisitModuleSymbol(ModuleSymbol t)
		{
			return VisitDSymbol(t);
		}

		public ITypeDeclaration VisitPackageSymbol(PackageSymbol t)
		{
			return null;
		}

		public ITypeDeclaration VisitDTuple(DTuple t)
		{
			return null;
		}

		public ITypeDeclaration VisitUnknownType(UnknownType t)
		{
			return new IdentifierDeclaration("?");
		}

		public ITypeDeclaration VisitAmbigousType(AmbiguousType t)
		{
			return null;
		}
	}
}
