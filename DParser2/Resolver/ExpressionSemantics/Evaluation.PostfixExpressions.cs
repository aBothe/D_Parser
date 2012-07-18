using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;
using System;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		ISemantic E(PostfixExpression ex)
		{
			if (ex is PostfixExpression_MethodCall)
				return E((PostfixExpression_MethodCall)ex);

			var foreExpr=E(ex.PostfixForeExpression);

			if(foreExpr is AliasedType)
				foreExpr = DResolver.StripAliasSymbol((AbstractType)foreExpr);

			if (foreExpr == null)
			{
				if (eval)
					throw new EvaluationException(ex.PostfixForeExpression, "Evaluation returned empty result");
				else
				{
					ctxt.LogError(new NothingFoundError(ex.PostfixForeExpression);
					return null;
				}
			}

			if (ex is PostfixExpression_Access)
			{
				var r = E((PostfixExpression_Access)ex, foreExpr, true);
				ctxt.CheckForSingleResult(r, ex);
				return r != null && r.Length != 0 ? r[0] : null;
			}
			else if (ex is PostfixExpression_Increment)
				return E((PostfixExpression_Increment)ex, foreExpr);
			else if (ex is PostfixExpression_Decrement)
				return E((PostfixExpression_Decrement)foreExpr);

			// myArray[0]; myArray[0..5];
			if(foreExpr is MemberSymbol)
				foreExpr = DResolver.StripMemberSymbols((AbstractType)foreExpr);

			if (ex is PostfixExpression_Slice) 
				return E((PostfixExpression_Slice)ex, foreExpr);
			else if(ex is PostfixExpression_Index)
				return E((PostfixExpression_Index)ex, foreExpr);

			return null;
		}

		ISemantic E(PostfixExpression_MethodCall call, bool returnBaseTypeOnly=true)
		{
			// Deduce template parameters later on
			AbstractType[] baseExpression = null;
			TemplateInstanceExpression tix = null;

			// Explicitly don't resolve the methods' return types - it'll be done after filtering to e.g. resolve template types to the deduced one
			var optBackup = ctxt.CurrentContext.ContextDependentOptions;
			ctxt.CurrentContext.ContextDependentOptions = ResolutionOptions.DontResolveBaseTypes;

			if (call.PostfixForeExpression is PostfixExpression_Access)
			{
				var pac = (PostfixExpression_Access)call.PostfixForeExpression;
				if (pac.AccessExpression is TemplateInstanceExpression)
					tix = (TemplateInstanceExpression)pac.AccessExpression;

				baseExpression = TypeDeclarationResolver.Convert(E(pac, null, false));
			}
			else if (call.PostfixForeExpression is TemplateInstanceExpression)
			{
				tix = (TemplateInstanceExpression)call.PostfixForeExpression;
				baseExpression = GetOverloads(tix, null, false);
			}
			else
				baseExpression = new[] { E(call.PostfixForeExpression) as AbstractType };

			ctxt.CurrentContext.ContextDependentOptions = optBackup;

			var methodOverloads = new List<AbstractType>();

			#region Search possible methods, opCalls or delegates that could be called
			bool requireStaticItems = true; //TODO: What if there's an opCall and a foreign method at the same time? - and then this variable would be bullshit
			IEnumerable<AbstractType> scanResults = DResolver.StripAliasSymbols(baseExpression);
			var nextResults = new List<AbstractType>();

			while (scanResults != null)
			{
				foreach (var b in scanResults)
				{
					if (b is MemberSymbol)
					{
						var mr = (MemberSymbol)b;

						if (mr.Definition is DMethod)
						{
							methodOverloads.Add(mr);
						}

						/*
						 * If mr.Node is not a method, so e.g. if it's a variable
						 * pointing to a delegate
						 * 
						 * class Foo
						 * {
						 *	string opCall() {  return "asdf";  }
						 * }
						 * 
						 * Foo f=new Foo();
						 * f(); -- calls opCall, opCall is not static
						 */
						else if (mr != null)
						{
							nextResults.Add(mr.Base);

							requireStaticItems = false;
						}
					}
					else if (b is DelegateType)
					{
						var dg = (DelegateType)b;

						/*
						 * int a = delegate(x) { return x*2; } (12); // a is 24 after execution
						 * auto dg=delegate(x) {return x*3;};
						 * int b = dg(4);
						 */

						if (dg.IsFunctionLiteral)
							methodOverloads.Add(dg);
					}
					else if (b is ClassType)
					{
						/*
						 * auto a = MyStruct(); -- opCall-Overloads can be used
						 */
						var classDef = ((ClassType)b).Definition;

						if (classDef == null)
							continue;

						foreach (var i in classDef)
							if (i.Name == "opCall" && i is DMethod && (!requireStaticItems || (i as DNode).IsStatic))
								methodOverloads.Add(TypeDeclarationResolver.HandleNodeMatch(i, ctxt, b, call) as MemberSymbol);
					}
					/*
					 * Every struct can contain a default ctor:
					 * 
					 * struct S { int a; bool b; }
					 * 
					 * auto s = S(1,true); -- ok
					 * auto s2= new S(2,false); -- error, no constructor found!
					 */
					else if (b is StructType && methodOverloads.Count == 0)
					{
						//TODO: Deduce parameters
						return b;
					}
				}

				scanResults = nextResults.Count == 0 ? null : nextResults.ToArray();
				nextResults.Clear();
			}
			#endregion

			if (methodOverloads.Count == 0)
				return null;

			// Get all arguments' types
			var callArgumentTypes = new List<AbstractType>();
			if (call.Arguments != null)
				foreach (var arg in call.Arguments)
					callArgumentTypes.Add(E(arg) as AbstractType);

			#region Deduce template parameters and filter out unmatching overloads
			// UFCS argument assignment will be done per-overload and in the EvalAndFilterOverloads method!

			// First add optionally given template params
			// http://dlang.org/template.html#function-templates
			var resolvedCallArguments = tix == null ?
				new List<ISemantic>() :
				TemplateInstanceHandler.PreResolveTemplateArgs(tix, ctxt);

			// Then add the arguments' types
			resolvedCallArguments.AddRange(callArgumentTypes);

			var templateParamFilteredOverloads= TemplateInstanceHandler.EvalAndFilterOverloads(
				methodOverloads,
				resolvedCallArguments.Count > 0 ? resolvedCallArguments.ToArray() : null,
				true, ctxt);
			#endregion

			#region Filter by parameter-argument comparison
			var argTypeFilteredOverloads = new List<AbstractType>();

			foreach (var ov in templateParamFilteredOverloads)
			{
				if (ov is MemberSymbol)
				{
					var ms = (MemberSymbol)ov;
					var dm = ms.Definition as DMethod;
					bool add = false;

					if (dm != null)
					{
						ctxt.CurrentContext.IntroduceTemplateParameterTypes(ms);

						add = false;

						if (callArgumentTypes.Count == 0 && dm.Parameters.Count == 0)
							add=true;
						else
							for (int i=0; i< dm.Parameters.Count; i++)
							{
								var paramType = TypeDeclarationResolver.ResolveSingle(dm.Parameters[i].Type, ctxt);
								
								// TODO: Expression tuples & variable argument lengths
								if (i >= callArgumentTypes.Count ||
									!ResultComparer.IsImplicitlyConvertible(callArgumentTypes[i], paramType, ctxt))
									continue;

								add = true;
							}

						if (add)
						{
							var bt=TypeDeclarationResolver.GetMethodReturnType(dm, ctxt);

							if (returnBaseTypeOnly)
								argTypeFilteredOverloads.Add(bt);
							else
								argTypeFilteredOverloads.Add(new MemberSymbol(dm, bt, ms.DeclarationOrExpressionBase, ms.DeducedTypes));
						}

						ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(ms);
					}
				}
				else if(ov is DelegateType)
				{
					var dg = (DelegateType)ov;
					var bt = TypeDeclarationResolver.GetMethodReturnType(dg, ctxt);

					//TODO: Param-Arg check
					if (returnBaseTypeOnly)
						argTypeFilteredOverloads.Add(bt);
					else
						argTypeFilteredOverloads.Add(new DelegateType(bt, dg.DeclarationOrExpressionBase as FunctionLiteral, dg.Parameters));
				}
			}

			ctxt.CheckForSingleResult(argTypeFilteredOverloads.ToArray(), call);

			return argTypeFilteredOverloads!=null && argTypeFilteredOverloads.Count !=0 ? argTypeFilteredOverloads[0] : null;
			#endregion
		}

		ISemantic[] E(PostfixExpression_Access acc,
			ISemantic resultBase = null, bool DeduceImplicitly=true)
		{
			if (acc == null)
				return null;

			var baseExpression = resultBase ?? E(acc.PostfixForeExpression);

			if (acc.AccessExpression is NewExpression)
			{
				/*
				 * This can be both a normal new-Expression as well as an anonymous class declaration!
				 */
				//TODO!
				return null;
			}


			AbstractType[] overloads = null;

			if (acc.AccessExpression is TemplateInstanceExpression)
			{
				var tix = (TemplateInstanceExpression)acc.AccessExpression;
				// Do not deduce and filter if superior expression is a method call since call arguments' types also count as template arguments!
				overloads = GetOverloads(tix, resultBase==null ? null : new[] { resultBase as AbstractType }, DeduceImplicitly);
			}
			
			else if (acc.AccessExpression is IdentifierExpression)
			{
				var id = ((IdentifierExpression)acc.AccessExpression).Value as string;
				
				/*
				 * 1) First off, try to resolve the identifier as it was a type declaration's identifer list part.
				 * 2) Static properties
				 * 3) UFCS
				 */

				// 1)
				overloads = TypeDeclarationResolver.ResolveFurtherTypeIdentifier(id, new[]{baseExpression as AbstractType}, ctxt, acc);

				if (overloads == null)
				{
					// 2)
					var staticTypeProperty = StaticPropertyResolver.TryResolveStaticProperties(baseExpression as AbstractType, id, ctxt);

					if (staticTypeProperty != null)
						return new[] { staticTypeProperty };
				}

				// 3)
				var ufcsResult = UFCSResolver.TryResolveUFCS(baseExpression, acc, ctxt);

				if (ufcsResult != null)
					return ufcsResult;
			}
			else // Error?
				return new[]{ baseExpression };

			if (overloads == null)
			{
				overloads = UFCSResolver.TryResolveUFCS(baseExpression, acc, ctxt);

				if (overloads != null && overloads.Length != 0 && eval)
				{
					// filter out overloads(?)
					// then, there must be one overload remaining
					// execute that one, if superior expression is NOT a method call
				}
			}

			return null;
		}




		ISemantic E(PostfixExpression_Index x, ISemantic foreExpression)
		{
			if (eval)
			{
				//TODO: Access pointer arrays(?)

				if (foreExpression is ArrayValue) // ArrayValue must be checked first due to inheritance!
				{
					var av = foreExpression as ArrayValue;

					// Make $ operand available
					var arrLen_Backup = ValueProvider.CurrentArrayLength;
					ValueProvider.CurrentArrayLength = av.Elements.Length;

					var n = E(x.Arguments[0]) as PrimitiveValue;

					ValueProvider.CurrentArrayLength = arrLen_Backup;

					if (n == null)
						throw new EvaluationException(x.Arguments[0], "Returned no value");

					int i = 0;
					try
					{
						i = Convert.ToInt32(n.Value);
					}
					catch { throw new EvaluationException(x.Arguments[0], "Index expression must be of type int"); }

					if (i < 0 || i > av.Elements.Length)
						throw new EvaluationException(x.Arguments[0], "Index out of range - it must be between 0 and " + av.Elements.Length);

					return av.Elements[i];
				}
				else if (foreExpression is AssociativeArrayValue)
				{
					var aa = (AssociativeArrayValue)foreExpression;

					var key = E(x.Arguments[0]);

					if (key == null)
						throw new EvaluationException(x.Arguments[0], "Returned no value");

					ISymbolValue val = null;

					foreach (var kv in aa.Elements)
						if (kv.Key.Equals(key))
							return kv.Value;

					throw new EvaluationException(x, "Could not find key '" + val + "'");
				}

				throw new EvaluationException(x.PostfixForeExpression, "Invalid index expression base value type", foreExpression);
			}
			else
			{
				if (foreExpression is AssocArrayType)
				{
					var ar = (AssocArrayType)foreExpression;
					/*
					 * myType_Array[0] -- returns TypeResult myType
					 * return the value type of a given array result
					 */
					//TODO: Handle opIndex overloads

					return ar.ValueType;
				}
				/*
				 * int* a = new int[10];
				 * 
				 * a[0] = 12;
				 */
				else if (foreExpression is PointerType)
					return ((PointerType)foreExpression).Base;

				ctxt.LogError(new ResolutionError(x, "Invalid base type for index expression"));
			}

			return null;
		}

		ISemantic E(PostfixExpression_Slice x, ISemantic foreExpression)
		{
			if (!eval)
				return foreExpression; // Still of the array's type.
			

			if (!(foreExpression is ArrayValue))
				throw new EvaluationException(x.PostfixForeExpression, "Must be an array");

			var ar = (ArrayValue)foreExpression;
			var sl = (PostfixExpression_Slice)x;

			// If the [ ] form is used, the slice is of the entire array.
			if (sl.FromExpression == null && sl.ToExpression == null)
				return foreExpression;

			// Make $ operand available
			var arrLen_Backup = ValueProvider.CurrentArrayLength;
			ValueProvider.CurrentArrayLength = ar.Elements.Length;

			var bound_lower = E(sl.FromExpression) as PrimitiveValue;
			var bound_upper = E(sl.ToExpression) as PrimitiveValue;

			ValueProvider.CurrentArrayLength = arrLen_Backup;

			if (bound_lower == null || bound_upper == null)
				throw new EvaluationException(bound_lower == null ? sl.FromExpression : sl.ToExpression, "Must be of an integral type");

			int lower = -1, upper = -1;
			try
			{
				lower = Convert.ToInt32(bound_lower.Value);
				upper = Convert.ToInt32(bound_upper.Value);
			}
			catch { throw new EvaluationException(lower != -1 ? sl.FromExpression : sl.ToExpression, "Boundary expression must base an integral type"); }

			if (lower < 0)
				throw new EvaluationException(sl.FromExpression, "Lower boundary must be greater than 0");
			if (lower >= ar.Elements.Length)
				throw new EvaluationException(sl.FromExpression, "Lower boundary must be smaller than " + ar.Elements.Length);
			if (upper < lower)
				throw new EvaluationException(sl.ToExpression, "Upper boundary must be greater than " + lower);
			if (upper >= ar.Elements.Length)
				throw new EvaluationException(sl.ToExpression, "Upper boundary must be smaller than " + ar.Elements.Length);


			var rawArraySlice = new ISymbolValue[upper - lower];
			int j = 0;
			for (int i = lower; i < upper; i++)
				rawArraySlice[j++] = ar.Elements[i];

			return new ArrayValue(ar.RepresentedType as ArrayType, rawArraySlice);
		}

		ISemantic E(PostfixExpression_Increment x, ISemantic foreExpression)
		{
			// myInt++ is still of type 'int'
			if (!eval)
				return foreExpression;

			if (resolveConstOnly)
				throw new NoConstException(x);
			// Must be implemented anyway regarding ctfe
			return null;
		}

		ISemantic E(PostfixExpression_Decrement x, ISemantic foreExpression)
		{
			if (!eval)
				return foreExpression;

			if (resolveConstOnly)
				throw new NoConstException(x);
			// Must be implemented anyway regarding ctfe
			return null;
		}
	}
}
