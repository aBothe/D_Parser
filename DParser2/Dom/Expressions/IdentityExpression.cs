using System;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// a is b; a !is b;
	/// </summary>
	public class IdentityExpression : OperatorBasedExpression
	{
		public bool Not;

		public IdentityExpression(bool notIs)
		{
			Not = notIs;
			OperatorToken = DTokens.Is;
		}

		public override string ToString()
		{
			return LeftOperand.ToString() + (Not ? " !" : " ") + "is " + RightOperand.ToString();
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

