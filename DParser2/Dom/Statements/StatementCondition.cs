using System;
using System.Text;
using System.Collections.Generic;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public class StatementCondition : StatementContainingStatement
	{
		public IStatement ElseStatement;
		public DeclarationCondition Condition;

		public override string ToCode()
		{
			var sb = new StringBuilder("if(");

			if (Condition != null)
				sb.Append(Condition.ToString());
			sb.AppendLine(")");

			if (ElseStatement != null)
				sb.Append(ElseStatement);

			return sb.ToString();
		}

		public override IEnumerable<IStatement> SubStatements
		{
			get
			{
				if (ElseStatement != null)
					yield return ElseStatement;
				if (ScopedStatement != null)
					yield return ScopedStatement;
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
}

