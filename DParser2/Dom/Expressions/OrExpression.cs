﻿using System;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// a | b;
	/// </summary>
	public class OrExpression : OperatorBasedExpression
	{
		public OrExpression()
		{
			OperatorToken = DTokens.BitwiseOr;
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

