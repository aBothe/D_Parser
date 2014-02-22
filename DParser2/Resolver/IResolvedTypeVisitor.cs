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
		void VisitPrimitiveType(PrimitiveType pt);
		void VisitPointerType(PointerType pt);
		void VisitArrayType(ArrayType at);
		void VisitAssocArrayType(AssocArrayType aa);
		void VisitDelegateCallSymbol(DelegateCallSymbol dg);
		void VisitDelegateType(DelegateType dg);
		void VisitAliasedType(AliasedType at);
		void VisitEnumType(EnumType t);
		void VisitStructType(StructType t);
		void VisitUnionType(UnionType t);
		void VisitClassType(ClassType t);
		void VisitInterfaceType(InterfaceType t);
		void VisitTemplateType(TemplateType t);
		void VisitMixinTemplateType(MixinTemplateType t);
		void VisitEponymousTemplateType(EponymousTemplateType t);
		void VisitStaticProperty(StaticProperty p);
		void VisitMemberSymbol(MemberSymbol ms);
		void VisitTemplateParameterSymbol(TemplateParameterSymbol tps);
		void VisitArrayAccessSymbol(ArrayAccessSymbol tps);
		void VisitModuleSymbol(ModuleSymbol tps);
		void VisitPackageSymbol(PackageSymbol tps);
		void VisitDTuple(DTuple tps);

		void VisitAmbigousType(AmbiguousType t);
	}
	public interface IResolvedTypeVisitor<R> : IVisitor<R>
	{
		R VisitPrimitiveType(PrimitiveType pt);
		R VisitPointerType(PointerType pt);
		R VisitArrayType(ArrayType at);
		R VisitAssocArrayType(AssocArrayType aa);
		R VisitDelegateCallSymbol(DelegateCallSymbol dg);
		R VisitDelegateType(DelegateType dg);
		R VisitAliasedType(AliasedType at);
		R VisitEnumType(EnumType t);
		R VisitStructType(StructType t);
		R VisitUnionType(UnionType t);
		R VisitClassType(ClassType t);
		R VisitInterfaceType(InterfaceType t);
		R VisitTemplateType(TemplateType t);
		R VisitMixinTemplateType(MixinTemplateType t);
		R VisitEponymousTemplateType(EponymousTemplateType t);
		R VisitStaticProperty(StaticProperty p);
		R VisitMemberSymbol(MemberSymbol ms);
		R VisitTemplateParameterSymbol(TemplateParameterSymbol tps);
		R VisitArrayAccessSymbol(ArrayAccessSymbol tps);
		R VisitModuleSymbol(ModuleSymbol tps);
		R VisitPackageSymbol(PackageSymbol tps);
		R VisitDTuple(DTuple tps);

		R VisitAmbigousType(AmbiguousType t);
	}
}
