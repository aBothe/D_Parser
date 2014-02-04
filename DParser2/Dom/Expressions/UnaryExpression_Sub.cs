using System;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	public class UnaryExpression_Sub : SimpleUnaryExpression
	{
		public override byte ForeToken
		{
			get { return DTokens.Minus; }
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

