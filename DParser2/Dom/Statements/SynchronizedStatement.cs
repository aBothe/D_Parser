using System;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public class SynchronizedStatement : StatementContainingStatement,IExpressionContainingStatement
	{
		public IExpression SyncExpression;

		public override string ToCode()
		{
			var ret = "synchronized";

			if (SyncExpression != null)
				ret += '(' + SyncExpression.ToString() + ')';

			if (ScopedStatement != null)
				ret += ' ' + ScopedStatement.ToCode();

			return ret;
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{ SyncExpression }; }
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

