using System;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// a * b; a / b; a % b;
	/// </summary>
	public class MulExpression : OperatorBasedExpression
	{
		public MulExpression(byte mulOperator)
		{
			OperatorToken = mulOperator;
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

