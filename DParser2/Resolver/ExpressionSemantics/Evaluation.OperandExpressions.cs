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

			// TODO: Implement operator precedence (see http://forum.dlang.org/thread/jjohpp$oj6$1@digitalmars.com )

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
		/// a + b; a - b; etc.
		/// </summary>
		ISemantic E_MathOp(OperatorBasedExpression x)
		{
			var lvalue = E(x.LeftOperand);

			if (!eval)
				return lvalue;

			var l = lvalue as ISymbolValue;

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
				if (x.RightOperand is OperatorBasedExpression && !(x.RightOperand is AssignExpression)) //TODO: This must be true only if it's a math expression, so not an assign expression etc.
				{
					var sx = (OperatorBasedExpression)x.RightOperand;

					// Now multiply/divide/mod expression 'l' with sx.LeftOperand
					// afterwards, evaluate the operation between the result just returned and the sx.RightOperand expression.
				}
			}

			var r = E(x.RightOperand) as ISymbolValue;

			if(r == null)
				throw new EvaluationException(x, "Right operand must evaluate to a value", lvalue);

			/*
			 * TODO: Handle invalid values/value ranges.
			 */

			if (x is XorExpression)
			{
				return HandleSingleMathOp(x, l,r, (a,b)=>{
					EnsureIntegralType(a);EnsureIntegralType(b);
					return (long)a.Value ^ (long)b.Value;
				});
			}
			else if (x is OrExpression)
			{
				return HandleSingleMathOp(x, l, r, (a, b) =>
				{
					EnsureIntegralType(a); EnsureIntegralType(b);
					return (long)a.Value | (long)b.Value;
				});
			}
			else if (x is AndExpression)
			{
				return HandleSingleMathOp(x, l, r, (a, b) =>
				{
					EnsureIntegralType(a); EnsureIntegralType(b);
					return (long)a.Value & (long)b.Value;
				});
			}
			else if (x is ShiftExpression) 
				return HandleSingleMathOp(x, l, r, (a, b) =>
				{
					EnsureIntegralType(a); EnsureIntegralType(b);
					if (b.Value < 0 || b.Value > 31)
						throw new EvaluationException(b.BaseExpression, "Shift operand must be between 0 and 31", b);

					switch(x.OperatorToken)
					{
						case DTokens.ShiftLeft:
							return (long)a.Value << (int)b.Value; // TODO: Handle the imaginary part
						case DTokens.ShiftRight:
							return (long)a.Value >> (int)b.Value;
						case DTokens.ShiftRightUnsigned: //TODO: Find out where's the difference between >> and >>>
							return (ulong)a.Value >> (int)(uint)b.Value;
					}

					throw new EvaluationException(x, "Invalid token for shift expression", l,r);
				});
			else if (x is AddExpression) return HandleSingleMathOp(x, l, r, (a, b) =>
			{
				switch (x.OperatorToken)
				{
					case DTokens.Plus:
						return new PrimitiveValue(a.BaseTypeToken, a.Value + b.Value, x, a.ImaginaryPart + b.ImaginaryPart);
					case DTokens.Minus:
						return new PrimitiveValue(a.BaseTypeToken, a.Value - b.Value, x, a.ImaginaryPart - b.ImaginaryPart);
				}

				throw new EvaluationException(x, "Invalid token for add/sub expression", l, r);
			});
			else if (x is CatExpression)
			{
				// Notable: If one element is of the value type of the array, the element is added (either at the front or at the back) to the array

				var av_l = l as ArrayValue;
				var av_r = r as ArrayValue;

				if (av_l!=null && av_r!=null)
				{
					// Ensure that both arrays are of the same type
					if(!ResultComparer.IsEqual(av_l.RepresentedType, av_r.RepresentedType))
						throw new EvaluationException(x, "Both arrays must be of same type", l,r);

					// Might be a string
					if (av_l.IsString && av_r.IsString)
						return new ArrayValue(av_l.RepresentedType as ArrayType, x, av_l.StringValue + av_r.StringValue);
					else
					{
						var elements = new ISymbolValue[av_l.Elements.Length + av_r.Elements.Length];
						Array.Copy(av_l.Elements, 0, elements, 0, av_l.Elements.Length);
						Array.Copy(av_r.Elements, 0, elements, av_l.Elements.Length, av_r.Elements.Length);

						return new ArrayValue(av_l.RepresentedType as ArrayType, elements);
					}
				}

				ArrayType at = null;

				// Append the right value to the array
				if (av_l!=null &&  (at=av_l.RepresentedType as ArrayType) != null &&
					ResultComparer.IsImplicitlyConvertible(r.RepresentedType, at.ValueType, ctxt))
				{
					var elements = new ISymbolValue[av_l.Elements.Length + 1];
					Array.Copy(av_l.Elements, elements, av_l.Elements.Length);
					elements[elements.Length - 1] = r;

					return new ArrayValue(at, elements);
				}
				// Put the left value into the first position
				else if (av_r != null && (at = av_r.RepresentedType as ArrayType) != null &&
					ResultComparer.IsImplicitlyConvertible(l.RepresentedType, at.ValueType, ctxt))
				{
					var elements = new ISymbolValue[1 + av_r.Elements.Length];
					elements[0] = l;
					Array.Copy(av_r.Elements,0,elements,1,av_r.Elements.Length);

					return new ArrayValue(at, elements);
				}

				throw new EvaluationException(x, "At least one operand must be an array. If so, the other operand must be of the array's element type.", l, r);
			}
			else if (x is PowExpression)
			{
				var pv_l=l as PrimitiveValue;
				var pv_r=r as PrimitiveValue;
				if (pv_l != null && pv_r != null)
					return new PrimitiveValue(pv_l.BaseTypeToken, 
						(decimal)Math.Pow((double)pv_l.Value, (double)pv_r.Value), x,
						(decimal)Math.Pow((double)pv_l.ImaginaryPart, (double)pv_r.ImaginaryPart));

				throw new EvaluationException(x, "Both operands are expected to be scalar values in order to perform the power operation correctly.",l,r);
			}
			
			throw new WrongEvaluationArgException();
		}

		void EnsureIntegralType(PrimitiveValue v)
		{
			if (!DTokens.BasicTypes_Integral[v.BaseTypeToken])
				throw new EvaluationException(v.BaseExpression, "Literal must be of integral type",v);
		}

		delegate decimal MathOp(PrimitiveValue x, PrimitiveValue y) ;
		delegate PrimitiveValue MathOp2(PrimitiveValue x, PrimitiveValue y);

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
				return new PrimitiveValue(pl.BaseTypeToken, m(pl, pr), x);

			throw new NotImplementedException("Operator overloading not implemented yet.");
		}

		ISemantic HandleSingleMathOp(IExpression x, ISemantic l, ISemantic r, MathOp2 m)
		{
			var pl = l as PrimitiveValue;
			var pr = r as PrimitiveValue;

			//TODO: imaginary/complex parts

			if (pl != null && pr != null)
				return m(pl,pr);

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
