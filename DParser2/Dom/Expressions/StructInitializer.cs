using System;
using D_Parser.Parser;
using System.Collections.Generic;

namespace D_Parser.Dom.Expressions
{
	public class StructInitializer : AbstractVariableInitializer, ContainerExpression
	{
		public StructMemberInitializer[] MemberInitializers;

		public sealed override string ToString()
		{
			var ret = "{";

			if (MemberInitializers != null)
				foreach (var i in MemberInitializers)
					ret += i.ToString() + ",";

			return ret.TrimEnd(',') + "}";
		}

		public IEnumerable<IExpression> SubExpressions
		{
			get
			{
				if (MemberInitializers != null)
					foreach (var mi in MemberInitializers)
						if (mi.Value != null)
							yield return mi.Value;
			}
		}

		public override void Accept(ExpressionVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(ExpressionVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
}

