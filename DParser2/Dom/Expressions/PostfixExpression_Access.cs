using System;
using System.Collections.Generic;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// PostfixExpression . Identifier
	/// PostfixExpression . TemplateInstance
	/// PostfixExpression . NewExpression
	/// </summary>
	public class PostfixExpression_Access : PostfixExpression
	{
		/// <summary>
		/// Can be either
		/// 1) An Identifier
		/// 2) A Template Instance
		/// 3) A NewExpression
		/// </summary>
		public IExpression AccessExpression;

		public override string ToString()
		{
			var r = PostfixForeExpression.ToString() + '.';

			if (AccessExpression != null)
				r += AccessExpression.ToString();

			return r;
		}

		public override CodeLocation EndLocation
		{
			get;
			set;
		}

		public override IEnumerable<IExpression> SubExpressions
		{
			get
			{
				yield return PostfixForeExpression;
				yield return AccessExpression;
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

