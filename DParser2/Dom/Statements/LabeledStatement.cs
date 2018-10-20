using System;

namespace D_Parser.Dom.Statements
{
	public class LabeledStatement : AbstractStatement
	{
		public int IdentifierHash;

		public string Identifier
		{
			get{ return Strings.TryGet(IdentifierHash); } 
			set
			{
				Strings.Add(value);
				IdentifierHash = value != null ? value.GetHashCode() : 0;
			}
		}

		public override string ToCode()
		{
			return Identifier + ":";
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

