using System;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public class WhileStatement : StatementContainingStatement, IExpressionContainingStatement
	{
		public IExpression Condition;

		public override CodeLocation EndLocation
		{
			get
			{
				if (ScopedStatement == null)
					return base.EndLocation;
				return ScopedStatement.EndLocation;
			}
			set
			{
				if (ScopedStatement == null)
					base.EndLocation = value;
			}
		}

		public override string ToCode()
		{
			var ret = "while(";

			if (Condition != null)
				ret += Condition.ToString();

			ret += ") " + Environment.NewLine;

			if (ScopedStatement != null)
				ret += ScopedStatement.ToCode();

			return ret;
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{ Condition }; }
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

