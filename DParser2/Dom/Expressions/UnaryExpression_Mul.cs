using System;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// Gets the pointer base type
	/// </summary>
	public class UnaryExpression_Mul : SimpleUnaryExpression
	{
		public override byte ForeToken
		{
			get { return DTokens.Times; }
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

