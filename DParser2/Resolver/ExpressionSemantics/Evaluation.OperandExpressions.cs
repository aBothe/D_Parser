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
			if (x is AssignExpression)
				return E((AssignExpression)x);

			if (x is XorExpression || // a ^ b
				x is OrExpression || // a | b
				x is AndExpression || // a & b
				x is ShiftExpression || // a << 8
				x is AddExpression || // a += b; a -= b;
				x is MulExpression || // a *= b; a /= b; a %= b;
				x is CatExpression || // a ~= b;
				x is PowExpression) // a ^^ b;
				return E_MathOp(x);

			else if (x is OrOrExpression || // a || b
				x is AndAndExpression || // a && b
				x is EqualExpression || // a==b
				x is IdendityExpression || // a is T
				x is RelExpression) // a <= b
				return E_BoolOp(x);

			else if (x is InExpression) // a in b
				return E((InExpression)x);

			throw new WrongEvaluationArgException();
		}

		ISemantic E(AssignExpression x)
		{
			var l = E(x.LeftOperand);

			if (!eval)
				return l;

			return null;
		}

		ISemantic E_BoolOp(OperatorBasedExpression x)
		{
			if (!eval)
				return new PrimitiveType(DTokens.Bool);

			if (x is OrOrExpression)
			{
			}
			else if (x is AndAndExpression)
			{ }
			else if (x is EqualExpression)
			{ }
			else if (x is IdendityExpression)
			{ }
			else if (x is RelExpression)
			{ }

			throw new WrongEvaluationArgException();
		}

		/// <summary>
		/// a + b; a - b;
		/// </summary>
		ISemantic E_MathOp(OperatorBasedExpression x)
		{
			var l = E(x.LeftOperand);

			if (!eval)
				return l;

			//TODO: Operator overloading

			// Note: a * b + c is theoretically treated as a * (b + c), but it's needed to evaluate it as (a * b) + c !
			if (x is MulExpression)
			{
				if (x.RightOperand is OperatorBasedExpression) //TODO: This must be true only if it's a math expression, so not an assign expression etc.
				{
					var sx = (OperatorBasedExpression)x.RightOperand;

					// Now multiply/divide/mod expression 'l' with sx.LeftOperand
					// afterwards, evaluate the operation between the result just returned and the sx.RightOperand expression.
				}
			}

			else if (x is XorExpression)
			{

			}
			else if (x is OrExpression)
			{
			}
			else if (x is AndExpression)
			{
			}
			else if (x is ShiftExpression) { }
			else if (x is AddExpression) { }
			else if (x is CatExpression) { }
			else if (x is PowExpression) { }
			else
				throw new WrongEvaluationArgException();

			return null;
		}

		ISemantic E(ConditionalExpression x)
		{
			return E(x.TrueCaseExpression);
		}

		ISemantic E(InExpression x)
		{
			// The return value of the InExpression is null if the element is not in the array; 
			// if it is in the array it is a pointer to the element.

			return E(x.RightOperand);
		}
	}
}
