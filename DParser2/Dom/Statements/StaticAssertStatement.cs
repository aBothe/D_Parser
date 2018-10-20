using D_Parser.Dom.Expressions;
using System;

namespace D_Parser.Dom.Statements
{
    public class StaticAssertStatement : AbstractStatement, IExpressionContainingStatement, StaticStatement
	{
        public IExpression AssertedExpression;
        public IExpression Message;

        public override string ToCode()
        {
            return "static assert(" + (AssertedExpression != null ? AssertedExpression.ToString() : "") +
                (Message == null ? "" : ("," + Message)) + ");";
        }

        public IExpression[] SubExpressions
        {
            get { return new[] { AssertedExpression }; }
        }

		public DAttribute[] Attributes
		{
			get;
			set;
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

