using System;

namespace D_Parser.Dom.Statements
{
	public interface IStatement : ISyntaxRegion, IVisitable<IStatementVisitor>
	{
		new CodeLocation Location { get; set; }

		new CodeLocation EndLocation { get; set; }

		IStatement Parent { get; set; }

		INode ParentNode { get; set; }

		string ToCode();

		R Accept<R>(StatementVisitor<R> vis);
	}
}

