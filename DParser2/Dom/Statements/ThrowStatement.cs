using System;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public class ThrowStatement : AbstractStatement,IExpressionContainingStatement
	{
		public IExpression ThrowExpression;

		public override string ToCode()
		{
			return "throw" + (ThrowExpression == null ? "" : (' ' + ThrowExpression.ToString())) + ';';
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{ ThrowExpression }; }
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

