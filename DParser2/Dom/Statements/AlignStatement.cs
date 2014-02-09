using System;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public partial class AsmStatement
	{
		public sealed class AlignStatement : AbstractStatement
		{
			public IExpression ValueExpression { get; set; }

			public override string ToCode()
			{
				if (ValueExpression == null)
					return "align <NULL>";
				var ie = ValueExpression as IdentifierExpression;
				if (ie != null && ie.Value.Equals(2))
					return "even";
				else
					return "align " + ValueExpression.ToString();
			}

			public override void Accept(StatementVisitor vis) { vis.Visit(this); }
			public override R Accept<R>(StatementVisitor<R> vis) { return vis.Visit(this); }
		}
	}
}

