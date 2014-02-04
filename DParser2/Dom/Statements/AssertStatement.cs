using System;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public class AssertStatement : AbstractStatement,IExpressionContainingStatement
	{
		public IExpression AssertedExpression;
		public IExpression Message;

		public override string ToCode()
		{
			return "assert(" + (AssertedExpression != null ? AssertedExpression.ToString() : "") +
				(Message == null ? "" : ("," + Message)) + ");";
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{ AssertedExpression }; }
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

