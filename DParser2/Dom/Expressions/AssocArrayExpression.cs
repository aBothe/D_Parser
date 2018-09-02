using System;
using System.Collections.Generic;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// auto arr=['a':0xa, 'b':0xb, 'c':0xc, 'd':0xd, 'e':0xe, 'f':0xf];
	/// </summary>
	public class AssocArrayExpression : PrimaryExpression, ContainerExpression
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

		public IEnumerable<IExpression> SubExpressions
		{
			get
			{
				foreach (var kv in Elements)
				{
					if (kv.Key != null)
						yield return kv.Key;
					if (kv.Value != null)
						yield return kv.Value;
				}
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
	}
}

