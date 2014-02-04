using System;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	public class UnaryExpression_Add : SimpleUnaryExpression
	{
		public override byte ForeToken
		{
			get { return DTokens.Plus; }
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

