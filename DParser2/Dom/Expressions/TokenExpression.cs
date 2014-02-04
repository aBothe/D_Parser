using System;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	public class TokenExpression : PrimaryExpression
	{
		public byte Token = DTokens.INVALID;

		public TokenExpression()
		{
		}

		public TokenExpression(byte T)
		{
			Token = T;
		}

		public override string ToString()
		{
			return DTokens.GetTokenString(Token);
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
			unchecked
			{
				return 1000000007 * (ulong)Token;
			}
		}
	}
}

