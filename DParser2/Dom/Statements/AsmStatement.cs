using System;

namespace D_Parser.Dom.Statements
{
	public class AsmStatement : AbstractStatement
	{
		/// <summary>
		/// TODO: Put the instructions into extra ISyntaxRegions
		/// </summary>
		public string[] Instructions;

		public override string ToCode()
		{
			var ret = "asm {";

			if (Instructions != null && Instructions.Length > 0)
			{
				foreach (var i in Instructions)
					ret += Environment.NewLine + i + ';';
				ret += Environment.NewLine;
			}

			return ret + '}';
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

