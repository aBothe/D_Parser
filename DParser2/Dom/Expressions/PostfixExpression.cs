using System;
using System.Collections.Generic;

namespace D_Parser.Dom.Expressions
{
	public abstract class PostfixExpression : IExpression, ContainerExpression
	{
		public IExpression PostfixForeExpression { get; set; }

		public CodeLocation Location
		{
			get { return PostfixForeExpression != null ? PostfixForeExpression.Location : CodeLocation.Empty; }
		}

		public abstract CodeLocation EndLocation { get; set; }

		public virtual IEnumerable<IExpression> SubExpressions
		{
			get { yield return PostfixForeExpression; }
		}

		public abstract void Accept(ExpressionVisitor vis);

		public abstract R Accept<R>(ExpressionVisitor<R> vis);
	}
}

