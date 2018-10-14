namespace D_Parser.Resolver.ExpressionSemantics.Exceptions
{
	public class EvaluationStackOverflowException : EvaluationException
	{
		public EvaluationStackOverflowException(string Message, params ISemantic[] LastSubresults)
			: base(Message, LastSubresults)
		{
		}
	}
}