using System;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// a &lt;&gt;= b etc.
	/// </summary>
	public class RelExpression : OperatorBasedExpression
	{
		public RelExpression(byte relationalOperator)
		{
			OperatorToken = relationalOperator;
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

