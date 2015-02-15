using System;

namespace D_Parser.Dom.Expressions
{
	public class PostfixExpression_Increment : PostfixExpression
	{
		public override string ToString()
		{
			return PostfixForeExpression.ToString() + "++";
		}

		public sealed override CodeLocation EndLocation
		{
			get;
			set;
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

