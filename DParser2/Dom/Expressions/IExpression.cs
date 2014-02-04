using System;

namespace D_Parser.Dom.Expressions
{
	public interface IExpression : ISyntaxRegion, IVisitable<ExpressionVisitor>
	{
		R Accept<R>(ExpressionVisitor<R> vis);

		ulong GetHash();
	}
}

