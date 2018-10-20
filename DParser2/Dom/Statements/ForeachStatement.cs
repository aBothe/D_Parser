using System;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public class ForeachStatement : StatementContainingStatement,  IExpressionContainingStatement, IDeclarationContainingStatement
	{
		public bool IsRangeStatement
		{
			get { return UpperAggregate != null; }
		}

		public bool IsReverse = false;
		public DVariable[] ForeachTypeList;

		public INode[] Declarations
		{
			get { return ForeachTypeList; }
		}

		public IExpression Aggregate;
		/// <summary>
		/// Used in ForeachRangeStatements. The Aggregate field will be the lower expression then.
		/// </summary>
		public IExpression UpperAggregate;

		public IExpression[] SubExpressions
		{
			get
			{
				if (Aggregate != null)
				{
					if (UpperAggregate == null)
						return new[]{ Aggregate };
					return new[]{ Aggregate, UpperAggregate };
				}
				if (UpperAggregate != null)
					return new[]{ UpperAggregate };
				return new IExpression[0];
			}
		}

		public override string ToCode()
		{
			var ret = (IsReverse ? "foreach_reverse" : "foreach") + '(';

			if (ForeachTypeList != null)
			{
				foreach (var v in ForeachTypeList)
					ret += v.ToString() + ',';

				ret = ret.TrimEnd(',') + ';';
			}

			if (Aggregate != null)
				ret += Aggregate.ToString();

			if (UpperAggregate != null)
				ret += ".." + UpperAggregate.ToString();

			ret += ')';

			if (ScopedStatement != null)
				ret += ' ' + ScopedStatement.ToCode();

			return ret;
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

