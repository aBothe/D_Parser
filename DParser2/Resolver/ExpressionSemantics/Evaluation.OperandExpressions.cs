using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		ISemantic E(OperatorBasedExpression x, ISemantic lValue=null)
		{
			if (x is AssignExpression)
				return E((AssignExpression)x,lValue);

			// TODO: Implement operator precedence (see http://forum.dlang.org/thread/jjohpp$oj6$1@digitalmars.com )

			if (x is XorExpression || // a ^ b
				x is OrExpression || // a | b
				x is AndExpression || // a & b
				x is ShiftExpression || // a << 8
				x is AddExpression || // a += b; a -= b;
				x is MulExpression || // a *= b; a /= b; a %= b;
				x is CatExpression || // a ~= b;
				x is PowExpression) // a ^^ b;
				return E_MathOp(x, lValue);

			else if (x is EqualExpression) // a==b
				return E((EqualExpression)x, lValue);

			else if (x is OrOrExpression || // a || b
				x is AndAndExpression || // a && b
				x is IdendityExpression || // a is T
				x is RelExpression) // a <= b
				return E_BoolOp(x, lValue as ISymbolValue);

			else if (x is InExpression) // a in b
				return E((InExpression)x, lValue);

			throw new WrongEvaluationArgException();
		}

		ISemantic E(AssignExpression x, ISemantic lValue=null)
		{
			if (!eval)
				return E(x.LeftOperand);

			var l = TryGetValue(lValue ?? E(x.LeftOperand));

			//TODO

			return null;
		}

		ISemantic E_BoolOp(OperatorBasedExpression x, ISemantic lValue=null, ISemantic rValue = null)
		{
			if (!eval)
				return new PrimitiveType(DTokens.Bool);

			var l = TryGetValue(lValue ?? E(x.LeftOperand));
			var r = TryGetValue(rValue ?? E(x.RightOperand));

			if (x is OrOrExpression)
			{
				// The OrOrExpression evaluates its left operand. 
				// If the left operand, converted to type bool, evaluates to true, 
				// then the right operand is not evaluated. If the result type of the OrOrExpression 
				// is bool then the result of the expression is true. 
				// If the left operand is false, then the right operand is evaluated. 
				// If the result type of the OrOrExpression is bool then the result 
				// of the expression is the right operand converted to type bool.
				return new PrimitiveValue(!(IsFalseZeroOrNull(l) && IsFalseZeroOrNull(r)), x);
			}
			else if (x is AndAndExpression)
				return new PrimitiveValue(!IsFalseZeroOrNull(l) && !IsFalseZeroOrNull(r), x);
			else if (x is IdendityExpression)
			{
				// http://dlang.org/expression.html#IdentityExpression
			}
			else if (x is RelExpression)
			{
				return HandleSingleMathOp(x, l, r, (a,b, op) => {

					// Unordered-ness is when at least one operator is Not any Number (NaN)
					bool unordered = a.IsNaN || b.IsNaN;

					bool relationIsTrue=false;
					bool cmpIm = a.ImaginaryPart != 0 || b.ImaginaryPart != 0;

					switch(x.OperatorToken)
					{
						case DTokens.GreaterThan: // greater, >
							relationIsTrue = a.Value > b.Value && (cmpIm ? a.ImaginaryPart > b.ImaginaryPart : true);
							break;
						case DTokens.GreaterEqual: // greater or equal, >=
							relationIsTrue = a.Value >= b.Value && a.ImaginaryPart >= b.ImaginaryPart;
							break;
						case DTokens.LessThan: // less, <
							relationIsTrue = a.Value < b.Value && (cmpIm ? a.ImaginaryPart < b.ImaginaryPart : true);
							break;
						case DTokens.LessEqual: // less or equal, <=
							relationIsTrue = a.Value <= b.Value && a.ImaginaryPart <= b.ImaginaryPart;
							break;
						case DTokens.Unordered: // unordered, !<>=
							relationIsTrue = unordered;
							break;
						case DTokens.LessOrGreater: // less or greater, <>
							relationIsTrue = (a.Value < b.Value || a.Value > b.Value) && (cmpIm ? 
								(a.ImaginaryPart < b.ImaginaryPart || a.ImaginaryPart > b.ImaginaryPart) : true);
							break;
						case DTokens.LessEqualOrGreater: // less, equal, or greater, <>=
							relationIsTrue = (a.Value < b.Value || a.Value >= b.Value) && (cmpIm ?
								(a.ImaginaryPart < b.ImaginaryPart || a.ImaginaryPart >= b.ImaginaryPart) : true);
							break;
						case DTokens.UnorderedOrGreater: // unordered or greater, !<=
							relationIsTrue = unordered || (a.Value > b.Value && (cmpIm ? a.ImaginaryPart > b.ImaginaryPart : true));
							break;
						case DTokens.UnorderedGreaterOrEqual: // unordered, greater, or equal, !<
							relationIsTrue = unordered || (a.Value >= b.Value && a.ImaginaryPart >= b.ImaginaryPart);
							break;
						case DTokens.UnorderedOrLess: // unordered or less, !>=
							relationIsTrue = unordered || (a.Value < b.Value && (cmpIm ? a.ImaginaryPart < b.ImaginaryPart : true));
							break;
						case DTokens.UnorderedLessOrEqual: // unordered, less, or equal, !>
							relationIsTrue = unordered || (a.Value <= b.Value && a.ImaginaryPart <= b.ImaginaryPart);
							break;
						case DTokens.UnorderedOrEqual: // unordered or equal, !<>
							relationIsTrue = unordered || (a.Value == b.Value && a.ImaginaryPart == b.ImaginaryPart);
							break;
					}

					return new PrimitiveValue(relationIsTrue, op);
				}, false);
			}

			throw new WrongEvaluationArgException();
		}

		ISemantic E(EqualExpression x, ISemantic lValue =null, ISemantic rValue = null)
		{
			var l = TryGetValue(lValue ?? E(x.LeftOperand));
			var r = TryGetValue(rValue ?? E(x.RightOperand));

			var isEq = SymbolValueComparer.IsEqual(l, r);

			return new PrimitiveValue(x.OperatorToken == DTokens.Equal ? isEq : !isEq, x);
		}

		/// <summary>
		/// a + b; a - b; etc.
		/// </summary>
		ISemantic E_MathOp(OperatorBasedExpression x, ISemantic lValue=null, ISemantic rValue=null)
		{
			if (!eval)
				return lValue ?? E(x.LeftOperand);

			var l = TryGetValue(lValue ?? E(x.LeftOperand));

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

				EvalError(x, "Left value must evaluate to a constant scalar value. Operator overloads aren't supported yet", new[]{lValue});
				return null;
			}

			//TODO: Operator overloading

			// Note: a * b + c is theoretically treated as a * (b + c), but it's needed to evaluate it as (a * b) + c !
			if (x is MulExpression || x is PowExpression)
			{
				try{
				if (x.RightOperand is OperatorBasedExpression && !(x.RightOperand is AssignExpression)) //TODO: This must be true only if it's a math expression, so not an assign expression etc.
				{
					var sx = (OperatorBasedExpression)x.RightOperand;

					// Now multiply/divide/mod expression 'l' with sx.LeftOperand
					try{
					var intermediateResult = HandleSingleMathOp(x, l, E(sx.LeftOperand), mult);

					// afterwards, evaluate the operation between the result just returned and the sx.RightOperand.
					return E(sx, intermediateResult);
					}catch(DivideByZeroException)
					{
						EvalError(sx, "Divide by 0");
						return null;
					}
				}

				return HandleSingleMathOp(x, l, TryGetValue(rValue ?? E(x.RightOperand)), mult);
				}catch(DivideByZeroException)
				{
					EvalError(x, "Divide by 0");
					return null;
				}
			}
			else if (x is CatExpression)
			{
				return EvalConcatenation(x as CatExpression, l);
			}

			var r = TryGetValue(rValue ?? E(x.RightOperand));

			if(r == null){
				EvalError(x, "Right operand must evaluate to a value", new[]{lValue});
				return null;
			}

			/*
			 * TODO: Handle invalid values/value ranges.
			 */

			if (x is XorExpression)
			{
				return HandleSingleMathOp(x, l,r, (a,b)=>{
				                          	if(EnsureIntegralType(x.LeftOperand,a) && EnsureIntegralType(x.RightOperand,b))
												return (long)a.Value ^ (long)b.Value;
				                          	return 0L;
				});
			}
			else if (x is OrExpression)
			{
				return HandleSingleMathOp(x, l, r, (a, b) =>
				{
					if(EnsureIntegralType(x.LeftOperand,a) && EnsureIntegralType(x.RightOperand,b))
						return (long)a.Value | (long)b.Value;
                  	return 0L;
				});
			}
			else if (x is AndExpression)
			{
				return HandleSingleMathOp(x, l, r, (a, b) =>
				{
					if(EnsureIntegralType(x.LeftOperand,a) && EnsureIntegralType(x.RightOperand,b))
												return (long)a.Value & (long)b.Value;
				                          	return 0L;
				});
			}
			else if (x is ShiftExpression) 
				return HandleSingleMathOp(x, l, r, (a, b) =>
				{
				                          	if(!EnsureIntegralType(x.LeftOperand, a) || !EnsureIntegralType(x.RightOperand, b))
				                          		return 0L;
					if (b.Value < 0 || b.Value > 31){
						EvalError(x, "Shift operand must be between 0 and 31", new[]{b});
						return 0m;
					}

					switch(x.OperatorToken)
					{
						case DTokens.ShiftLeft:
							return (long)a.Value << (int)b.Value; // TODO: Handle the imaginary part
						case DTokens.ShiftRight:
							return (long)a.Value >> (int)b.Value;
						case DTokens.ShiftRightUnsigned: //TODO: Find out where's the difference between >> and >>>
							return (ulong)a.Value >> (int)(uint)b.Value;
					}

					EvalError(x, "Invalid token for shift expression", new[]{l,r});
					return 0m;
				});
			else if (x is AddExpression)
				return HandleSingleMathOp(x, l, r, (a, b, op) =>
				{
					switch (op.OperatorToken)
					{
						case DTokens.Plus:
							return new PrimitiveValue(a.BaseTypeToken, a.Value + b.Value, x, a.ImaginaryPart + b.ImaginaryPart);
						case DTokens.Minus:
							return new PrimitiveValue(a.BaseTypeToken, a.Value - b.Value, x, a.ImaginaryPart - b.ImaginaryPart);
					}

					EvalError(x, "Invalid token for add/sub expression", new[]{l,r});
					return null;
				});
			
			throw new WrongEvaluationArgException();
		}
		
		ISymbolValue EvalConcatenation(CatExpression x, ISymbolValue lValue)
		{
			// In the (not unusual) case that more than 2 arrays/strings shall be concat'ed - process them more efficiently
			var catQueue = new Queue<ISymbolValue>();
			
			var catEx = (x as CatExpression);
			
			catQueue.Enqueue(lValue);
			
			catEx = catEx.RightOperand as CatExpression;
			ISymbolValue r;
			while(catEx != null)
			{
				r = TryGetValue(E(catEx.LeftOperand));
				if(r == null)
				{
					EvalError(catEx.LeftOperand, "Couldn't be evaluated.");
					return null;
				}
				catQueue.Enqueue(r);
				if(catEx.RightOperand is CatExpression)
					catEx = catEx.RightOperand as CatExpression;
				else
					break;
			}
			
			r = TryGetValue(E((catEx ?? x).RightOperand));
			if(r == null)
			{
				EvalError(catEx.LeftOperand, "Couldn't be evaluated.");
				return null;
			}
			catQueue.Enqueue(r);
			
			// Notable: If one element is of the value type of the array, the element is added (either at the front or at the back) to the array
			// myString ~ 'a' will append an 'a' to the string
			// 'a' ~ myString inserts 'a' at index 0
			
			// Determine whether we have to build up a string OR a normal list of atomic elements
			bool isString = true;
			ArrayType lastArrayType = null;
			
			foreach(var e in catQueue)
			{
				if(e is AssociativeArrayValue)
				{
					EvalError(x, "Can't concatenate associative arrays", e);
					return null;
				}
				else if(e is ArrayValue)
				{
					if(lastArrayType != null && !ResultComparer.IsEqual(lastArrayType, e.RepresentedType))
					{
						EvalError(x, "Both arrays must be of same type", new[]{lastArrayType, e.RepresentedType});
						return null;
					}
					lastArrayType = e.RepresentedType as ArrayType;
					
					if((e as ArrayValue).IsString)
						continue;
				}
				else if(e is PrimitiveValue)
				{
					var btt = (e as PrimitiveValue).BaseTypeToken;
					if(btt == DTokens.Char || btt == DTokens.Dchar || btt == DTokens.Wchar)
						continue;
				}
				
				isString = false;
			}
			
			if(lastArrayType == null)
			{
				EvalError(x, "At least one operand must be an (non-associative) array. If so, the other operand must be of the array's element type.", catQueue.ToArray());
				return null;
			}
			
			if(isString)
			{
				var sb = new StringBuilder();
				
				while(catQueue.Count != 0)
				{
					var e = catQueue.Dequeue();
					if(e is ArrayValue)
						sb.Append((e as ArrayValue).StringValue);
					else if(e is PrimitiveValue)
						sb.Append((char)((e as PrimitiveValue).Value));
				}
				return new ArrayValue(GetStringType(LiteralSubformat.Utf8), sb.ToString());
			}
			
			
			var elements = new List<ISymbolValue>();
			while(catQueue.Count != 0)
			{
				var e = catQueue.Dequeue();
				
				var av = e as ArrayValue;
				if(av != null)
				{
					if(av.IsString)
						elements.Add(av);
					else if(av.Elements != null)
						elements.AddRange(av.Elements);
					continue;
				}
				
				if(!ResultComparer.IsImplicitlyConvertible(e.RepresentedType, lastArrayType.ValueType, ctxt))
				{
					EvalError(x, "Element with type " + e.RepresentedType.ToCode() + " doesn't fit into array with type "+lastArrayType.ToCode(), catQueue.ToArray());
					return null;
				}
				
				elements.Add(e);
			}
			
			return new ArrayValue(lastArrayType, elements.ToArray());
		}

		ISymbolValue TryGetValue(ISemantic s)
		{
			if (s is VariableValue)
				return EvaluateValue(s as VariableValue, ValueProvider);

			return s as ISymbolValue;
		}

		static PrimitiveValue mult(PrimitiveValue a, PrimitiveValue b, OperatorBasedExpression x)
		{
			decimal v = 0;
			decimal im=0;
			switch (x.OperatorToken)
			{
				case DTokens.Pow:
					v = (decimal)Math.Pow((double)a.Value, (double)b.Value);
					v = (decimal)Math.Pow((double)a.ImaginaryPart, (double)b.ImaginaryPart);
					break;
				case DTokens.Times:
					v= a.Value * b.Value;
					im=a.ImaginaryPart * b.ImaginaryPart;
					break;
				case DTokens.Div:
					if ((a.Value!=0 && b.Value == 0) || (a.ImaginaryPart!=0 && b.ImaginaryPart==0))
						throw new DivideByZeroException();
					if(b.Value!=0)
						v= a.Value / b.Value;
					if(b.ImaginaryPart!=0)
						im=a.ImaginaryPart / b.ImaginaryPart;
					break;
				case DTokens.Mod:
					if ((a.Value!=0 && b.Value == 0) || (a.ImaginaryPart!=0 && b.ImaginaryPart==0))
						throw new DivideByZeroException();
					if(b.Value!=0)
						v= a.Value % b.Value;
					if(b.ImaginaryPart!=0)
						im=a.ImaginaryPart % b.ImaginaryPart;
					break;
				default:
					return null;
					//EvalError(x, "Invalid token for multiplication expression (*,/,% only)");
			}

			return new PrimitiveValue(a.BaseTypeToken, v, x, im);
		}

		bool EnsureIntegralType(IExpression x,PrimitiveValue v)
		{
			if (!DTokens.BasicTypes_Integral[v.BaseTypeToken]){
				EvalError(x,"Literal must be of integral type",new[]{(ISemantic)v});
				return false;
			}
			return true;
		}

		delegate decimal MathOp(PrimitiveValue x, PrimitiveValue y) ;
		delegate PrimitiveValue MathOp2(PrimitiveValue x, PrimitiveValue y, OperatorBasedExpression op);

		/// <summary>
		/// Handles mathemathical operation.
		/// If l and r are both primitive values, the MathOp delegate is executed.
		/// 
		/// TODO: Operator overloading.
		/// </summary>
		ISemantic HandleSingleMathOp(IExpression x, ISemantic l, ISemantic r, MathOp m)
		{
			if(l == null || r == null)
				return null;
			
			var pl = l as PrimitiveValue;
			var pr = r as PrimitiveValue;

			//TODO: imaginary/complex parts

			if (pl != null && pr != null)
			{
				// If one 
				if (pl.IsNaN || pr.IsNaN)
					return PrimitiveValue.CreateNaNValue(x, pl.IsNaN ? pl.BaseTypeToken : pr.BaseTypeToken);

				return new PrimitiveValue(pl.BaseTypeToken, m(pl, pr), x);
			}

			throw new NotImplementedException("Operator overloading not implemented yet.");
		}

		ISemantic HandleSingleMathOp(OperatorBasedExpression x, ISemantic l, ISemantic r, MathOp2 m, bool UnorderedCheck = true)
		{
			if(l == null || r == null)
				return null;
			
			var pl = l as PrimitiveValue;
			var pr = r as PrimitiveValue;

			//TODO: imaginary/complex parts

			if (pl != null && pr != null)
			{
				if (UnorderedCheck && (pl.IsNaN || pr.IsNaN))
					return PrimitiveValue.CreateNaNValue(x, pl.IsNaN ? pl.BaseTypeToken : pr.BaseTypeToken);

				return m(pl, pr, x);
			}

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

		ISemantic E(InExpression x, ISemantic l=null)
		{
			// The return value of the InExpression is null if the element is not in the array; 
			// if it is in the array it is a pointer to the element.

			return E(x.RightOperand);
		}
	}
}
