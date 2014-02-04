using System;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	public abstract class SimpleUnaryExpression : UnaryExpression, ContainerExpression
	{
		public abstract byte ForeToken { get; }

		public IExpression UnaryExpression { get; set; }

		public override string ToString()
		{
			return DTokens.GetTokenString(ForeToken) + UnaryExpression.ToString();
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get { return UnaryExpression.EndLocation; }
		}

		public virtual IExpression[] SubExpressions
		{
			get { return new[]{ UnaryExpression }; }
		}

		public abstract void Accept(ExpressionVisitor vis);

		public abstract R Accept<R>(ExpressionVisitor<R> vis);

		public ulong GetHash()
		{
			ulong hashCode;
			unchecked
			{
				hashCode = 1000000007 * (ulong)ForeToken;
				if (UnaryExpression != null)
					hashCode += 1000000009 * UnaryExpression.GetHash();
			}
			return hashCode;
		}
	}
}

