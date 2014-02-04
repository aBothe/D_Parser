using System;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	public class TypeidExpression : PrimaryExpression,ContainerExpression
	{
		public ITypeDeclaration Type;
		public IExpression Expression;

		public override string ToString()
		{
			return "typeid(" + (Type != null ? Type.ToString() : Expression.ToString()) + ")";
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
			get
			{ 
				if (Expression != null)
					return new[]{ Expression };
				if (Type != null)
					return new[] { new TypeDeclarationExpression(Type) };
				return null;
			}
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
			ulong hashCode = DTokens.Typeid;
			unchecked
			{
				if (Type != null)
					hashCode += 1000000007 * Type.GetHash();
				if (Expression != null)
					hashCode += 1000000009 * Expression.GetHash();
			}
			return hashCode;
		}
	}
}

