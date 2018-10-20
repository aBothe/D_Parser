using System;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public sealed class AsmAlignStatement : AbstractStatement
	{
		public IExpression ValueExpression { get; set; }

		public override string ToCode()
		{
			if (ValueExpression == null)
				return "align <NULL>";
			var ie = ValueExpression as ScalarConstantExpression;
			if (ie != null && ie.Value.Equals(2m))
				return "even";
			else
				return "align " + ValueExpression.ToString();
		}

		public override void Accept(IStatementVisitor vis) { vis.VisitAsmAlignStatement(this); }
		public override R Accept<R>(StatementVisitor<R> vis) { return vis.VisitAsmAlignStatement(this); }
	}
}

