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

			var l = E(x.LeftOperand) as ISymbolValue;

			if (x is OrOrExpression)
			{
				// The OrOrExpression evaluates its left operand. 
				// If the left operand, converted to type bool, evaluates to true, 
				// then the right operand is not evaluated. If the result type of the OrOrExpression 
				// is bool then the result of the expression is true. 
				// If the left operand is false, then the right operand is evaluated. 
				// If the result type of the OrOrExpression is bool then the result 
				// of the expression is the right operand converted to type bool.
				return new PrimitiveValue(!(IsFalseZeroOrNull(l) && IsFalseZeroOrNull(E(x.RightOperand) as ISymbolValue)), x);
			}
			else if (x is AndAndExpression)
				return new PrimitiveValue(!IsFalseZeroOrNull(l) && !IsFalseZeroOrNull(E(x.RightOperand) as ISymbolValue), x);
			else if (x is EqualExpression)
				return E((EqualExpression)x,l);
			else if (x is IdendityExpression)
			{
				// http://dlang.org/expression.html#IdentityExpression
			}
			else if (x is RelExpression)
			{ }

			throw new WrongEvaluationArgException();
		}

		ISemantic E(EqualExpression x, ISymbolValue l=null)
		{
			var r = E(x.RightOperand) as ISymbolValue;

			if (x.OperatorToken == DTokens.Equal) // ==
			{
				if (l is PrimitiveValue && r is PrimitiveValue)
				{
					var pv_l = (PrimitiveValue)l;
					var pv_r = (PrimitiveValue)r;

					return new PrimitiveValue(pv_l.Value == pv_r.Value && pv_l.ImaginaryPart == pv_r.ImaginaryPart, x);
				}

				/*
				 * Furthermore TODO: object comparison, pointer content comparison
				 */

				return new PrimitiveValue(false, x);
			}
			else // !=
			{
				if (l is PrimitiveValue && r is PrimitiveValue)
				{
					var pv_l = (PrimitiveValue)l;
					var pv_r = (PrimitiveValue)r;

					return new PrimitiveValue(pv_l.Value != pv_r.Value && pv_l.ImaginaryPart != pv_r.ImaginaryPart, x);
				}

				return new PrimitiveValue(true, x);
			}
		}

		/// <summary>
		/// a + b; a - b;
		/// </summary>
		ISemantic E_MathOp(OperatorBasedExpression x)
		{
			var lvalue = E(x.LeftOperand);

			if (!eval)
				return lvalue;

			var l = lvalue as PrimitiveValue;

			if (l == null)
			{
				/*
				 * In terms of adding opOverloading later on, 
				 * lvalue not being a PrimitiveValue shouldn't be a problem anymore - we simply had to
				 * search the type of l for methods called opAdd etc. and call that method via ctfe.
				 * Finally, return the value the opAdd method passed back - and everything is fine.
				 */

				/*
				 * Also, pointers should be implemented later on.
				 * http://dlang.org/expression.html#AddExpression
				 */

				throw new EvaluationException(x, "Left value must evaluate to a constant scalar value. Operator overloads aren't supported yet", lvalue);
			}

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

			var r = E(x.RightOperand) as ISymbolValue;

			/*
			 * TODO: Handle invalid values/value ranges.
			 */

			if (x is XorExpression)
			{
				return HandleSingleMathOp(x, l,r, (a,b)=>(long)a^(long)b);
			}
			else if (x is OrExpression)
			{
				return HandleSingleMathOp(x, l, r, (a, b) => (long)a | (long)b);
			}
			else if (x is AndExpression)
			{
				return HandleSingleMathOp(x, l, r, (a, b) => (long)a & (long)b);
			}
			else if (x is ShiftExpression) {
				switch (x.OperatorToken)
				{
					case DTokens.ShiftLeft:
						return HandleSingleMathOp(x, l, r, (a, b) => (long)a << (int)b);
						break;
					case DTokens.ShiftRight:
						break;
					case DTokens.ShiftRightUnsigned:
						break;
				}
			}
			else if (x is AddExpression) { }
			else if (x is CatExpression) { }
			else if (x is PowExpression) { }
			else
				throw new WrongEvaluationArgException();

			return null;
		}

		delegate decimal MathOp(decimal x, decimal y) ;

		/// <summary>
		/// Handles mathemathical operation.
		/// If l and r are both primitive values, the MathOp delegate is executed.
		/// 
		/// TODO: Operator overloading.
		/// </summary>
		ISemantic HandleSingleMathOp(IExpression x, ISemantic l, ISemantic r, MathOp m)
		{
			var pl = l as PrimitiveValue;
			var pr = r as PrimitiveValue;

			//TODO: imaginary/complex parts

			if (pl != null && pr != null)
				return new PrimitiveValue(pl.BaseTypeToken, m(pl.Value, pr.Value), x);

			throw new NotImplementedException("Operator overloading not implemented yet.");
		}

		ISemantic E(ConditionalExpression x)
		{
			if (eval)
			{
				var b = E(x.OrOrExpression) as ISymbolValue;

				if (IsFalseZeroOrNull(b))
					return E(x.FalseCaseExpression);
				else
					return E(x.TrueCaseExpression);
			}

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
