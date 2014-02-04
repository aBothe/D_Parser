using System;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	public class PowExpression : OperatorBasedExpression, UnaryExpression
	{
		public PowExpression()
		{
			OperatorToken = DTokens.Pow;
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

