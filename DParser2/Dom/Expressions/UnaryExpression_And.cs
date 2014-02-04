using System;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// Creates a pointer from the trailing type
	/// </summary>
	public class UnaryExpression_And : SimpleUnaryExpression
	{
		public override byte ForeToken
		{
			get { return DTokens.BitwiseAnd; }
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

