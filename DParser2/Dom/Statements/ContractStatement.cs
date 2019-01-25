using System;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public class ContractStatement : StatementContainingStatement, IExpressionContainingStatement
	{
		public IExpression Expression;
		public bool isOut; // true for Out
		public IdentifierDeclaration OutResultVariable;

		public override string ToCode()
		{
			string s = isOut ? "out" : "in";
			if (Expression != null)
			{
				s += "(";
				if (OutResultVariable != null)
					s += OutResultVariable.ToString();
				if (isOut)
					s += "; ";
				s += Expression.ToString() + ")";
			}
			else if (ScopedStatement != null)
			{
				if (OutResultVariable != null)
					s += "(" + OutResultVariable.ToString() + ")";
				s += Environment.NewLine + ScopedStatement.ToString();
			}
			return s;
		}

		public IExpression[] SubExpressions
		{
			get { return Expression != null ? new[] { Expression } : new Expression[0]; }
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

