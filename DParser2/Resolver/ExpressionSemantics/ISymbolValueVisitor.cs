using D_Parser.Dom;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public interface ISymbolValueVisitor : IVisitor
	{
		void VisitErrorValue(ErrorValue v);
		void VisitPrimitiveValue(PrimitiveValue v);
		void VisitVoidValue(VoidValue v);
		void VisitArrayValue(ArrayValue v);
		void VisitAssociativeArrayValue(AssociativeArrayValue v);
		void VisitDelegateValue(DelegateValue v);
		void VisitNullValue(NullValue v);
		void VisitTypeOverloadValue(InternalOverloadValue v);
		void VisitVariableValue(VariableValue v);
		void VisitTypeValue(TypeValue v);
	}

	public interface ISymbolValueVisitor<R> : IVisitor<R>
	{
		R VisitErrorValue(ErrorValue v);
		R VisitPrimitiveValue(PrimitiveValue v);
		R VisitVoidValue(VoidValue v);
		R VisitArrayValue(ArrayValue v);
		R VisitAssociativeArrayValue(AssociativeArrayValue v);
		R VisitDelegateValue(DelegateValue v);
		R VisitNullValue(NullValue v);
		R VisitTypeOverloadValue(InternalOverloadValue v);
		R VisitVariableValue(VariableValue v);
		R VisitTypeValue(TypeValue v);
	}
}
