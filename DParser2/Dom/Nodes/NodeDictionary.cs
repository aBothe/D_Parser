using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace D_Parser.Dom
{
	/// <summary>
	/// Stores node children. Thread safe.
	/// </summary>
	public class NodeDictionary : IList<INode>
	{
		volatile ConcurrentDictionary<int, List<INode>> nameDict = new ConcurrentDictionary<int, List<INode>>();
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

		public void Insert(int i, INode Node)
		{
			children.Insert(i,Node);

			// Alter the node's parent
			if (ParentNode != null)
				Node.Parent = ParentNode;

			List<INode> l;
		TryGet:
			if (!nameDict.TryGetValue (Node.NameHash, out l))
			{
				// This may look like it has the possibility to loop
				// infinitely, but the circumstances required for it
				// to do so are very very very very specific.
				if (!nameDict.TryAdd(Node.NameHash, l = new List<INode>()))
					goto TryGet;
			}

			l.Add(Node);
		}

		public void Add(INode Node)
		{
			// Alter the node's parent
			if (ParentNode != null)
				Node.Parent = ParentNode;

			List<INode> l;

		TryGet:
			if (!nameDict.TryGetValue (Node.NameHash, out l))
			{
				// This may look like it has the possibility to loop
				// infinitely, but the circumstances required for it
				// to do so are very very very very specific.
				if (!nameDict.TryAdd(Node.NameHash, l = new List<INode>()))
					goto TryGet;
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
			
			List<INode> l;
			if(nameDict.TryGetValue(n.NameHash, out l))
			{
				gotRemoved = l.Remove(n) || gotRemoved;
				if (l.Count == 0)
					nameDict.TryRemove(n.NameHash, out l);
			}

			return gotRemoved;
		}

		public void Clear()
		{
			nameDict = new ConcurrentDictionary<int, List<INode>> ();
			children.Clear();
			children.TrimExcess ();
		}

		public int Count => children.Count;

		public bool HasMultipleOverloads(int NameHash)
		{
			List<INode> l;
			return nameDict.TryGetValue(NameHash, out l) && l.Count > 1;
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
			if (nameDict.TryGetValue (nameHash, out l))
				return l;
			return Enumerable.Empty<INode>();
		}

		public INode ItemAt(int index)
		{
			return children [index];
		}

		public INode this[int Index]
		{
			get
			{
				return children[Index];
			}
			set
			{
				throw new System.NotImplementedException();
			}
		}

		class AscNodeLocationComparer : Comparer<INode>
		{
			public override int Compare (INode x, INode y)
			{
				if (x == y)
					return 0;
				return x.Location < y.Location ? -1 : 1;
			}
		}

		class DescNodeLocationComparer : Comparer<INode>
		{
			public override int Compare (INode x, INode y)
			{
				if (x == y)
					return 0;
				return x.Location > y.Location ? -1 : 1;
			}
		}

		public void Sort(bool asc = true)
		{
			children.Sort (asc ? new AscNodeLocationComparer() as IComparer<INode> : new DescNodeLocationComparer());
		}

		public int IndexOf(INode item)
		{
			return children.IndexOf(item);
		}

		public void RemoveAt(int index)
		{
			Remove(ItemAt(index));
		}

		public bool Contains(INode item)
		{
			return children.Contains(item);
		}

		public void CopyTo(INode[] array, int arrayIndex)
		{
			children.CopyTo(array, arrayIndex);
		}

		public bool IsReadOnly
		{
			get { return false; }
		}
	}
}
