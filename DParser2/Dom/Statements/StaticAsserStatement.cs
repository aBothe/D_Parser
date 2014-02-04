using System;

namespace D_Parser.Dom.Statements
{
	public class StaticAssertStatement : AssertStatement, StaticStatement
	{
		public override string ToCode()
		{
			return "static " + base.ToCode();
		}

		public DAttribute[] Attributes
		{
			get;
			set;
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

