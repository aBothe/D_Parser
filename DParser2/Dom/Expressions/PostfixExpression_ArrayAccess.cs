using System;
using System.Collections.Generic;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// SliceExpression:
	/// IndexExpression:
	///		PostfixExpression [ ArgumentList ]
	/// </summary>
	public class PostfixExpression_ArrayAccess : PostfixExpression
	{
		public readonly IndexArgument[] Arguments;

		public PostfixExpression_ArrayAccess(IndexArgument[] args)
		{
			Arguments = args;
		}
		public PostfixExpression_ArrayAccess(IExpression indexExpression)
		{
			Arguments = new[] { new IndexArgument(indexExpression) };
		}
		public PostfixExpression_ArrayAccess(IExpression lower, IExpression upper)
		{
			Arguments = new [] { new SliceArgument(lower, upper) };
		}

		public class IndexArgument
		{
			public readonly IExpression Expression;

			public IndexArgument(IExpression x)
			{
				if(x == null)
					throw new ArgumentNullException("x");
				Expression = x;
			}

			public override string ToString ()
			{
				return Expression.ToString();
			}
		}

		public class SliceArgument : IndexArgument
		{
			public IExpression UpperBoundExpression;
			/// <summary>
			/// Aliases IndexArgument.Expression
			/// </summary>
			public IExpression LowerBoundExpression
			{
				get{ return Expression; }
			}

			public SliceArgument(IExpression lower, IExpression upper) : base(lower)
			{
				if(upper == null)
					throw new ArgumentNullException("upper");
				UpperBoundExpression = upper;
			}

			public override string ToString ()
			{
				return LowerBoundExpression.ToString () + ".." + (UpperBoundExpression != null ? UpperBoundExpression.ToString () : string.Empty);
			}
		}



		public override string ToString()
		{
			var ret = (PostfixForeExpression != null ? PostfixForeExpression.ToString() : "") + "[";

			if (Arguments != null)
				foreach (var a in Arguments)
					if (a != null)
						ret += a.ToString() + ",";

			return ret.TrimEnd(',') + "]";
		}

		public override CodeLocation EndLocation
		{
			get;
			set;
		}

		public override IEnumerable<IExpression> SubExpressions
		{
			get
			{
				if (PostfixForeExpression != null)
					yield return PostfixForeExpression;

				if (Arguments != null)
					foreach (var arg in Arguments) {
						yield return arg.Expression;
						if (arg is SliceArgument)
							yield return (arg as SliceArgument).UpperBoundExpression;
					}
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

