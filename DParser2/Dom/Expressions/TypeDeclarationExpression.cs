using System;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// BasicType . Identifier
	/// </summary>
	public class TypeDeclarationExpression : PrimaryExpression
	{
		public readonly ITypeDeclaration Declaration;

		public static IExpression TryWrap(ISyntaxRegion expressionOrDeclaration)
		{
			return expressionOrDeclaration != null ? expressionOrDeclaration as IExpression ??
				new TypeDeclarationExpression(expressionOrDeclaration as ITypeDeclaration) : null;
		}

		TypeDeclarationExpression(ITypeDeclaration td)
		{
			Declaration = td;
		}

		public override string ToString()
		{
			return Declaration.ToString();
		}

		public CodeLocation Location
		{
			get { return Declaration.Location; }
		}

		public CodeLocation EndLocation
		{
			get { return Declaration.EndLocation; }
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

