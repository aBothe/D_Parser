using System;
using System.Collections.Generic;

namespace D_Parser.Dom.Expressions
{
	public class Expression : IExpression, IEnumerable<IExpression>, ContainerExpression
	{
		public List<IExpression> Expressions = new List<IExpression>();

		public void Add(IExpression ex)
		{
			Expressions.Add(ex);
		}

		public IEnumerator<IExpression> GetEnumerator()
		{
			return Expressions.GetEnumerator();
		}

		public override string ToString()
		{
			var s = "";
			if (Expressions != null)
				foreach (var ex in Expressions)
					s += (ex == null ? string.Empty : ex.ToString()) + ",";
			return s.TrimEnd(',');
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return Expressions.GetEnumerator();
		}

		public CodeLocation Location
		{
			get { return Expressions.Count > 0 ? Expressions[0].Location : CodeLocation.Empty; }
		}

		public CodeLocation EndLocation
		{
			get { return Expressions.Count > 0 ? Expressions[Expressions.Count - 1].EndLocation : CodeLocation.Empty; }
		}

		public IExpression[] SubExpressions
		{
			get { return Expressions.ToArray(); }
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

