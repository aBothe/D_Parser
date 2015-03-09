using System;
using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver.Templates;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial struct Evaluation
	{
		public ISymbolValue Visit(PostfixExpression_MethodCall call)
		{
			var returnBaseTypeOnly = !this.returnBaseTypeOnly.HasValue ? 
				!ctxt.Options.HasFlag(ResolutionOptions.ReturnMethodReferencesOnly) : 
				this.returnBaseTypeOnly.Value;
			this.returnBaseTypeOnly = null;

			List<ISemantic> callArguments;
			ISymbolValue delegValue;

			// Deduce template parameters later on
			AbstractType[] baseExpression;
			ISymbolValue baseValue;
			TemplateInstanceExpression tix;

			GetRawCallOverloads(ctxt, call, out baseExpression, out baseValue, out tix);

			var argTypeFilteredOverloads = EvalMethodCall(baseExpression, baseValue, tix, ctxt, call, out callArguments, out delegValue, returnBaseTypeOnly, ValueProvider);

			if (delegValue != null)
				return delegValue;
			if (argTypeFilteredOverloads == null)
				return null;

			// Convert ISemantic[] to ISymbolValue[]
			var args = new List<ISymbolValue>(callArguments != null ? callArguments.Count : 0);

			if(callArguments != null)
				foreach (var a in callArguments)
					args.Add(a as ISymbolValue);

			// Execute/Evaluate the variable contents etc.
			return TryDoCTFEOrGetValueRefs(argTypeFilteredOverloads, call.PostfixForeExpression, true, args.ToArray());
		}

		public static AbstractType EvalMethodCall(AbstractType[] baseExpression, ISymbolValue baseValue, TemplateInstanceExpression tix,
			ResolutionContext ctxt, 
			PostfixExpression_MethodCall call, out List<ISemantic> callArguments, out ISymbolValue delegateValue,
			bool returnBaseTypeOnly, AbstractSymbolValueProvider ValueProvider = null)
		{
			//TODO: Refactor this crap!

			delegateValue = null;
			callArguments = null;

			var methodOverloads = new List<AbstractType>();

			#region Search possible methods, opCalls or delegates that could be called
			bool requireStaticItems = true; //TODO: What if there's an opCall and a foreign method at the same time? - and then this variable would be bullshit
			IEnumerable<AbstractType> scanResults = baseExpression;
			var nextResults = new List<AbstractType>();

			while (scanResults != null)
			{
				foreach (var b in scanResults)
				{
					if (b is AmbiguousType)
						nextResults.AddRange((b as AmbiguousType).Overloads);
					else if (b is TemplateParameterSymbol)
						nextResults.Add((b as TemplateParameterSymbol).Base);
					else if (b is MemberSymbol)
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
							if (ValueProvider != null)
							{
								var dgVal = ValueProvider[(DVariable)mr.Definition] as DelegateValue;

								if (dgVal != null)
								{
									nextResults.Add(dgVal.Definition);
									continue;
								}
								else
								{
									ValueProvider.LogError(call, "Variable must be a delegate, not anything else");
									return null;
								}
							}
							else
							{
								var bt = mr.Base ?? TypeDeclarationResolver.ResolveSingle(mr.Definition.Type, ctxt);

								// Must be of type delegate
								if (bt is DelegateType)
								{
									var ret = HandleCallDelegateType(ValueProvider,bt as DelegateType, methodOverloads, returnBaseTypeOnly);
									if (ret is ISymbolValue)
									{
										delegateValue = ret as ISymbolValue;
										return null;
									}
									else if (ret is AbstractType)
										return ret as AbstractType;
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
						var ret = HandleCallDelegateType(ValueProvider,b as DelegateType, methodOverloads, returnBaseTypeOnly);
						if (ret is ISymbolValue)
						{
							delegateValue = ret as ISymbolValue;
							return null;
						}
						else if (ret is AbstractType)
							return ret as AbstractType;
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

						foreach (var i in ExpressionTypeEvaluation.GetOpCalls(tit, requireStaticItems))
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
					else if (b is PrimitiveType) // dmd 2.066: Uniform Construction Syntax. creal(3) is of type creal.
						methodOverloads.Add(b);
				}

				scanResults = nextResults.Count == 0 ? null : nextResults.ToArray();
				nextResults.Clear();
			}
			#endregion

			if (methodOverloads.Count == 0)
				return null;

			// Get all arguments' types
			callArguments = new List<ISemantic>();

			if (call.Arguments != null)
			{
				if (ValueProvider != null)
				{
					foreach (var arg in call.Arguments)
						callArguments.Add(arg != null ? Evaluation.EvaluateValue(arg, ValueProvider) : null);
				}
				else
					foreach (var arg in call.Arguments)
						callArguments.Add(arg != null ? ExpressionTypeEvaluation.EvaluateType(arg, ctxt) : null);
			}

			#region If explicit template type args were given, try to associate them with each overload
			if (tix != null)
			{
				var args = TemplateInstanceHandler.PreResolveTemplateArgs(tix, ctxt);
				var deducedOverloads = TemplateInstanceHandler.DeduceParamsAndFilterOverloads(methodOverloads, args, true, ctxt);
				methodOverloads.Clear();
				if (deducedOverloads != null)
					methodOverloads.AddRange(deducedOverloads);
			}
			#endregion

			#region Filter by parameter-argument comparison
			var argTypeFilteredOverloads = new List<AbstractType>();
			bool hasHandledUfcsResultBefore = false;
			AbstractType untemplatedMethodResult = null;

			foreach (var ov in methodOverloads)
			{
				if (ov is MemberSymbol)
					HandleDMethodOverload(ctxt, ValueProvider != null, baseValue, callArguments, returnBaseTypeOnly, argTypeFilteredOverloads, ref hasHandledUfcsResultBefore, 
						ov as MemberSymbol, ref untemplatedMethodResult);
				else if (ov is DelegateType)
				{
					var dg = ov as DelegateType;
					var bt = dg.Base ?? TypeDeclarationResolver.GetMethodReturnType(dg, ctxt);

					//TODO: Param-Arg check

					if (returnBaseTypeOnly)
						argTypeFilteredOverloads.Add(bt);
					else
					{
						if (dg.Base == null)
						{
							if (dg.IsFunctionLiteral)
								dg = new DelegateType(bt, dg.delegateTypeBase as FunctionLiteral, dg.Parameters);
							else
								dg = new DelegateType(bt, dg.delegateTypeBase as DelegateDeclaration, dg.Parameters);
						}
						argTypeFilteredOverloads.Add(new DelegateCallSymbol(dg, call));
					}
				}
				else if (ov is PrimitiveType) // dmd 2.066: Uniform Construction Syntax. creal(3) is of type creal.
				{
					if (ValueProvider != null)
					{
						if (callArguments == null || callArguments.Count != 1)
							ValueProvider.LogError(call, "Uniform construction syntax expects exactly one argument");
						else
						{
							var pv = callArguments[0] as PrimitiveValue;
							if (pv == null)
								ValueProvider.LogError(call, "Uniform construction syntax expects one built-in scalar value as first argument");
							else
								delegateValue = new PrimitiveValue(pv.Value, ov as PrimitiveType, pv.ImaginaryPart);
						}
					}

					argTypeFilteredOverloads.Add(ov);
				}
			}

			// Prefer untemplated methods over templated ones
			if (untemplatedMethodResult != null)
				return untemplatedMethodResult;
			#endregion

			return AmbiguousType.Get(argTypeFilteredOverloads);
		}

		static void HandleDMethodOverload(ResolutionContext ctxt, bool eval, ISymbolValue baseValue, List<ISemantic> callArguments, bool returnBaseTypeOnly, List<AbstractType> argTypeFilteredOverloads, ref bool hasHandledUfcsResultBefore, 
			MemberSymbol ms, ref AbstractType untemplatedMethod)
		{
			var dm = ms.Definition as DMethod;

			if (dm == null)
				return;

			

			ISemantic firstUfcsArg;
			bool isUfcs = UFCSResolver.IsUfcsResult(ms, out firstUfcsArg);
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

			if (dm.Parameters.Count == 0 && callArguments.Count > 0)
				return;

			var deducedTypeDict = new DeducedTypeDictionary(ms);
			var templateParamDeduction = new TemplateParameterDeduction(deducedTypeDict, ctxt);

			var back = ctxt.ScopedBlock;
			using (ctxt.Push(ms))
			{
				if (ctxt.ScopedBlock != back)
					ctxt.CurrentContext.DeducedTemplateParameters = deducedTypeDict;

				bool add = true;
				int currentArg = 0;
				if (dm.Parameters.Count > 0 || callArguments.Count > 0)
				{
					bool hadDTuples = false;
					for (int i = 0; i < dm.Parameters.Count; i++)
					{
						var paramType = dm.Parameters[i].Type;

						// Handle the usage of tuples: Tuples may only be used as as-is, so not as an array, pointer or in a modified way..
						if (paramType is IdentifierDeclaration &&
							(hadDTuples |= TryHandleMethodArgumentTuple(ctxt, ref add, callArguments, dm, deducedTypeDict, i, ref currentArg)))
							continue;
						else if (currentArg < callArguments.Count)
						{
							if (!(add = templateParamDeduction.HandleDecl(null, paramType, callArguments[currentArg++])))
								break;
						}
						else
						{
							// If there are more parameters than arguments given, check if the param has default values
							add = !(dm.Parameters[i] is DVariable) || (dm.Parameters[i] as DVariable).Initializer != null;

							// Assume that all further method parameters do have default values - and don't check further parameters
							break;
						}
					}

					// Too few args
					if (!hadDTuples && currentArg < callArguments.Count)
						add = false;
				}

				if (!add)
					return;

				// If type params were unassigned, try to take the defaults
				if (dm.TemplateParameters != null)
				{
					foreach (var tpar in dm.TemplateParameters)
					{
						if (deducedTypeDict[tpar] == null && !templateParamDeduction.Handle(tpar, null))
							return;
					}
				}

				if (deducedTypeDict.AllParamatersSatisfied)
				{
					ms.SetDeducedTypes(deducedTypeDict);
					var bt = TypeDeclarationResolver.GetMethodReturnType(dm, ctxt) ?? ms.Base;

					if (eval || !returnBaseTypeOnly) {
						bt = new MemberSymbol (dm, bt, ms.DeducedTypes) { Modifier = ms.Modifier };
						bt.AssignTagsFrom (ms);
					}

					if (dm.TemplateParameters == null || dm.TemplateParameters.Length == 0)
						untemplatedMethod = bt; //ISSUE: Have another state that indicates an ambiguous non-templated method matching.

					argTypeFilteredOverloads.Add(bt);
				}
			}
		}

		static ISemantic HandleCallDelegateType(AbstractSymbolValueProvider ValueProvider,DelegateType dg, List<AbstractType> methodOverloads, bool returnBaseTypeOnly)
		{
			if(returnBaseTypeOnly)
				return ValueProvider != null ? new TypeValue(dg.Base) as ISemantic : dg.Base;

			/*
			 * int a = delegate(x) { return x*2; } (12); // a is 24 after execution
			 * auto dg=delegate(x) {return x*3;};
			 * int b = dg(4);
			 */

			if (ValueProvider == null) {
				methodOverloads.Add (dg);
				return null;
			}
			else
			{
				// If it's just wanted to pass back the delegate's return type, skip the remaining parts of this method.
				//EvalError(dg.DeclarationOrExpressionBase as IExpression, "TODO", dg);
				ValueProvider.LogError(dg.delegateTypeBase, "Ctfe not implemented yet");
				return null;
			}
		}

		internal static bool TryHandleMethodArgumentTuple(ResolutionContext ctxt,ref bool add,
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
				var tt = new DTuple(argsToTake);
				tps = new TemplateParameterSymbol(tpar, tt);

				//   and set the actual template tuple parameter deduction
				deducedTypeDict[tpar] = tps;
			}
			add = true;
			return true;
		}

		void GetRawCallOverloads(ResolutionContext ctxt,PostfixExpression_MethodCall call, 
			out AbstractType[] baseExpression, 
			out ISymbolValue baseValue, 
			out TemplateInstanceExpression tix)
		{
			baseValue = null;
			tix = null;

			if (call.PostfixForeExpression is PostfixExpression_Access)
			{
				var pac = (PostfixExpression_Access)call.PostfixForeExpression;
				tix = pac.AccessExpression as TemplateInstanceExpression;

				var vs = EvalPostfixAccessExpression(this, ctxt, pac, null, false, false);

				baseExpression = AbstractType.Get(vs);
			}
			else
			{
				// Explicitly don't resolve the methods' return types - it'll be done after filtering to e.g. resolve template types to the deduced one
				var optBackup = ctxt.CurrentContext.ContextDependentOptions;
				ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.DontResolveBaseTypes;

				if (call.PostfixForeExpression is TokenExpression)
					baseExpression = ExpressionTypeEvaluation.GetResolvedConstructorOverloads((TokenExpression)call.PostfixForeExpression, ctxt);
				else
				{
					var fore = call.PostfixForeExpression;
					if (fore is TemplateInstanceExpression)
					{
						ImplicitlyExecute = false;
						tix = call.PostfixForeExpression as TemplateInstanceExpression;
					}
					else if (fore is IdentifierExpression)
						ImplicitlyExecute = false;

					if(call.PostfixForeExpression != null)
						baseValue = call.PostfixForeExpression.Accept(this) as ISymbolValue;

					if (baseValue is InternalOverloadValue)
						baseExpression = ((InternalOverloadValue)baseValue).Overloads;
					else if (baseValue != null)
						baseExpression = new[] { baseValue.RepresentedType };
					else 
						baseExpression = null;
				}

				ctxt.CurrentContext.ContextDependentOptions = optBackup;
			}
		}

		/// <summary>
		/// Returns either all unfiltered and undeduced overloads of a member of a base type/value (like b from type a if the expression is a.b).
		/// if <param name="EvalAndFilterOverloads"></param> is false.
		/// If true, all overloads will be deduced, filtered and evaluated, so that (in most cases,) a one-item large array gets returned
		/// which stores the return value of the property function b that is executed without arguments.
		/// Also handles UFCS - so if filtering is wanted, the function becom
		/// </summary>
		public static R[] EvalPostfixAccessExpression<R>(ExpressionVisitor<R> vis, ResolutionContext ctxt,PostfixExpression_Access acc,
			ISemantic resultBase = null, bool EvalAndFilterOverloads = true, bool ResolveImmediateBaseType = true, AbstractSymbolValueProvider ValueProvider = null)
			where R : class,ISemantic
		{
			if (acc == null)
				return null;

			var baseExpression = resultBase ?? (acc.PostfixForeExpression != null ? acc.PostfixForeExpression.Accept(vis) as ISemantic : null);

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
				overloads = ExpressionTypeEvaluation.GetOverloads(tix, ctxt, AbstractType.Get(baseExpression), EvalAndFilterOverloads);

				if (!ResolveImmediateBaseType)
					ctxt.CurrentContext.ContextDependentOptions = optBackup;
			}

			else if (acc.AccessExpression is IdentifierExpression)
			{
				var id = acc.AccessExpression as IdentifierExpression;

				if (ValueProvider != null && EvalAndFilterOverloads && baseExpression != null)
				{
					var staticPropResult = StaticProperties.TryEvalPropertyValue(ValueProvider, baseExpression, id.ValueStringHash);
					if (staticPropResult != null)
						return new[]{(R)staticPropResult};
				}

				if (!ResolveImmediateBaseType)
					ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.DontResolveBaseTypes;

				overloads = ExpressionTypeEvaluation.GetOverloads(id, ctxt, AbstractType.Get(baseExpression), EvalAndFilterOverloads);

				if (!ResolveImmediateBaseType)
					ctxt.CurrentContext.ContextDependentOptions = optBackup;
			}
			else
			{/*
				if (eval){
					EvalError(acc, "Invalid access expression");
					return null;
				}*/
				ctxt.LogError(acc, "Invalid post-dot expression");
				return null;
			}

			// If evaluation active and the access expression is stand-alone, return a single item only.
			if (EvalAndFilterOverloads && ValueProvider != null)
				return new[] { (R)new Evaluation(ValueProvider).TryDoCTFEOrGetValueRefs(AmbiguousType.Get(overloads), acc.AccessExpression) };

			return overloads as R[];
		}

		ISymbolValue EvalForeExpression(PostfixExpression ex)
		{
			return ex.PostfixForeExpression != null ? ex.PostfixForeExpression.Accept(this) : null;
		}

		public ISymbolValue Visit(PostfixExpression_Access ex)
		{
			var r = EvalPostfixAccessExpression(this, ctxt, ex, null, true, ValueProvider:ValueProvider);
			ctxt.CheckForSingleResult(r, ex);

			return r != null && r.Length != 0 ? r[0] : null;
		}

		public ISymbolValue Visit(PostfixExpression_Increment x)
		{
			var foreExpr = EvalForeExpression(x);

			if (resolveConstOnly)
				EvalError(new NoConstException(x));
			// Must be implemented anyway regarding ctfe/ Op overloading
			return null;
		}

		public ISymbolValue Visit(PostfixExpression_Decrement x)
		{
			var foreExpr = EvalForeExpression(x);

			if (resolveConstOnly)
				EvalError(new NoConstException(x));
			// Must be implemented anyway regarding ctfe
			return null;
		}

		public ISymbolValue Visit(PostfixExpression_ArrayAccess x)
		{
			var foreExpression = EvalForeExpression(x);

			if(x.Arguments != null)
				foreach (var arg in x.Arguments) {
					if (arg == null)
						continue;

					if (arg is PostfixExpression_ArrayAccess.SliceArgument)
						foreExpression = SliceArray (x, foreExpression, arg as PostfixExpression_ArrayAccess.SliceArgument);
					else
						foreExpression = AccessArrayAtIndex (x, foreExpression, arg);

					if (foreExpression == null)
						return null;
				}

			return foreExpression;
		}

		ISymbolValue AccessArrayAtIndex(PostfixExpression_ArrayAccess x, ISymbolValue foreExpression, PostfixExpression_ArrayAccess.IndexArgument ix)
		{
			//TODO: Access pointer arrays(?)

			if (foreExpression is ArrayValue) // ArrayValue must be checked first due to inheritance!
			{
				var av = foreExpression as ArrayValue;

				// Make $ operand available
				var arrLen_Backup = ValueProvider.CurrentArrayLength;
				ValueProvider.CurrentArrayLength = av.Elements.Length;

				var n = ix.Expression.Accept(this) as PrimitiveValue;

				ValueProvider.CurrentArrayLength = arrLen_Backup;

				if (n == null)
				{
					EvalError(ix.Expression, "Returned no value");
					return null;
				}

				int i = 0;
				try
				{
					i = Convert.ToInt32(n.Value);
				}
				catch
				{
					EvalError(ix.Expression, "Index expression must be of type int");
					return null;
				}

				if (i < 0 || i > av.Elements.Length)
				{
					EvalError(ix.Expression, "Index out of range - it must be between 0 and " + av.Elements.Length);
					return null;
				}

				return av.Elements[i];
			}
			else if (foreExpression is AssociativeArrayValue)
			{
				var aa = (AssociativeArrayValue)foreExpression;

				var key = ix.Expression.Accept(this) as PrimitiveValue;

				if (key == null)
				{
					EvalError(ix.Expression, "Returned no value");
					return null;
				}

				ISymbolValue val = null;

				foreach (var kv in aa.Elements)
					if (kv.Key.Equals(key))
						return kv.Value;

				EvalError(x, "Could not find key '" + val + "'");
				return null;
			}

			//TODO: myClassWithAliasThis[0] -- Valid!!

			EvalError(x.PostfixForeExpression, "Invalid index expression base value type", foreExpression);
			return null;
		}

		ISymbolValue SliceArray(IExpression x,ISymbolValue foreExpression, PostfixExpression_ArrayAccess.SliceArgument sl)
		{
			if (!(foreExpression is ArrayValue))
			{
				EvalError(x, "Must be an array");
				return null;
			}

			var ar = (ArrayValue)foreExpression;

			// If the [ ] form is used, the slice is of the entire array.
			if (sl.LowerBoundExpression == null && sl.UpperBoundExpression == null)
				//TODO: Clone it or append an item or so
				return foreExpression;

			// Make $ operand available
			var arrLen_Backup = ValueProvider.CurrentArrayLength;
			var len = ar.Length;
			ValueProvider.CurrentArrayLength = len;

			//TODO: Strip aliases and whatever things may break this
			var bound_lower = sl.LowerBoundExpression.Accept(this) as PrimitiveValue;
			var bound_upper = sl.UpperBoundExpression.Accept(this) as PrimitiveValue;

			ValueProvider.CurrentArrayLength = arrLen_Backup;

			if (bound_lower == null || bound_upper == null)
			{
				EvalError(bound_lower == null ? sl.LowerBoundExpression : sl.UpperBoundExpression, "Must be of an integral type");
				return null;
			}

			int lower = -1, upper = -1;
			try
			{
				lower = Convert.ToInt32(bound_lower.Value);
				upper = Convert.ToInt32(bound_upper.Value);
			}
			catch
			{
				EvalError(lower != -1 ? sl.LowerBoundExpression : sl.UpperBoundExpression, "Boundary expression must base an integral type");
				return null;
			}

			if (lower < 0)
			{
				EvalError(sl.LowerBoundExpression, "Lower boundary must be greater than 0"); return new NullValue(ar.RepresentedType);
			}
			if (lower >= len && len > 0)
			{
				EvalError(sl.LowerBoundExpression, "Lower boundary must be smaller than " + len); return new NullValue(ar.RepresentedType);
			}
			if (upper < lower)
			{
				EvalError(sl.UpperBoundExpression, "Upper boundary must be greater than " + lower); return new NullValue(ar.RepresentedType);
			}
			else if (upper > len)
			{
				EvalError(sl.UpperBoundExpression, "Upper boundary must be smaller than " + len); return new NullValue(ar.RepresentedType);
			}

			if (ar.IsString)
				return new ArrayValue(ar.RepresentedType as ArrayType, ar.StringValue.Substring(lower, upper - lower));

			var rawArraySlice = new ISymbolValue[upper - lower];
			int j = 0;
			for (int i = lower; i < upper; i++)
				rawArraySlice[j++] = ar.Elements[i];

			return new ArrayValue(ar.RepresentedType as ArrayType, rawArraySlice);
		}
	}
}
