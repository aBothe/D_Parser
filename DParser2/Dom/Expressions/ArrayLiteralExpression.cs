using System;
using D_Parser.Parser;
using System.Collections.Generic;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// auto arr= [1,2,3,4,5,6];
	/// </summary>
	public class ArrayLiteralExpression : PrimaryExpression,ContainerExpression
	{
		public readonly List<IExpression> Elements = new List<IExpression>();

		public ArrayLiteralExpression(List<IExpression> elements)
		{
			Elements = elements;
		}

		public override string ToString()
		{
			var s = "[";

			//HACK: To prevent exessive string building flood, limit element count to 100
			if (Elements != null)
				for (int i = 0; i < Elements.Count; i++)
				{
					s += Elements[i].ToString() + ", ";
					if (i == 100)
					{
						s += "...";
						break;
					}
				}
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
			get { return Elements != null && Elements.Count > 0 ? Elements.ToArray() : null; }
		}

		public void Accept(ExpressionVisitor vis)
		{
			vis.Visit(this);
		}

		public R Accept<R>(ExpressionVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
}

