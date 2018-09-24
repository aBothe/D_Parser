using System;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// BasicType . Identifier
	/// </summary>
	public class TypeDeclarationExpression : PrimaryExpression
	{
		public readonly ITypeDeclaration Declaration;

		public TypeDeclarationExpression(ITypeDeclaration td)
		{
			Declaration = td;
		}

		public override string ToString()
		{
			return Declaration != null ? Declaration.ToString() : "";
		}

		public CodeLocation Location
		{
			get { return Declaration != null ? Declaration.Location : CodeLocation.Empty; }
		}

		public CodeLocation EndLocation
		{
			get { return Declaration != null ? Declaration.EndLocation : CodeLocation.Empty; }
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

