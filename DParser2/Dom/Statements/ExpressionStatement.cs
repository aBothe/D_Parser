using System;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public class ExpressionStatement : AbstractStatement,IExpressionContainingStatement
	{
		public IExpression Expression;

		public override string ToCode()
		{
			return Expression.ToString() + ';';
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{ Expression }; }
		}

		public override void Accept(IStatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
}

