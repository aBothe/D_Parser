using System;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// a ^ b;
	/// </summary>
	public class XorExpression : OperatorBasedExpression
	{
		public XorExpression()
		{
			OperatorToken = DTokens.Xor;
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

