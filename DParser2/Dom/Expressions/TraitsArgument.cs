using System;

namespace D_Parser.Dom.Expressions
{
	public class TraitsArgument : ISyntaxRegion
	{
		public readonly ITypeDeclaration Type;
		public readonly IExpression AssignExpression;

		public TraitsArgument(ITypeDeclaration t)
		{
			this.Type = t;
		}

		public TraitsArgument(IExpression x)
		{
			this.AssignExpression = x;
		}

		public override string ToString()
		{
			return Type != null ? Type.ToString(true) : AssignExpression.ToString();
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
	}
}

