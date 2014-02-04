using System;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	public class MixinExpression : PrimaryExpression,ContainerExpression
	{
		public IExpression AssignExpression;

		public override string ToString()
		{
			return "mixin(" + AssignExpression.ToString() + ")";
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
			get { return new[]{ AssignExpression }; }
		}

		public void Accept(ExpressionVisitor vis)
		{
			vis.Visit(this);
		}

		public R Accept<R>(ExpressionVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public ulong GetHash()
		{
			ulong hashCode = DTokens.Mixin;
			unchecked
			{
				if (AssignExpression != null)
					hashCode += 1000000007 * AssignExpression.GetHash();
			}
			return hashCode;
		}
	}
}

