using System;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// a >> b; a &lt;&lt; b; a >>> b;
	/// </summary>
	public class ShiftExpression : OperatorBasedExpression
	{
		public ShiftExpression(byte shiftOperator)
		{
			OperatorToken = shiftOperator;
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

