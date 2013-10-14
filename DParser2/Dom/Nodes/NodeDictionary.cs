using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;

namespace D_Parser.Dom
{
	/// <summary>
	/// Stores node children. Thread safe.
	/// </summary>
	public class NodeDictionary : IEnumerable<INode>
	{
		ConcurrentDictionary<int, List<INode>> nameDict = new ConcurrentDictionary<int, List<INode>>();
		/// <summary>
		/// For faster enum access, store a separate list of INodes
		/// </summary>
		List<INode> children = new List<INode>();
		public readonly INode ParentNode;

		public NodeDictionary() { }
		public NodeDictionary(INode parent)
		{
			ParentNode = parent;
		}

		public void Add(INode Node)
		{
			// Alter the node's parent
			if (ParentNode != null)
				Node.Parent = ParentNode;

			var n = Node.NameHash;
			List<INode> l;

			if (!nameDict.TryGetValue (n, out l)) {
				nameDict [n] = l = new List<INode> ();
			}

			l.Add(Node);
			children.Add(Node);
		}

		public void AddRange(IEnumerable<INode> nodes)
		{
			if(nodes!=null)
				foreach (var n in nodes)
					Add(n);
			children.TrimExcess ();
		}

		public bool Remove(INode n)
		{
			var gotRemoved = children.Remove(n);
			
			var Name = n.NameHash;
			List<INode> l;
			if(nameDict.TryGetValue(Name, out l))
			{
				gotRemoved = l.Remove(n) || gotRemoved;
				if (l.Count == 0)
					nameDict[Name] = null;
			}

			return gotRemoved;
		}

		public void Clear()
		{
			nameDict.Clear();
			children.Clear();
			children.TrimExcess ();
		}

		public int Count
		{
			get
			{
				return children.Count;
			}
		}

		public bool HasMultipleOverloads(int NameHash)
		{
			List<INode> l;

			if (nameDict.TryGetValue(NameHash, out l))
				return l.Count > 1;

			return false;
		}

		public bool HasMultipleOverloads(string Name)
		{
			return HasMultipleOverloads (Name.GetHashCode ());
		}

		public IEnumerator<INode> GetEnumerator()
		{
			return children.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return children.GetEnumerator();
		}

		public IEnumerable<INode> this[string Name]
		{
			get
			{
				return GetNodes ((Name ?? string.Empty).GetHashCode ());
			}
		}

		public IEnumerable<INode> GetNodes(string Name)
		{
			return GetNodes((Name ?? string.Empty).GetHashCode ());
		}

		public IEnumerable<INode> GetNodes(int nameHash)
		{
			List<INode> l;
			nameDict.TryGetValue (nameHash, out l);
			return l;
		}

		public INode this[int Index]
		{
			get
			{
				return children[Index];
			}
		}
	}
}
