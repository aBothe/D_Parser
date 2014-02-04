using System;

namespace D_Parser.Dom.Statements
{
	public class DebugSpecification : AbstractStatement, StaticStatement
	{
		public string SpecifiedId;
		public int SpecifiedDebugLevel;

		public override string ToCode()
		{
			return "debug = " + (SpecifiedId ?? SpecifiedDebugLevel.ToString()) + ";";
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public DAttribute[] Attributes
		{
			get;
			set;
		}
	}
}

