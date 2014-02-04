using System;

namespace D_Parser.Dom.Expressions
{
	public class StructMemberInitializer : ISyntaxRegion, IVisitable<ExpressionVisitor>
	{
		public string MemberName = string.Empty;
		public IExpression Value;

		public sealed override string ToString()
		{
			return (!string.IsNullOrEmpty(MemberName) ? (MemberName + ":") : "") + Value.ToString();
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
			ulong hashCode = AbstractVariableInitializer.AbstractInitializerHash;
			unchecked
			{
				if (MemberName != null)
					hashCode += 1000000007 * (ulong)MemberName.GetHashCode();
				if (Value != null)
					hashCode += 1000000009 * Value.GetHash();
			}
			return hashCode;
		}
	}
}

