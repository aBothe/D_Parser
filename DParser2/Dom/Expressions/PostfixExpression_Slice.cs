using System;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// SliceExpression:
	///		PostfixExpression [ ]
	///		PostfixExpression [ AssignExpression .. AssignExpression ]
	/// </summary>
	public class PostfixExpression_Slice : PostfixExpression
	{
		public IExpression FromExpression;
		public IExpression ToExpression;

		public override string ToString()
		{
			var ret = PostfixForeExpression != null ? PostfixForeExpression.ToString() : "";

			ret += "[";

			if (FromExpression != null)
				ret += FromExpression.ToString();

			if (FromExpression != null && ToExpression != null)
				ret += "..";

			if (ToExpression != null)
				ret += ToExpression.ToString();

			return ret + "]";
		}

		public override CodeLocation EndLocation
		{
			get;
			set;
		}

		public override IExpression[] SubExpressions
		{
			get
			{
				return new[] { FromExpression, ToExpression };
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

		public override ulong GetHash()
		{
			ulong hashCode = base.GetHash();
			unchecked
			{
				if (FromExpression != null)
					hashCode += 1000000007 * FromExpression.GetHash();
				if (ToExpression != null)
					hashCode += 1000000009 * ToExpression.GetHash();
			}
			return hashCode;
		}
	}
}

