using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver.ExpressionSemantics.CTFE
{
	public static class FunctionEvaluation
	{
		public static ISymbolValue Execute(MemberSymbol method, ISymbolValue[] arguments, AbstractSymbolValueProvider vp)
		{
			return new ErrorValue(new EvaluationException("CTFE is not implemented yet."));
		}
	}
}
