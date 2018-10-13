namespace D_Parser.Resolver.ExpressionSemantics.Exceptions
{
	public class VariableNotInitializedException : EvaluationException
	{
		public VariableNotInitializedException(string Message, params ISemantic[] LastSubresults) : base(Message, LastSubresults)
		{
		}
	}
}