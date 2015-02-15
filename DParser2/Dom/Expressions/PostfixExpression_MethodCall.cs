using System;
using System.Text;
using System.Collections.Generic;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// PostfixExpression ( )
	/// PostfixExpression ( ArgumentList )
	/// </summary>
	public class PostfixExpression_MethodCall : PostfixExpression
	{
		public IExpression[] Arguments;

		public int ArgumentCount
		{
			get { return Arguments == null ? 0 : Arguments.Length; }
		}

		public override string ToString()
		{
			var sb = new StringBuilder(PostfixForeExpression.ToString());
			sb.Append('(');

			if (Arguments != null)
				foreach (var a in Arguments)
					if (a != null)
						sb.Append(a.ToString()).Append(',');

			if (sb[sb.Length - 1] == ',')
				sb.Remove(sb.Length - 1, 1);

			return sb.Append(')').ToString();
		}

		public sealed override CodeLocation EndLocation
		{
			get;
			set;
		}

		public override IExpression[] SubExpressions
		{
			get
			{
				var l = new List<IExpression>();

				if (Arguments != null)
					l.AddRange(Arguments);

				if (PostfixForeExpression != null)
					l.Add(PostfixForeExpression);

				return l.Count > 0 ? l.ToArray() : null;
			}
		}

		public override void Accept(ExpressionVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(ExpressionVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
}

