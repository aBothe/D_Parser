using System;
using System.Collections.Generic;

namespace D_Parser.Dom.Expressions
{
	public sealed class UnaryExpression_SegmentBase : SimpleUnaryExpression
	{
		public IExpression RegisterExpression { get; set; }
		// This should never be called for this.
		public override byte ForeToken { get { throw new NotSupportedException(); } }

		public override string ToString()
		{
			return RegisterExpression.ToString() + ":" + UnaryExpression.ToString();
		}

		public override CodeLocation Location
		{
			get { return RegisterExpression.Location; }
			set { throw new NotSupportedException(); }
		}

		public override IEnumerable<IExpression> SubExpressions
		{
			get
			{
				if (RegisterExpression != null)
					yield return RegisterExpression;
				if (UnaryExpression != null)
					yield return UnaryExpression;
			}
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}
}

