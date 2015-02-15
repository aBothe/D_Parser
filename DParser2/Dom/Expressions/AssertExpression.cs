using System;

namespace D_Parser.Dom.Expressions
{
	public class AssertExpression : PrimaryExpression,ContainerExpression
	{
		public IExpression[] AssignExpressions;

		public override string ToString()
		{
			var ret = "assert(";

			foreach (var e in AssignExpressions)
				ret += e.ToString() + ",";

			return ret.TrimEnd(',') + ")";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public IExpression[] SubExpressions
		{
			get { return AssignExpressions; }
		}

		public void Accept(ExpressionVisitor vis)
		{
			vis.Visit(this);
		}

		public R Accept<R>(ExpressionVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
}

