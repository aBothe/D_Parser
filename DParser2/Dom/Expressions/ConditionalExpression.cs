using System;
using System.Collections.Generic;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// a ? b : b;
	/// </summary>
	public class ConditionalExpression : IExpression, ContainerExpression
	{
		public IExpression OrOrExpression { get; set; }

		public IExpression TrueCaseExpression { get; set; }

		public IExpression FalseCaseExpression { get; set; }

		public override string ToString()
		{
			return (OrOrExpression != null ? OrOrExpression.ToString() : "") + "?" + 
				(TrueCaseExpression != null ? TrueCaseExpression.ToString() : "") + ':' + 
				(FalseCaseExpression != null ? FalseCaseExpression.ToString() : "");
		}

		public CodeLocation Location
		{
			get { return OrOrExpression.Location; }
		}

		public CodeLocation EndLocation
		{
			get { return (FalseCaseExpression ?? TrueCaseExpression ?? OrOrExpression).EndLocation; }
		}

		public IEnumerable<IExpression> SubExpressions
		{
			get {
				if (OrOrExpression != null)
					yield return OrOrExpression;
				if (TrueCaseExpression != null)
					yield return TrueCaseExpression;
				if (FalseCaseExpression != null)
					yield return FalseCaseExpression;
			}
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

