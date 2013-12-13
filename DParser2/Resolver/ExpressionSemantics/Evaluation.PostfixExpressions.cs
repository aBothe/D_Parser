using System;
using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver.Templates;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		ISemantic E(PostfixExpression ex)
		{
			if (ex is PostfixExpression_MethodCall)
				return E((PostfixExpression_MethodCall)ex, !ctxt.Options.HasFlag(ResolutionOptions.ReturnMethodReferencesOnly));

			var foreExpr=E(ex.PostfixForeExpression);

			if(foreExpr is AliasedType)
				foreExpr = DResolver.StripAliasSymbol((AbstractType)foreExpr);

			if (foreExpr == null)
			{
				if (eval)
					return null;
				else
				{
					ctxt.LogError(new NothingFoundError(ex.PostfixForeExpression));
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
			// opIndex/opSlice ?
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
			AbstractType[] baseExpression;
			ISymbolValue baseValue;
			TemplateInstanceExpression tix;
			
			GetRawCallOverloads(call, out baseExpression, out baseValue, out tix);

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
							continue;
						}
						else if (mr.Definition is DVariable)
						{
							// If we've got a variable here, get its base type/value reference
							if (eval)
							{
								var dgVal = ValueProvider[(DVariable)mr.Definition] as DelegateValue;

								if (dgVal != null)
								{
									nextResults.Add(dgVal.Definition);
									continue;
								}
								else{
									EvalError(call, "Variable must be a delegate, not anything else", mr);
									return null;
								}
							}
							else
							{
								var bt = DResolver.StripAliasSymbol(mr.Base ?? TypeDeclarationResolver.ResolveSingle(mr.Definition.Type, ctxt));

								// Must be of type delegate
								if (bt is DelegateType)
								{
									var ret = HandleCallDelegateType(bt as DelegateType, methodOverloads, returnBaseTypeOnly);
									if(ret != null)
										return ret;
								}
								else
								{
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
									nextResults.Add(bt);
									requireStaticItems = false;
								}
								//TODO: Can other types work as function/are callable?
							}
						}
					}
					else if (b is DelegateType)
					{
						var ret = HandleCallDelegateType(b as DelegateType, methodOverloads, returnBaseTypeOnly);
						if(ret != null)
							return ret;
					}
					else if (b is ClassType || b is StructType)
					{
						var tit = (TemplateIntermediateType)b;
						/*
						 * auto a = MyStruct(); -- opCall-Overloads can be used
						 */
						var classDef = tit.Definition;

						if (classDef == null)
							continue;

						foreach (var i in GetOpCalls(tit, requireStaticItems))
								methodOverloads.Add(TypeDeclarationResolver.HandleNodeMatch(i, ctxt, b, call) as MemberSymbol);

						/*
						 * Every struct can contain a default ctor:
						 * 
						 * struct S { int a; bool b; }
						 * 
						 * auto s = S(1,true); -- ok
						 * auto s2= new S(2,false); -- error, no constructor found!
						 */
						if (b is StructType && methodOverloads.Count == 0)
						{
							//TODO: Deduce parameters
							return b;
						}
					}

					/*
					 * If the overload is a template, it quite exclusively means that we'll handle a method that is the only
					 * child inside a template + that is named as the template.
					 */
					else if (b is TemplateType)
						methodOverloads.Add(b);
				}

				scanResults = nextResults.Count == 0 ? null : nextResults.ToArray();
				nextResults.Clear();
			}
			#endregion

			if (methodOverloads.Count == 0)
				return null;

			// Get all arguments' types
			var callArguments = new List<ISemantic>();
			bool hasNonFinalArgs = false;

			if (call.Arguments != null)
				foreach (var arg in call.Arguments)
					callArguments.Add(E(arg));

			#region If explicit template type args were given, try to associate them with each overload
			if (tix != null)
			{
				var args = TemplateInstanceHandler.PreResolveTemplateArgs(tix, ctxt, out hasNonFinalArgs);
				var deducedOverloads = TemplateInstanceHandler.DeduceParamsAndFilterOverloads(methodOverloads, args, true, ctxt, hasNonFinalArgs);
				methodOverloads.Clear();
				if(deducedOverloads != null)
					methodOverloads.AddRange(deducedOverloads);
			}
			#endregion

			#region Filter by parameter-argument comparison
			var argTypeFilteredOverloads = new List<AbstractType>();
			bool hasHandledUfcsResultBefore = false;
			
			foreach (var ov in methodOverloads)
			{
				if (ov is MemberSymbol)
				{
					var ms = ov as MemberSymbol;
					var dm = ms.Definition as DMethod;

					if (dm != null)
					{
						ISemantic firstUfcsArg;
						bool isUfcs = UFCSResolver.IsUfcsResult(ov, out firstUfcsArg);
						// In the case of an ufcs, insert the first argument into the CallArguments list
						if (isUfcs && !hasHandledUfcsResultBefore)
						{
							callArguments.Insert(0, eval ? baseValue as ISemantic : firstUfcsArg);
							hasHandledUfcsResultBefore = true;
						}
						else if (!isUfcs && hasHandledUfcsResultBefore) // In the rare case of having a ufcs result occuring _after_ a normal member result, remove the initial arg again
						{
							callArguments.RemoveAt(0);
							hasHandledUfcsResultBefore = false;
						}
						
						var deducedTypeDict = new DeducedTypeDictionary(ms);
						var templateParamDeduction = new TemplateParameterDeduction(deducedTypeDict, ctxt);


						if(dm.Parameters.Count == 0 && callArguments.Count > 0)
							continue;

						int currentArg = 0;
						bool add = true;
						if (dm.Parameters.Count > 0 || callArguments.Count > 0)
							for (int i=0; i< dm.Parameters.Count; i++)
							{
								var paramType = dm.Parameters[i].Type;

								// Handle the usage of tuples: Tuples may only be used as as-is, so not as an array, pointer or in a modified way..
								if (paramType is IdentifierDeclaration &&
									TryHandleMethodArgumentTuple(ref add, callArguments, dm, deducedTypeDict, i, ref currentArg))
									continue;
								else if (currentArg < callArguments.Count)
								{
									if (!templateParamDeduction.HandleDecl(null, paramType, callArguments[currentArg++]))
									{
										add = false;
										break;
									}
								}
								else
								{
									// If there are more parameters than arguments given, check if the param has default values
									if (!(dm.Parameters[i] is DVariable) || (dm.Parameters[i] as DVariable).Initializer == null)
									{
										add = false;
										break;
									}
									// Assume that all further method parameters do have default values - and don't check further parameters
									break;
								}
							}

						// If type params were unassigned, try to take the defaults
						if (add && dm.TemplateParameters != null)
						{
							foreach (var tpar in dm.TemplateParameters)
							{
								if (deducedTypeDict[tpar] == null)
								{
									add = templateParamDeduction.Handle(tpar, null);
									if (!add)
									{
										if (hasNonFinalArgs)
										{
											deducedTypeDict[tpar] = new TemplateParameterSymbol(tpar, null);
											add = true;
										}
										else
											break;
									}
								}
							}
						}

						if (add && (deducedTypeDict.AllParamatersSatisfied || hasNonFinalArgs))
						{
							ms.DeducedTypes = deducedTypeDict.ToReadonly();
							var pop = ctxt.ScopedBlock != dm;
							if(pop)
								ctxt.PushNewScope(dm);
							ctxt.CurrentContext.IntroduceTemplateParameterTypes(ms);

							var bt = TypeDeclarationResolver.GetMethodReturnType(dm, ctxt) ?? ms.Base;

							if(pop)
								ctxt.Pop();
							else
								ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(ms);

							if(eval || !returnBaseTypeOnly)
								argTypeFilteredOverloads.Add(new MemberSymbol(dm, bt, ms.DeclarationOrExpressionBase, ms.DeducedTypes){ Tag = ms.Tag });
							else
								argTypeFilteredOverloads.Add(bt);
						}
					}
				}
				else if(ov is DelegateType)
				{
					var dg = ov as DelegateType;
					var bt = dg.Base ?? TypeDeclarationResolver.GetMethodReturnType(dg, ctxt);

					//TODO: Param-Arg check
						
					if (returnBaseTypeOnly)
						argTypeFilteredOverloads.Add(bt);
					else
					{
						if(dg.Base == null){
							if(dg.IsFunctionLiteral) 
								dg = new DelegateType(bt, dg.DeclarationOrExpressionBase as FunctionLiteral, dg.Parameters);
							else
								dg = new DelegateType(bt, dg.DeclarationOrExpressionBase as DelegateDeclaration, dg.Parameters);
						}
						argTypeFilteredOverloads.Add(new DelegateCallSymbol(dg, call));
					}
				}
			}
			#endregion

			if (eval)
			{
				// Convert ISemantic[] to ISymbolValue[]
				var args = new List<ISymbolValue>(callArguments.Count);

				foreach (var a in callArguments)
					args.Add(a as ISymbolValue);

				// Execute/Evaluate the variable contents etc.
				return TryDoCTFEOrGetValueRefs(argTypeFilteredOverloads.ToArray(), call.PostfixForeExpression, true, args.ToArray());
			}
			else
			{
				// Check if one overload remains and return that one.
				ctxt.CheckForSingleResult(argTypeFilteredOverloads, call);
				return argTypeFilteredOverloads.Count != 0 ? argTypeFilteredOverloads[0] : null;
			}
		}

		ISemantic HandleCallDelegateType(DelegateType dg, List<AbstractType> methodOverloads, bool returnBaseTypeOnly)
		{
			if(returnBaseTypeOnly)
				return eval ? new TypeValue(dg.Base) as ISemantic : dg.Base;

			/*
			 * int a = delegate(x) { return x*2; } (12); // a is 24 after execution
			 * auto dg=delegate(x) {return x*3;};
			 * int b = dg(4);
			 */

			if (!eval) {
				methodOverloads.Add (dg);
				return null;
			}
			else
			{
				// If it's just wanted to pass back the delegate's return type, skip the remaining parts of this method.
				EvalError(dg.DeclarationOrExpressionBase as IExpression, "TODO", dg);
				return null;
			}
		}

		private bool TryHandleMethodArgumentTuple(ref bool add,
			List<ISemantic> callArguments, 
			DMethod dm, 
			DeducedTypeDictionary deducedTypeDict, int currentParameter,ref int currentArg)
		{
			// .. so only check if it's an identifer & if the id represents a tuple parameter
			var id = dm.Parameters[currentParameter].Type as IdentifierDeclaration;
			var curNode = dm as DNode;
			TemplateParameter tpar = null;
			while (curNode != null && !curNode.TryGetTemplateParameter(id.IdHash, out tpar))
				curNode = curNode.Parent as DNode;

			if (!(tpar is TemplateTupleParameter))
				return false;

			int lastArgumentToTake = -1;
			/*
			 * Note: an expression tuple parameter can occur also somewhere in between the parameter list!
			 * void write(A...)(bool b, A a, double d) {}
			 * 
			 * can be matched by
			 * write(true, 1.2) as well as
			 * write(true, "asdf", 1.2) as well as
			 * write(true, 123, true, 'c', [3,4,5], 3.4) !
			 */

			TemplateParameterSymbol tps;
			DTuple tuple = null;
			if (deducedTypeDict.TryGetValue(tpar, out tps) && tps != null)
			{
				if (tps.Base is DTuple)
				{
					tuple = tps.Base as DTuple;
					lastArgumentToTake = currentParameter + (tuple.Items == null ? 0 : (tuple.Items.Length-1));
				}
				else
				{
					// Error: Type param must be tuple!
				}
			}
			// - Get the (amount of) arguments that shall be put into the tuple
			else if (currentParameter == dm.Parameters.Count - 1)
			{
				// The usual case: A tuple of a variable length is put at the end of a parameter list..
				// take all arguments from i until the end of the argument list..
				// ; Also accept empty tuples
				lastArgumentToTake = callArguments.Count - 1;
			}
			else
			{
				// Get the type of the next expected parameter
				var nextExpectedParameter = DResolver.StripMemberSymbols(TypeDeclarationResolver.ResolveSingle(dm.Parameters[currentParameter + 1].Type, ctxt));

				// Look for the first argument whose type is equal to the next parameter's type..
				for (int k = currentArg; k < callArguments.Count; k++)
				{
					if (ResultComparer.IsEqual(AbstractType.Get(callArguments[k]), nextExpectedParameter))
					{
						// .. and assume the tuple to go from i to the previous argument..
						lastArgumentToTake = k - 1;
						break;
					}
				}
			}

			int argCountToHandle = lastArgumentToTake - currentArg + 1;

			if (tuple != null)
			{
				// - If there's been set an explicit type tuple, compare all arguments' types with those in the tuple
				if(tuple.Items != null)
					foreach (ISemantic item in tuple.Items)
					{
						if (currentArg >= callArguments.Count || !ResultComparer.IsImplicitlyConvertible(callArguments[currentArg++], AbstractType.Get(item), ctxt))
						{
							add = false;
							return true;
						}
					}
			}
			else
			{
				// - If there was no explicit initialization, put all arguments' types into a type tuple 
				var argsToTake = new ISemantic[argCountToHandle];
				callArguments.CopyTo(currentArg, argsToTake, 0, argsToTake.Length);
				currentArg += argsToTake.Length;
				var tt = new DTuple(null, argsToTake);
				tps = new TemplateParameterSymbol(tpar, tt);

				//   and set the actual template tuple parameter deduction
				deducedTypeDict[tpar] = tps;
			}
			add = true;
			return true;
		}

		void GetRawCallOverloads(PostfixExpression_MethodCall call, 
			out AbstractType[] baseExpression, 
			out ISymbolValue baseValue, 
			out TemplateInstanceExpression tix)
		{
			baseExpression = null;
			baseValue = null;
			tix = null;

			if (call.PostfixForeExpression is PostfixExpression_Access)
			{
				var pac = (PostfixExpression_Access)call.PostfixForeExpression;
				tix = pac.AccessExpression as TemplateInstanceExpression;

				var vs = E(pac, null, false, false);

				if (vs != null && vs.Length != 0)
				{
					if (vs[0] is ISymbolValue)
					{
						baseValue = (ISymbolValue)vs[0];
						baseExpression = new[] { baseValue.RepresentedType };
					}
					else if (vs[0] is InternalOverloadValue)
						baseExpression = ((InternalOverloadValue)vs[0]).Overloads;
					else
						baseExpression = TypeDeclarationResolver.Convert(vs);
				}
			}
			else
			{
				// Explicitly don't resolve the methods' return types - it'll be done after filtering to e.g. resolve template types to the deduced one
				var optBackup = ctxt.CurrentContext.ContextDependentOptions;
				ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.DontResolveBaseTypes;

				if (call.PostfixForeExpression is TokenExpression)
					baseExpression = GetResolvedConstructorOverloads((TokenExpression)call.PostfixForeExpression, ctxt);
				else if (eval)
				{
					if (call.PostfixForeExpression is TemplateInstanceExpression)
						baseValue = E(tix = call.PostfixForeExpression as TemplateInstanceExpression, false) as ISymbolValue;
					else if (call.PostfixForeExpression is IdentifierExpression)
						baseValue = E((IdentifierExpression)call.PostfixForeExpression, false) as ISymbolValue;
					else
						baseValue = E(call.PostfixForeExpression) as ISymbolValue;

					if (baseValue is InternalOverloadValue)
						baseExpression = ((InternalOverloadValue)baseValue).Overloads;
					else if (baseValue != null)
						baseExpression = new[] { baseValue.RepresentedType };
					else baseExpression = null;
				}
				else
				{
					if (call.PostfixForeExpression is TemplateInstanceExpression)
						baseExpression = GetOverloads(tix = (TemplateInstanceExpression)call.PostfixForeExpression, null, false);
					else if (call.PostfixForeExpression is IdentifierExpression)
						baseExpression = GetOverloads(call.PostfixForeExpression as IdentifierExpression, deduceParameters:false);
					else
						baseExpression = new[] { AbstractType.Get(E(call.PostfixForeExpression)) };
				}

				ctxt.CurrentContext.ContextDependentOptions = optBackup;
			}
		}

		public static AbstractType[] GetAccessedOverloads(PostfixExpression_Access acc, ResolutionContext ctxt,
			ISemantic resultBase = null, bool DeducePostfixTemplateParams = true)
		{
			return TypeDeclarationResolver.Convert(new Evaluation(ctxt).E(acc, resultBase, DeducePostfixTemplateParams));
		}

		/// <summary>
		/// Returns either all unfiltered and undeduced overloads of a member of a base type/value (like b from type a if the expression is a.b).
		/// if <param name="EvalAndFilterOverloads"></param> is false.
		/// If true, all overloads will be deduced, filtered and evaluated, so that (in most cases,) a one-item large array gets returned
		/// which stores the return value of the property function b that is executed without arguments.
		/// Also handles UFCS - so if filtering is wanted, the function becom
		/// </summary>
		ISemantic[] E(PostfixExpression_Access acc,
			ISemantic resultBase = null, bool EvalAndFilterOverloads = true, bool ResolveImmediateBaseType = true)
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
			
			
			AbstractType[] overloads;
			var optBackup = ctxt.CurrentContext.ContextDependentOptions;
			
			if (acc.AccessExpression is TemplateInstanceExpression)
			{
				if (!ResolveImmediateBaseType)
					ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.DontResolveBaseTypes;

				var tix = (TemplateInstanceExpression)acc.AccessExpression;
				// Do not deduce and filter if superior expression is a method call since call arguments' types also count as template arguments!
				overloads = GetOverloads(tix, new[] { AbstractType.Get(baseExpression) }, EvalAndFilterOverloads);

				if (!ResolveImmediateBaseType)
					ctxt.CurrentContext.ContextDependentOptions = optBackup;
			}

			else if (acc.AccessExpression is IdentifierExpression)
			{
				var id = acc.AccessExpression as IdentifierExpression;

				if (eval && EvalAndFilterOverloads && resultBase != null)
				{
					var staticPropResult = StaticProperties.TryEvalPropertyValue(ValueProvider, resultBase, id.ValueStringHash);
					if (staticPropResult != null)
						return new[]{staticPropResult};
				}

				if (!ResolveImmediateBaseType)
					ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.DontResolveBaseTypes;

				overloads = GetOverloads(id, new[] { AbstractType.Get(baseExpression) }, EvalAndFilterOverloads);

				if (!ResolveImmediateBaseType)
					ctxt.CurrentContext.ContextDependentOptions = optBackup;
			}
			else
			{
				if (eval){
					EvalError(acc, "Invalid access expression");
					return null;
				}
				ctxt.LogError(acc, "Invalid post-dot expression");
				return null;
			}

			/*
			 * Try to get ufcs functions at first!
			 * 
			 * void foo(int i) {}
			 * 
			 * class A
			 * {
			 *	void foo(int i, int a) {}
			 * 
			 *	void bar(){
			 *		123.foo(23); // Not allowed! 
			 *		// Anyway, if we tried to search ufcs functions AFTER searching from child to parent scope levels,
			 *		// it would return the local foo() only, not the global one..which would be an error then!
			 *  }
			 *  
			 * Probably also worth to notice is the property syntax..are property functions rather preferred than ufcs ones?
			 * }
			 */
			if(overloads == null || EvalAndFilterOverloads)
			{
				var	oo = UFCSResolver.TryResolveUFCS(baseExpression, acc, ctxt);
	
				if (oo.Count > 0) {
					if (overloads != null && overloads.Length != 0)
						oo.AddRange (overloads);
					overloads = oo.ToArray();
				}
			}

			// If evaluation active and the access expression is stand-alone, return a single item only.
			if (EvalAndFilterOverloads && eval)
				return new[] { TryDoCTFEOrGetValueRefs(overloads, acc.AccessExpression) };

			return overloads;
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

					if (n == null){
						EvalError(x.Arguments[0], "Returned no value");
						return null;
					}

					int i = 0;
					try{
						i = Convert.ToInt32(n.Value);						
					}
					catch
					{
						EvalError(x.Arguments[0], "Index expression must be of type int");
						return null;
					}

					if (i < 0 || i > av.Elements.Length){
						EvalError(x.Arguments[0], "Index out of range - it must be between 0 and " + av.Elements.Length);
						return null;
					}

					return av.Elements[i];
				}
				else if (foreExpression is AssociativeArrayValue)
				{
					var aa = (AssociativeArrayValue)foreExpression;

					var key = E(x.Arguments[0]);

					if (key == null){
						EvalError(x.Arguments[0], "Returned no value");
						return null;
					}

					ISymbolValue val = null;

					foreach (var kv in aa.Elements)
						if (kv.Key.Equals(key))
							return kv.Value;

					EvalError(x, "Could not find key '" + val + "'");
					return null;
				}

				EvalError(x.PostfixForeExpression, "Invalid index expression base value type", foreExpression);
				return null;
			}
			else
			{
				foreExpression = DResolver.StripMemberSymbols(AbstractType.Get(foreExpression));
				
				if (foreExpression is AssocArrayType) {
					var ar = foreExpression as AssocArrayType;
					/*
					 * myType_Array[0] -- returns TypeResult myType
					 * return the value type of a given array result
					 */
					//TODO: Handle opIndex overloads

					return new ArrayAccessSymbol(x,ar.ValueType);
				}
				/*
				 * int* a = new int[10];
				 * 
				 * a[0] = 12;
				 */
				else if (foreExpression is PointerType)
					return (foreExpression as PointerType).Base;
					//return new ArrayAccessSymbol(x,((PointerType)foreExpression).Base);

				else if (foreExpression is DTuple)
				{
					var tt = foreExpression as DTuple;

					if (x.Arguments != null && x.Arguments.Length != 0)
					{
						var idx = EvaluateValue(x.Arguments[0], ctxt) as PrimitiveValue;

						if (idx == null || !DTokens.BasicTypes_Integral[idx.BaseTypeToken])
						{
							ctxt.LogError(x.Arguments[0], "Index expression must evaluate to integer value");
						}
						else if (idx.Value > (decimal)Int32.MaxValue || 
								 (int)idx.Value >= tt.Items.Length || 
								 (int)idx.Value < 0)
						{
							ctxt.LogError(x.Arguments[0], "Index number must be a value between 0 and " + tt.Items.Length);
						}
						else
						{
							return tt.Items[(int)idx.Value];
						}
					}
				}

				ctxt.LogError(new ResolutionError(x, "Invalid base type for index expression"));
			}

			return null;
		}

		ISemantic E(PostfixExpression_Slice x, ISemantic foreExpression)
		{
			if (!eval)
				return foreExpression; // Still of the array's type.
			

			if (!(foreExpression is ArrayValue)){
				EvalError(x.PostfixForeExpression, "Must be an array");
				return null;
			}

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

			if (bound_lower == null || bound_upper == null){
				EvalError(bound_lower == null ? sl.FromExpression : sl.ToExpression, "Must be of an integral type");
				return null;
			}

			int lower = -1, upper = -1;
			try
			{
				lower = Convert.ToInt32(bound_lower.Value);
				upper = Convert.ToInt32(bound_upper.Value);
			}
			catch { EvalError(lower != -1 ? sl.FromExpression : sl.ToExpression, "Boundary expression must base an integral type"); 
				return null;
			}

			if (lower < 0){
				EvalError(sl.FromExpression, "Lower boundary must be greater than 0");return null;}
			if (lower >= ar.Elements.Length){
				EvalError(sl.FromExpression, "Lower boundary must be smaller than " + ar.Elements.Length);return null;}
			if (upper < lower){
				EvalError(sl.ToExpression, "Upper boundary must be greater than " + lower);return null;}
			if (upper >= ar.Elements.Length){
				EvalError(sl.ToExpression, "Upper boundary must be smaller than " + ar.Elements.Length);return null;}


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
				EvalError(new NoConstException(x));
			// Must be implemented anyway regarding ctfe
			return null;
		}

		ISemantic E(PostfixExpression_Decrement x, ISemantic foreExpression)
		{
			if (!eval)
				return foreExpression;

			if (resolveConstOnly)
				EvalError(new NoConstException(x));
			// Must be implemented anyway regarding ctfe
			return null;
		}
	}
}
