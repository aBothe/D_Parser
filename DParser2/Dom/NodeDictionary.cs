using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Dom
{
	public class NodeDictionary : Dictionary<string, List<INode>>, IEnumerable<INode>
	{
		public void Add(INode Node)
		{
			var n = Node.Name ?? "";
			List<INode> l = null;

			if (!TryGetValue(n, out l))
				this[n] = l = new List<INode>();

			l.Add(Node);
		}

		public void AddRange(IEnumerable<INode> nodes)
		{
			if(nodes!=null)
				foreach (var n in nodes)
					Add(n);
		}

		public bool HasMultipleOverloads(string Name)
		{
			List<INode> l = null;

			if (TryGetValue(Name ?? "", out l))
				return l.Count > 1;

			return false;
		}

		IEnumerator<INode> IEnumerable<INode>.GetEnumerator()
		{
			foreach (var v in Values)
				foreach (var n in v)
					yield return n;
		}
	}
}
