using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver.ExpressionSemantics.CTFE
{
	public class FunctionEvaluation
	{
		public static ISymbolValue Execute(MemberSymbol method, ISymbolValue[] arguments, AbstractSymbolValueProvider vp)
		{
			return new ErrorValue(new EvaluationException("CTFE is not implemented yet."));
		}
	}
}
