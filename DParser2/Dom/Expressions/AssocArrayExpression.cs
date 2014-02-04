using System;
using System.Collections.Generic;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// auto arr=['a':0xa, 'b':0xb, 'c':0xc, 'd':0xd, 'e':0xe, 'f':0xf];
	/// </summary>
	public class AssocArrayExpression : PrimaryExpression,ContainerExpression
	{
		public IList<KeyValuePair<IExpression, IExpression>> Elements = new List<KeyValuePair<IExpression, IExpression>>();

		public override string ToString()
		{
			var s = "[";
			foreach (var expr in Elements)
				s += expr.Key.ToString() + ":" + expr.Value.ToString() + ", ";
			s = s.TrimEnd(' ', ',') + "]";
			return s;
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public IExpression[] SubExpressions
		{
			get
			{
				var l = new List<IExpression>();

				foreach (var kv in Elements)
				{
					if (kv.Key != null)
						l.Add(kv.Key);
					if (kv.Value != null)
						l.Add(kv.Value);
				}

				return l.Count > 0 ? l.ToArray() : null;
			}
		}

		public virtual void Accept(ExpressionVisitor vis)
		{
			vis.Visit(this);
		}

		public virtual R Accept<R>(ExpressionVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public virtual ulong GetHash()
		{
			ulong hashCode = 0uL;
			unchecked
			{
				if (Elements != null && Elements.Count != 0)
					foreach (var e in Elements)
					{
						hashCode += 1000000007 * e.Key.GetHash();
						hashCode += 1000000009 * e.Value.GetHash();
					}
			}
			return hashCode;
		}
	}
}

