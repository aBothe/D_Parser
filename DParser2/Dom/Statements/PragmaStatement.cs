using System;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public class PragmaStatement : StatementContainingStatement,IExpressionContainingStatement
	{
		public PragmaAttribute Pragma;

		public IExpression[] SubExpressions
		{
			get { return Pragma == null ? null : Pragma.Arguments; }
		}

		public override string ToCode()
		{
			var r = Pragma == null ? "" : Pragma.ToString();

			r += ScopedStatement == null ? "" : (" " + ScopedStatement.ToCode());

			return r;
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

