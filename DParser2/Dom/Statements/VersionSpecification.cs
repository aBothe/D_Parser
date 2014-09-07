using System;

namespace D_Parser.Dom.Statements
{
	public class VersionSpecification : AbstractStatement, StaticStatement
	{
		public string SpecifiedId;
		public ulong SpecifiedNumber;

		public override string ToCode()
		{
			return "version = " + (SpecifiedId ?? SpecifiedNumber.ToString()) + ";";
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

