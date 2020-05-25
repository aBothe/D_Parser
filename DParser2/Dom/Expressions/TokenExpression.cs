using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	public class TokenExpression : PrimaryExpression
	{
		public readonly byte Token;

		public TokenExpression(byte token)
		{
			Token = token;
		}
		
		public TokenExpression(byte token, CodeLocation startLocation, CodeLocation endLocation) : this(token)
		{
			Location = startLocation;
			EndLocation = endLocation;
		}

		public override string ToString()
		{
			return DTokens.GetTokenString(Token);
		}

		public CodeLocation Location
		{
			get;
		}

		public CodeLocation EndLocation
		{
			get;
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

