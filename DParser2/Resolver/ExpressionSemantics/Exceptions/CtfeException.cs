using D_Parser.Dom.Expressions;

namespace D_Parser.Resolver.ExpressionSemantics.CTFE
{
	public class CtfeException : EvaluationException
	{
		public CtfeException(IExpression x, string Message, params ISemantic[] LastSubresults)
			: base(x, Message, LastSubresults)
		{
		}

		public CtfeException(string Message, params ISemantic[] LastSubresults)
			: base(Message, LastSubresults)
		{
		}
	}
}