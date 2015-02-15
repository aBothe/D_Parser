using System;

namespace D_Parser.Dom.Expressions
{
	public class ArrayInitializer : AssocArrayExpression,IVariableInitializer
	{
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

