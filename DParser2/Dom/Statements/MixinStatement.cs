using System;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public class MixinStatement : AbstractStatement,IExpressionContainingStatement,StaticStatement
	{
		public IExpression MixinExpression;

		public override string ToCode()
		{
			return "mixin(" + (MixinExpression == null ? "" : MixinExpression.ToString()) + ");";
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{ MixinExpression }; }
		}

		public override void Accept(IStatementVisitor vis)
		{
			vis.VisitMixinStatement(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.VisitMixinStatement(this);
		}

		public DAttribute[] Attributes{ get; set; }
	}
}

