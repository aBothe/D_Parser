using System;

namespace D_Parser.Dom.Statements
{
	public class VolatileStatement : StatementContainingStatement
	{
		public override string ToCode()
		{
			return "volatile " + (ScopedStatement == null ? "" : ScopedStatement.ToCode());
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

