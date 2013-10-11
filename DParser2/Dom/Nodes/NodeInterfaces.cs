using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using D_Parser.Dom.Statements;

namespace D_Parser.Dom
{
	public interface IBlockNode: INode, IEnumerable<INode>
	{
		CodeLocation BlockStartLocation { get; set; }
		NodeDictionary Children { get; }

		void Add(INode Node);
		void AddRange(IEnumerable<INode> Nodes);
		int Count { get; }
		void Clear();

		IEnumerable<INode> this[string Name] { get; }
		IEnumerable<INode> this[int NameHash] { get; }
	}

	public interface INode : ISyntaxRegion, IVisitable<NodeVisitor>
	{
		string Name { get; set; }
		int NameHash { get; set; }
		CodeLocation NameLocation { get; set; }
		string Description { get; set; }
		ITypeDeclaration Type { get; set; }

		new CodeLocation Location { get; set; }
		new CodeLocation EndLocation { get; set; }

		/// <summary>
		/// Assigns a node's properties
		/// </summary>
		/// <param name="Other"></param>
		void AssignFrom(INode Other);

		INode Parent { get; set; }
		INode NodeRoot { get; }

		R Accept<R>(NodeVisitor<R> vis);
	}
}
