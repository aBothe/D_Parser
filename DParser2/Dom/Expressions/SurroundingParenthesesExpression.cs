using System;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// ( Expression )
	/// </summary>
	public class SurroundingParenthesesExpression : PrimaryExpression,ContainerExpression
	{
		public IExpression Expression;

		public override string ToString()
		{
			return "(" + (Expression != null ? Expression.ToString() : string.Empty) + ")";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{ Expression }; }
		}

		public void Accept(ExpressionVisitor vis)
		{
			vis.Visit(this);
		}

		public R Accept<R>(ExpressionVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
}

