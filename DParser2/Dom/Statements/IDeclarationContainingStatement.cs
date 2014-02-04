using System;

namespace D_Parser.Dom.Statements
{
	public interface IDeclarationContainingStatement : IStatement
	{
		INode[] Declarations { get; }
	}
}

