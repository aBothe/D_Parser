using System;

namespace D_Parser.Dom.Statements
{
	public partial class AsmStatement : StatementContainingStatement
	{
		public bool Naked { get; set; }
		public AbstractStatement[] Instructions;

		public override string ToCode()
		{
			var ret = "asm {";

			if (Instructions != null && Instructions.Length > 0)
			{
				foreach (var i in Instructions)
					ret += Environment.NewLine + i.ToCode() + ';';
				ret += Environment.NewLine;
			}

			return ret + '}';
		}

		public override void Accept(StatementVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(StatementVisitor<R> vis) { return vis.Visit(this); }
	}
}

