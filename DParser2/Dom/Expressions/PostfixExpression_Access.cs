using System;

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

		public override IExpression[] SubExpressions
		{
			get
			{
				return new[]{ PostfixForeExpression, AccessExpression };
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

		public override ulong GetHash()
		{
			ulong hashCode = base.GetHash();
			unchecked
			{
				if (AccessExpression != null)
					hashCode += 1000000009 * AccessExpression.GetHash();
			}
			return hashCode;
		}
	}
}

