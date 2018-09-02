using System;
using System.Collections.Generic;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	public abstract class OperatorBasedExpression : IExpression, ContainerExpression
	{
		public virtual IExpression LeftOperand { get; set; }

		public virtual IExpression RightOperand { get; set; }

		public byte OperatorToken { get; protected set; }

		public override string ToString()
		{
			return LeftOperand.ToString() + DTokens.GetTokenString(OperatorToken) + (RightOperand != null ? RightOperand.ToString() : "");
		}

		public CodeLocation Location
		{
			get { return LeftOperand != null ? LeftOperand.Location : CodeLocation.Empty; }
		}

		public CodeLocation EndLocation
		{
			get { return RightOperand != null ? RightOperand.EndLocation : CodeLocation.Empty; }
		}

		public IEnumerable<IExpression> SubExpressions
		{
			get {
				if (LeftOperand != null) yield return LeftOperand;
				if (RightOperand != null) yield return RightOperand;
			}
		}

		public abstract void Accept(ExpressionVisitor v);

		public abstract R Accept<R>(ExpressionVisitor<R> v);
	}
}

