using System;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	public class ImportExpression : PrimaryExpression,ContainerExpression
	{
		public IExpression AssignExpression;

		public override string ToString()
		{
			return "import(" + AssignExpression.ToString() + ")";
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
			get { return new[]{ AssignExpression }; }
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

