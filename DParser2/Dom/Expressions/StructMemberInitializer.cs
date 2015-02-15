using System;

namespace D_Parser.Dom.Expressions
{
	public class StructMemberInitializer : ISyntaxRegion, IVisitable<ExpressionVisitor>
	{
		public string MemberName { 
			get { return Strings.TryGet(MemberNameHash); } 
			set {
				Strings.Add(value);
				MemberNameHash = value.GetHashCode();
			}
		}
		public int MemberNameHash = 0;
		public IExpression Value;

		public sealed override string ToString()
		{
			return (MemberNameHash != 0 ? (MemberName + ": ") : "") + (Value != null ? Value.ToString() : "");
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
	}
}

