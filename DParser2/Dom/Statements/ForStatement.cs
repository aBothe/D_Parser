using System;
using D_Parser.Dom.Expressions;
using System.Collections.Generic;

namespace D_Parser.Dom.Statements
{
	public class ForStatement : StatementContainingStatement, IDeclarationContainingStatement, IExpressionContainingStatement
	{
		public IStatement Initialize;
		public IExpression Test;
		public IExpression Increment;

		public IExpression[] SubExpressions
		{
			get { return new[] { Test, Increment }; }
		}

		public override IEnumerable<IStatement> SubStatements
		{
			get
			{
				if (Initialize != null)
					yield return Initialize;
				if (ScopedStatement != null)
					yield return ScopedStatement;
			}
		}

		public override string ToCode()
		{
			var ret = "for(";

			if (Initialize != null)
				ret += Initialize.ToCode();

			ret += ';';

			if (Test != null)
				ret += Test.ToString();

			ret += ';';

			if (Increment != null)
				ret += Increment.ToString();

			ret += ')';

			if (ScopedStatement != null)
				ret += ' ' + ScopedStatement.ToCode();

			return ret;
		}

		public INode[] Declarations
		{
			get
			{
				if (Initialize is DeclarationStatement)
					return (Initialize as DeclarationStatement).Declarations;

				return null;
			}
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

