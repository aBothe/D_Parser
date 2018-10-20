using System;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public class WithStatement : StatementContainingStatement, IExpressionContainingStatement
	{
		public IExpression WithExpression;
		public ITypeDeclaration WithSymbol;

		public override string ToCode()
		{
			var ret = "with(";

			if (WithExpression != null)
				ret += WithExpression.ToString();
			else if (WithSymbol != null)
				ret += WithSymbol.ToString();

			ret += ')';

			if (ScopedStatement != null)
				ret += ScopedStatement.ToCode();

			return ret;
		}

		public IExpression[] SubExpressions
		{
			get
			{
				return new[] { WithExpression };
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

