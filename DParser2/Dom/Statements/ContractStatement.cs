using System;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public class ContractStatement : StatementContainingStatement, IExpressionContainingStatement
	{
		public IExpression Condition;
		public IExpression Message;
		public bool isOut; // true for Out
		public IdentifierDeclaration OutResultVariable;

		public override string ToCode()
		{
			string s = isOut ? "out" : "in";
			if (Condition != null)
			{
				s += "(";
				if (OutResultVariable != null)
					s += OutResultVariable.ToString();
				if (isOut)
					s += ";";
				s += Condition.ToString();
				if (Message != null)
					s += "," + Message.ToString();
				s += ")";
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
			get {
				if (Condition != null && Message != null)
					return new[] { Condition, Message };
				if (Condition != null)
					return new[] { Condition };
				return new Expression[0];
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

