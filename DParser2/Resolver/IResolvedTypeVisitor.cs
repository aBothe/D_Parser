using D_Parser.Dom;
using D_Parser.Resolver.ExpressionSemantics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver
{
	public interface IResolvedTypeVisitor : IVisitor
	{
		void VisitPrimitiveType(PrimitiveType t);
		void VisitPointerType(PointerType t);
		void VisitArrayType(ArrayType t);
		void VisitAssocArrayType(AssocArrayType t);
		void VisitDelegateCallSymbol(DelegateCallSymbol t);
		void VisitDelegateType(DelegateType t);
		void VisitAliasedType(AliasedType t);
		void VisitEnumType(EnumType t);
		void VisitStructType(StructType t);
		void VisitUnionType(UnionType t);
		void VisitClassType(ClassType t);
		void VisitInterfaceType(InterfaceType t);
		void VisitTemplateType(TemplateType t);
		void VisitMixinTemplateType(MixinTemplateType t);
		void VisitEponymousTemplateType(EponymousTemplateType t);
		void VisitStaticProperty(StaticProperty t);
		void VisitMemberSymbol(MemberSymbol t);
		void VisitTemplateParameterSymbol(TemplateParameterSymbol t);
		void VisitArrayAccessSymbol(ArrayAccessSymbol t);
		void VisitModuleSymbol(ModuleSymbol t);
		void VisitPackageSymbol(PackageSymbol t);
		void VisitDTuple(DTuple t);

		void VisitUnknownType(UnknownType t);
		void VisitAmbigousType(AmbiguousType t);
	}
	public interface IResolvedTypeVisitor<R> : IVisitor<R>
	{
		R VisitPrimitiveType(PrimitiveType t);
		R VisitPointerType(PointerType t);
		R VisitArrayType(ArrayType t);
		R VisitAssocArrayType(AssocArrayType t);
		R VisitDelegateCallSymbol(DelegateCallSymbol t);
		R VisitDelegateType(DelegateType t);
		R VisitAliasedType(AliasedType t);
		R VisitEnumType(EnumType t);
		R VisitStructType(StructType t);
		R VisitUnionType(UnionType t);
		R VisitClassType(ClassType t);
		R VisitInterfaceType(InterfaceType t);
		R VisitTemplateType(TemplateType t);
		R VisitMixinTemplateType(MixinTemplateType t);
		R VisitEponymousTemplateType(EponymousTemplateType t);
		R VisitStaticProperty(StaticProperty t);
		R VisitMemberSymbol(MemberSymbol t);
		R VisitTemplateParameterSymbol(TemplateParameterSymbol t);
		R VisitArrayAccessSymbol(ArrayAccessSymbol t);
		R VisitModuleSymbol(ModuleSymbol t);
		R VisitPackageSymbol(PackageSymbol t);
		R VisitDTuple(DTuple t);

		R VisitUnknownType(UnknownType t);
		R VisitAmbigousType(AmbiguousType t);
	}
}
