using System;

namespace D_Parser.Dom.Statements
{
	public class ScopeGuardStatement : StatementContainingStatement
	{
		public const string ExitScope = "exit";
		public const string SuccessScope = "success";
		public const string FailureScope = "failure";
		public string GuardedScope = ExitScope;

		public override string ToCode()
		{
			return "scope(" + GuardedScope + ')' + (ScopedStatement == null ? "" : ScopedStatement.ToCode());
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

