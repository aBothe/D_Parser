using System;
using System.Collections.Generic;

namespace D_Parser.Dom.Statements
{
	public class TryStatement : StatementContainingStatement
	{
		public CatchStatement[] Catches;
		public FinallyStatement FinallyStmt;

		public override IEnumerable<IStatement> SubStatements
		{
			get
			{
				if (ScopedStatement != null)
					yield return ScopedStatement;

				if (Catches != null && Catches.Length > 0)
					foreach (var c in Catches)
						yield return c;

				if (FinallyStmt != null)
					yield return FinallyStmt;
			}
		}

		public override string ToCode()
		{
			var ret = "try " + (ScopedStatement != null ? (' ' + ScopedStatement.ToCode()) : "");

			if (Catches != null && Catches.Length > 0)
				foreach (var c in Catches)
					ret += Environment.NewLine + c.ToCode();

			if (FinallyStmt != null)
				ret += Environment.NewLine + FinallyStmt.ToCode();

			return ret;
		}

		public override void Accept(IStatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public class CatchStatement : StatementContainingStatement,IDeclarationContainingStatement
		{
			public DVariable CatchParameter;

			public override string ToCode()
			{
				return "catch" + (CatchParameter != null ? ('(' + CatchParameter.ToString() + ')') : "")
					+ (ScopedStatement != null ? (' ' + ScopedStatement.ToCode()) : "");
			}

			public INode[] Declarations
			{
				get
				{
					if (CatchParameter == null)
						return null;
					return new[]{ CatchParameter }; 
				}
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

		public class FinallyStatement : StatementContainingStatement
		{
			public override string ToCode()
			{
				return "finally" + (ScopedStatement != null ? (' ' + ScopedStatement.ToCode()) : "");
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
}

