using System;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// a &amp; b;
	/// </summary>
	public class AndExpression : OperatorBasedExpression
	{
		public AndExpression()
		{
			OperatorToken = DTokens.BitwiseAnd;
		}

		public override void Accept(ExpressionVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(ExpressionVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
}

