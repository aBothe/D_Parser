using System;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// a == b; a != b;
	/// </summary>
	public class EqualExpression : OperatorBasedExpression
	{
		public EqualExpression(bool isUnEqual)
		{
			OperatorToken = isUnEqual ? DTokens.NotEqual : DTokens.Equal;
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

