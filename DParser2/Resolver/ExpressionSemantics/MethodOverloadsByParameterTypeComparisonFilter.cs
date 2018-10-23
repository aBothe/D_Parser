﻿using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.Templates;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ExpressionSemantics
{
	internal static class MethodOverloadsByParameterTypeComparisonFilter
	{
		public static AbstractType FilterOverloads(
			PostfixExpression_MethodCall call,
			IEnumerable<AbstractType> methodOverloads,
			ResolutionContext ctxt,
			StatefulEvaluationContext valueProvider,
			bool returnBaseTypeOnly,
			List<AbstractType> callArguments)
		{
			var visitor = new OverloadFilterVisitor(call, ctxt, valueProvider, returnBaseTypeOnly, callArguments);

			foreach (var ov in methodOverloads)
			{
				ov.Accept(visitor);
			}

			// Prefer untemplated methods over templated ones
			if (visitor.untemplatedMethodResult != null)
				return visitor.untemplatedMethodResult;

			return AmbiguousType.Get(visitor.argTypeFilteredOverloads);
		}

		class OverloadFilterVisitor : IResolvedTypeVisitor
		{
			readonly PostfixExpression_MethodCall call;
			readonly ResolutionContext ctxt;
			readonly StatefulEvaluationContext valueProvider;
			readonly bool returnBaseTypeOnly;
			readonly List<AbstractType> callArguments;

			bool hasHandledUfcsResultBefore = false;

			public AbstractType untemplatedMethodResult;
			public readonly List<AbstractType> argTypeFilteredOverloads = new List<AbstractType>();

			public OverloadFilterVisitor(
				PostfixExpression_MethodCall call,
				ResolutionContext ctxt,
				StatefulEvaluationContext valueProvider,
				bool returnBaseTypeOnly,
				List<AbstractType> callArguments)
			{
				this.call = call;
				this.ctxt = ctxt;
				this.valueProvider = valueProvider;
				this.returnBaseTypeOnly = returnBaseTypeOnly;
				this.callArguments = callArguments;
			}

			public void VisitMemberSymbol(MemberSymbol ms)
			{
				var dm = ms.Definition as DMethod;

				if (dm == null)
					return;

				ISemantic firstUfcsArg;
				bool isUfcs = UFCSResolver.IsUfcsResult(ms, out firstUfcsArg);
				// In the case of an ufcs, insert the first argument into the CallArguments list
				if (isUfcs && !hasHandledUfcsResultBefore)
				{
					callArguments.Insert(0, AbstractType.Get(firstUfcsArg));
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
								if (!TemplateTypeParameterTypeMatcher.TryMatchTypeDeclAgainstResolvedResult(paramType, callArguments[currentArg++], ctxt, deducedTypeDict, false))
								{
									add = false;
									break;
								}
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
						var bt = DSymbolBaseTypeResolver.GetMethodReturnType(dm, ctxt) ?? ms.Base;

						if (valueProvider != null || !returnBaseTypeOnly)
						{
							bt = new MemberSymbol(dm, bt, deducedTypeDict.Values) { Modifiers = ms.Modifiers };
							bt.AssignTagsFrom(ms);
						}

						if (dm.TemplateParameters == null || dm.TemplateParameters.Length == 0)
							untemplatedMethodResult = bt; //ISSUE: Have another state that indicates an ambiguous non-templated method matching.

						argTypeFilteredOverloads.Add(bt);
					}
				}
			}

			static bool TryHandleMethodArgumentTuple(ResolutionContext ctxt, ref bool add,
			List<AbstractType> callArguments,
			DMethod dm,
			DeducedTypeDictionary deducedTypeDict, int currentParameter, ref int currentArg)
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
						lastArgumentToTake = currentParameter + (tuple.Items == null ? 0 : (tuple.Items.Length - 1));
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
					if (tuple.Items != null)
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
					var argsToTake = new AbstractType[argCountToHandle];
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

			public void VisitDelegateType(DelegateType dg)
			{
				var bt = dg.Base ?? GetDelegateReturnType(dg, ctxt);

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

			static AbstractType GetDelegateReturnType(DelegateType dg, ResolutionContext ctxt)
			{
				if (dg == null || ctxt == null)
					return null;

				if (dg.IsFunctionLiteral)
					return DSymbolBaseTypeResolver.GetMethodReturnType(((FunctionLiteral)dg.delegateTypeBase).AnonymousMethod, ctxt);

				return TypeDeclarationResolver.ResolveSingle(((DelegateDeclaration)dg.delegateTypeBase).ReturnType, ctxt);
			}

			/// <summary>
			/// dmd 2.066: Uniform Construction Syntax. creal(3) is of type creal.
			/// </summary>
			/// <param name="ov"></param>
			public void VisitPrimitiveType(PrimitiveType ov)
			{
				argTypeFilteredOverloads.Add(ov);
			}

			public void VisitStructType(StructType ov)
			{
				argTypeFilteredOverloads.Add(ov);
			}

			public void VisitAliasedType(AliasedType t) { }
			public void VisitAmbigousType(AmbiguousType t) { }
			public void VisitArrayAccessSymbol(ArrayAccessSymbol t) { }
			public void VisitArrayType(ArrayType t) { }
			public void VisitAssocArrayType(AssocArrayType t) { }
			public void VisitClassType(ClassType t) { }
			public void VisitDelegateCallSymbol(DelegateCallSymbol t) { }
			public void VisitDTuple(DTuple t) { }
			public void VisitEnumType(EnumType t) { }
			public void VisitEponymousTemplateType(EponymousTemplateType t) { }
			public void VisitInterfaceType(InterfaceType t) { }
			public void VisitMixinTemplateType(MixinTemplateType t) { }
			public void VisitModuleSymbol(ModuleSymbol t) { }
			public void VisitPackageSymbol(PackageSymbol t) { }
			public void VisitPointerType(PointerType t) { }
			public void VisitStaticProperty(StaticProperty t) { }
			public void VisitTemplateParameterSymbol(TemplateParameterSymbol t) { }
			public void VisitTemplateType(TemplateType t) { }
			public void VisitUnionType(UnionType t) { }
			public void VisitUnknownType(UnknownType t) { }
		}
	}
}
