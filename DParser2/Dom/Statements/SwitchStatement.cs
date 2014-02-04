using System;
using D_Parser.Dom.Expressions;
using System.Collections.Generic;

namespace D_Parser.Dom.Statements
{
	public class SwitchStatement : StatementContainingStatement, IExpressionContainingStatement
	{
		public bool IsFinal;
		public IExpression SwitchExpression;

		public IExpression[] SubExpressions
		{
			get { return new[] { SwitchExpression }; }
		}

		public override string ToCode()
		{
			var ret = "switch(";

			if (SwitchExpression != null)
				ret += SwitchExpression.ToString();

			ret += ')';

			if (ScopedStatement != null)
				ret += ' ' + ScopedStatement.ToCode();

			return ret;
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public class CaseStatement : StatementContainingStatement, IExpressionContainingStatement, IDeclarationContainingStatement
		{
			public bool IsCaseRange
			{
				get { return LastExpression != null; }
			}

			public IExpression ArgumentList;
			/// <summary>
			/// Used for CaseRangeStatements
			/// </summary>
			public IExpression LastExpression;
			public IStatement[] ScopeStatementList;

			public override string ToCode()
			{
				var ret = "case " + ArgumentList.ToString() + ':' + (IsCaseRange ? (" .. case " + LastExpression.ToString() + ':') : "") + Environment.NewLine;

				foreach (var s in ScopeStatementList)
					ret += s.ToCode() + Environment.NewLine;

				return ret;
			}

			public IExpression[] SubExpressions
			{
				get { return new[]{ ArgumentList, LastExpression }; }
			}

			public override IEnumerable<IStatement> SubStatements
			{
				get
				{
					return ScopeStatementList;
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

			public INode[] Declarations
			{
				get
				{
					return BlockStatement.GetDeclarations(ScopeStatementList).ToArray();
				}
			}
		}

		public class DefaultStatement : StatementContainingStatement, IDeclarationContainingStatement
		{
			public IStatement[] ScopeStatementList;

			public override IEnumerable<IStatement> SubStatements
			{
				get
				{
					return ScopeStatementList;
				}
			}

			public override string ToCode()
			{
				var ret = "default:" + Environment.NewLine;

				foreach (var s in ScopeStatementList)
					ret += s.ToCode() + Environment.NewLine;

				return ret;
			}

			public override void Accept(StatementVisitor vis)
			{
				vis.Visit(this);
			}

			public override R Accept<R>(StatementVisitor<R> vis)
			{
				return vis.Visit(this);
			}

			public INode[] Declarations
			{
				get
				{
					return BlockStatement.GetDeclarations(ScopeStatementList).ToArray();
				}
			}
		}
	}
}

