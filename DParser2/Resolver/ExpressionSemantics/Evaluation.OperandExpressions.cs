using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		ISemantic E(OperatorBasedExpression x)
		{
			if (x is AssignExpression || // a = b
				x is XorExpression || // a ^ b
				x is OrExpression || // a | b
				x is AndExpression || // a & b
				x is ShiftExpression || // a << 8
				x is AddExpression || // a += b; a -= b;
				x is MulExpression || // a *= b; a /= b; a %= b;
				x is CatExpression || // a ~= b;
				x is PowExpression) // a ^^ b;
				return E((x as OperatorBasedExpression).LeftOperand);

			else if (x is OrOrExpression || // a || b
				x is AndAndExpression || // a && b
				x is EqualExpression || // a==b
				x is IdendityExpression || // a is T
				x is RelExpression) // a <= b
				return new PrimitiveType(DTokens.Bool);

			else if (x is InExpression) // a in b
			{
				// The return value of the InExpression is null if the element is not in the array; 
				// if it is in the array it is a pointer to the element.

				return E(((InExpression)x).RightOperand);
			}

			return null;
		}

		ISemantic E(ConditionalExpression x)
		{
			return E(((ConditionalExpression)x).TrueCaseExpression);
		}
	}
}
