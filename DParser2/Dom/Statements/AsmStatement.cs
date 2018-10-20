using System;
using System.Collections.Generic;

namespace D_Parser.Dom.Statements
{
	public class AsmStatement : StatementContainingStatement
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

		public override IEnumerable<IStatement> SubStatements { get { return Instructions; } }

		public override void Accept(IStatementVisitor vis) { vis.VisitAsmStatement(this); }
		public override R Accept<R>(StatementVisitor<R> vis) { return vis.VisitAsmStatement(this); }
	}
}

