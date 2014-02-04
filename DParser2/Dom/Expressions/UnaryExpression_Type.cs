using System;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// (Type).Identifier
	/// </summary>
	public class UnaryExpression_Type : UnaryExpression
	{
		public ITypeDeclaration Type { get; set; }

		public int AccessIdentifierHash;

		public string AccessIdentifier
		{
			get { return Strings.TryGet(AccessIdentifierHash); }
			set
			{
				AccessIdentifierHash = value != null ? value.GetHashCode() : 0;
				Strings.Add(value);
			}
		}

		public override string ToString()
		{
			return "(" + Type.ToString() + ")." + AccessIdentifier;
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
			ulong hashCode = 0uL;
			unchecked
			{
				if (Type != null)
					hashCode += 1000000007 * (ulong)Type.GetHashCode();
				if (AccessIdentifierHash != 0)
					hashCode += 1000000009 * (ulong)AccessIdentifierHash;
			}
			return hashCode;
		}
	}
}

