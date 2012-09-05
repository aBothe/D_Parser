using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using D_Parser.Dom.Statements;

namespace D_Parser.Dom
{
	public class DBlockNode : DNode, IBlockNode
	{
		protected readonly NodeDictionary _Children;

		public DBlockNode()
		{
			_Children = new NodeDictionary(this);
		}

		/// <summary>
		/// Used for storing import statement and similar stuff
		/// </summary>
		public readonly List<IStatement> StaticStatements = new List<IStatement>();

		public CodeLocation BlockStartLocation
		{
			get;
			set;
		}

		public NodeDictionary Children
		{
			get { return _Children; }
		}

		public IStatement[] Statements
		{
			get { return StaticStatements.ToArray(); }
		}

		public void Add(IStatement Statement)
		{
			StaticStatements.Add(Statement);
		}

		public void Add(INode Node)
		{
			_Children.Add(Node);
		}

		public void AddRange(IEnumerable<INode> Nodes)
		{
			_Children.AddRange(Nodes);
		}

		public int Count
		{
			get { return _Children.Count; }
		}

		public void Clear()
		{
			_Children.Clear();
		}

		public ReadOnlyCollection<INode> this[string Name]
		{
			get
			{
				return _Children[Name];
			}
		}

		public IEnumerator<INode> GetEnumerator()
		{
			return _Children.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public override void AssignFrom(INode other)
		{
			var bn = other as IBlockNode;

			if (bn != null)
			{
				BlockStartLocation = bn.BlockStartLocation;
				Clear();
				AddRange(bn);

				if (bn is DBlockNode)
				{
					StaticStatements.Clear();
					StaticStatements.AddRange(((DBlockNode)bn).StaticStatements);
				}
			}

			base.AssignFrom(other);
		}

		public override void Accept(NodeVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
}
