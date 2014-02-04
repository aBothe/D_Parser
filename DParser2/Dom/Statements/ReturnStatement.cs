using System;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public class ReturnStatement : AbstractStatement,IExpressionContainingStatement
	{
		public IExpression ReturnExpression;

		public override string ToCode()
		{
			return "return" + (ReturnExpression == null ? "" : (' ' + ReturnExpression.ToString())) + ';';
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{ ReturnExpression }; }
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
}

