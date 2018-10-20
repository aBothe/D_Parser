using System;
using D_Parser.Dom.Expressions;
using System.Collections.Generic;
using System.Text;

namespace D_Parser.Dom.Statements
{
	public class IfStatement : StatementContainingStatement,IDeclarationContainingStatement,IExpressionContainingStatement
	{
		public IExpression IfCondition;
		public DVariable IfVariable;

		public IStatement ThenStatement
		{
			get { return ScopedStatement; }
			set { ScopedStatement = value; }
		}

		public IStatement ElseStatement;

		public override IEnumerable<IStatement> SubStatements
		{
			get
			{
				if (ThenStatement != null)
					yield return ThenStatement;
				if (ElseStatement != null)
					yield return ElseStatement;
			}
		}

		public override CodeLocation EndLocation
		{
			get
			{
				if (ScopedStatement == null)
					return base.EndLocation;
				return ElseStatement != null ? ElseStatement.EndLocation : ScopedStatement.EndLocation;
			}
			set
			{
				if (ScopedStatement == null)
					base.EndLocation = value;
			}
		}

		public override string ToCode()
		{
			var sb = new StringBuilder("if(");

			if (IfCondition != null)
				sb.Append(IfCondition.ToString());
			else if (IfVariable != null)
				sb.Append(IfVariable.ToString(true, false, true));

			sb.AppendLine(")");

			if (ScopedStatement != null)
				sb.Append(ScopedStatement.ToCode());

			if (ElseStatement != null)
				sb.AppendLine().Append("else ").Append(ElseStatement.ToCode());

			return sb.ToString();
		}

		public IExpression[] SubExpressions
		{
			get
			{
				return new[] { IfCondition };
			}
		}

		public INode[] Declarations
		{
			get
			{ 
				return IfVariable == null ? null : new[]{ IfVariable };
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

