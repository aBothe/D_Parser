using System;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// a = b;
	/// a += b;
	/// a *= b; etc.
	/// </summary>
	public class AssignExpression : OperatorBasedExpression
	{
		public AssignExpression(byte opToken)
		{
			OperatorToken = opToken;
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

