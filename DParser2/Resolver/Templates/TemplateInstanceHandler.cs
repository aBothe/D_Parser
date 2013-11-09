using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.Templates;
using System.Collections.ObjectModel;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver.TypeResolution
{
	public class TemplateInstanceHandler
	{
		public static List<ISemantic> PreResolveTemplateArgs(TemplateInstanceExpression tix, ResolutionContext ctxt, out bool hasNonFinalArgument)
		{
			hasNonFinalArgument = false;
			// Resolve given argument expressions
			var templateArguments = new List<ISemantic>();

			if (tix != null && tix.Arguments!=null)
				foreach (var arg in tix.Arguments)
				{
					if (arg is TypeDeclarationExpression)
					{
						var tde = (TypeDeclarationExpression)arg;

						var res = TypeDeclarationResolver.Resolve(tde.Declaration, ctxt);

						if (ctxt.CheckForSingleResult(res, tde.Declaration) || res != null)
						{
							var mr = res[0] as MemberSymbol;
							if (mr != null && mr.Definition is DVariable)
							{
								var dv = (DVariable)mr.Definition;

								if (dv.IsAlias || dv.Initializer == null)
								{
									templateArguments.Add(mr);
									continue;
								}

								ISemantic eval = null;

								try
								{
									eval = new StandardValueProvider(ctxt)[dv];
								}
								catch(System.Exception ee) // Should be a non-const-expression error here only
								{
									ctxt.LogError(dv.Initializer, ee.Message);
								}

								templateArguments.Add(eval==null ? (ISemantic)mr : eval);
							}
							else{
								if(!hasNonFinalArgument)
									hasNonFinalArgument = IsNonFinalArgument(res[0]);
								templateArguments.Add(res[0]);
							}
						}
					}
					else
					{
						var v = Evaluation.EvaluateValue(arg, ctxt, true);
						if (v is VariableValue)
						{
							var vv = v as VariableValue;
							if (vv.Variable.IsConst && vv.Variable.Initializer != null)
								v = Evaluation.EvaluateValue(vv, new StandardValueProvider(ctxt));
						}
						if(!hasNonFinalArgument)
							hasNonFinalArgument = IsNonFinalArgument(v);
						templateArguments.Add(v);
					}
				}

			return templateArguments;
		}
		
		static bool IsNonFinalArgument(ISemantic v)
		{
			return (v is TypeValue && (v as TypeValue).RepresentedType is TemplateParameterSymbol) ||
				(v is TemplateParameterSymbol && (v as TemplateParameterSymbol).Base == null);
		}

		public static AbstractType[] DeduceParamsAndFilterOverloads(IEnumerable<AbstractType> rawOverloadList,
			TemplateInstanceExpression templateInstanceExpr,
			ResolutionContext ctxt, bool isMethodCall = false)
		{
			bool hasUndeterminedArgs;
			var args = PreResolveTemplateArgs(templateInstanceExpr, ctxt, out hasUndeterminedArgs);
			return DeduceParamsAndFilterOverloads(rawOverloadList, args, isMethodCall, ctxt, hasUndeterminedArgs);
		}

		/// <summary>
		/// Associates the given arguments with the template parameters specified in the type/method declarations 
		/// and filters out unmatching overloads.
		/// </summary>
		/// <param name="rawOverloadList">Can be either type results or method results</param>
		/// <param name="givenTemplateArguments">A list of already resolved arguments passed explicitly 
		/// in the !(...) section of a template instantiation 
		/// or call arguments given in the (...) appendix 
		/// that follows a method identifier</param>
		/// <param name="isMethodCall">If true, arguments that exceed the expected parameter count will be ignored as far as all parameters could be satisfied.</param>
		/// <param name="ctxt"></param>
		/// <returns>A filtered list of overloads which mostly fit to the specified arguments.
		/// Usually contains only 1 element.
		/// The 'TemplateParameters' property of the results will be also filled for further usage regarding smart completion etc.</returns>
		public static AbstractType[] DeduceParamsAndFilterOverloads(IEnumerable<AbstractType> rawOverloadList,
			IEnumerable<ISemantic> givenTemplateArguments,
			bool isMethodCall,
			ResolutionContext ctxt,
			bool hasUndeterminedArguments=false)
		{
			if (rawOverloadList == null)
				return null;
			
			var filteredOverloads = hasUndeterminedArguments ? new List<AbstractType>(rawOverloadList) : 
				DeduceOverloads(rawOverloadList, givenTemplateArguments, isMethodCall, ctxt);

			AbstractType[] sortedAndFilteredOverloads;

			// If there are >1 overloads, filter from most to least specialized template param
			if (filteredOverloads.Count > 1)
				sortedAndFilteredOverloads = SpecializationOrdering.FilterFromMostToLeastSpecialized(filteredOverloads, ctxt);
			else if (filteredOverloads.Count == 1)
				sortedAndFilteredOverloads = new[] { filteredOverloads[0] };
			else
				return null;

			if (hasUndeterminedArguments)
				return sortedAndFilteredOverloads;
			
			if(sortedAndFilteredOverloads!=null)
			{
				filteredOverloads.Clear();
				for(int i = sortedAndFilteredOverloads.Length - 1; i >= 0; i--)
				{
					var ds = sortedAndFilteredOverloads[i] as DSymbol;
					if(ds != null && ds.Definition.TemplateConstraint != null)
					{
						ctxt.CurrentContext.IntroduceTemplateParameterTypes(ds);
						try{
							var v = Evaluation.EvaluateValue(ds.Definition.TemplateConstraint, ctxt);
							if(v is VariableValue)
								v = new StandardValueProvider(ctxt)[((VariableValue)v).Variable];
							if(!Evaluation.IsFalseZeroOrNull(v))
								filteredOverloads.Add(ds);
						}catch{} //TODO: Handle eval exceptions
						ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(ds);
					}
					else
						filteredOverloads.Add(sortedAndFilteredOverloads[i]);
				}
				if(filteredOverloads.Count == 0)
					return null;
				sortedAndFilteredOverloads = filteredOverloads.ToArray();
			}

			if (sortedAndFilteredOverloads != null &&
				sortedAndFilteredOverloads.Length == 1)
			{
				var t = sortedAndFilteredOverloads [0];
				if(t is TemplateType)
					return TryGetImplicitProperty (t as TemplateType, ctxt) ?? sortedAndFilteredOverloads;
				if (t is EponymousTemplateType)
					return new[]{ DeduceEponymousTemplate(t as EponymousTemplateType, ctxt) };
			}

			return sortedAndFilteredOverloads;
		}

		static AbstractType[] TryGetImplicitProperty(TemplateType template, ResolutionContext ctxt)
		{
			// Prepare a new context
			bool pop = !ctxt.ScopedBlockIsInNodeHierarchy(template.Definition);
			if (pop)
				ctxt.PushNewScope(template.Definition);

			// Introduce the deduced params to the current resolution context
			ctxt.CurrentContext.IntroduceTemplateParameterTypes(template);

			// Get actual overloads
			var matchingChild = TypeDeclarationResolver.ResolveFurtherTypeIdentifier( template.NameHash, new[]{ template }, ctxt);

			// Undo context-related changes
			if (pop)
				ctxt.Pop();
			else
				ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(template);

			return matchingChild;
		}

		static AbstractType DeduceEponymousTemplate(EponymousTemplateType ept, ResolutionContext ctxt)
		{
			if (ept.Definition.Initializer == null) {
				ctxt.LogError (ept.Definition, "Can't deduce type from empty initializer!");
				return null;
			}

			// Introduce the deduced params to the current resolution context
			ctxt.CurrentContext.IntroduceTemplateParameterTypes(ept);

			// Get actual overloads
			AbstractType deducedType = null;

			deducedType = new MemberSymbol (ept.Definition, Evaluation.EvaluateType (ept.Definition.Initializer, ctxt), null, ept.DeducedTypes); //ept; //Evaluation.EvaluateType (ept.Definition.Initializer, ctxt);

			// Undo context-related changes
			ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(ept);

			return deducedType;
		}

		private static List<AbstractType> DeduceOverloads(
			IEnumerable<AbstractType> rawOverloadList, 
			IEnumerable<ISemantic> givenTemplateArguments, 
			bool isMethodCall, 
			ResolutionContext ctxt)
		{
			bool hasTemplateArgsPassed = givenTemplateArguments != null && givenTemplateArguments.FirstOrDefault() != null;

			var filteredOverloads = new List<AbstractType>();

			if (rawOverloadList == null)
				return filteredOverloads;

			foreach (var o in rawOverloadList)
			{
				var overload = o as DSymbol;
				if (overload == null)
				{
					if(!hasTemplateArgsPassed)
						filteredOverloads.Add(o);
					continue;
				}

				var tplNode = overload.Definition;

				// Generically, the node should never be null -- except for TemplateParameterNodes that encapsule such params
				if (tplNode == null)
				{
					filteredOverloads.Add(overload);
					continue;
				}

				// If the type or method has got no template parameters and if there were no args passed, keep it - it's legit.
				if (tplNode.TemplateParameters == null)
				{
					if (!hasTemplateArgsPassed || isMethodCall)
						filteredOverloads.Add(overload);
					continue;
				}

				var deducedTypes = new DeducedTypeDictionary(tplNode);

				if (DeduceParams(givenTemplateArguments, isMethodCall, ctxt, overload, tplNode, deducedTypes))
				{
					overload.DeducedTypes = deducedTypes.ToReadonly(); // Assign calculated types to final result
					filteredOverloads.Add(overload);
				}
				else
					overload.DeducedTypes = null;
			}
			return filteredOverloads;
		}

		private static bool DeduceParams(IEnumerable<ISemantic> givenTemplateArguments, 
			bool isMethodCall, 
			ResolutionContext ctxt, 
			DSymbol overload, 
			DNode tplNode, 
			DeducedTypeDictionary deducedTypes)
		{
			bool isLegitOverload = true;

			var paramEnum = tplNode.TemplateParameters.GetEnumerator();

			var args = givenTemplateArguments ?? new List<ISemantic> ();

			var argEnum = args.GetEnumerator();
			foreach (var expectedParam in tplNode.TemplateParameters)
				if (!DeduceParam(ctxt, overload, deducedTypes, argEnum, expectedParam))
				{
					isLegitOverload = false;
					break; // Don't check further params if mismatch has been found
				}

			if (!isMethodCall && argEnum.MoveNext())
			{
				// There are too many arguments passed - discard this overload
				isLegitOverload = false;
			}
			return isLegitOverload;
		}

		private static bool DeduceParam(ResolutionContext ctxt, 
			DSymbol overload, 
			DeducedTypeDictionary deducedTypes,
			IEnumerator<ISemantic> argEnum, 
			TemplateParameter expectedParam)
		{
			if (expectedParam is TemplateThisParameter && overload.Base != null)
			{
				var ttp = (TemplateThisParameter)expectedParam;

				// Get the type of the type of 'this' - so of the result that is the overload's base
				var t = DResolver.StripMemberSymbols(overload.Base);

				if (t == null || t.DeclarationOrExpressionBase == null)
					return false;

				//TODO: Still not sure if it's ok to pass a type result to it 
				// - looking at things like typeof(T) that shall return e.g. const(A) instead of A only.

				if (!CheckAndDeduceTypeAgainstTplParameter(ttp, t, deducedTypes, ctxt))
					return false;

				return true;
			}

			// Used when no argument but default arg given
			bool useDefaultType = false;
			if (argEnum.MoveNext() || (useDefaultType = HasDefaultType(expectedParam)))
			{
				// On tuples, take all following arguments and pass them to the check function
				if (expectedParam is TemplateTupleParameter)
				{
					var tupleItems = new List<ISemantic>();
					// A tuple must at least contain one item!
					tupleItems.Add(argEnum.Current);
					while (argEnum.MoveNext())
						tupleItems.Add(argEnum.Current);

					if (!CheckAndDeduceTypeTuple((TemplateTupleParameter)expectedParam, tupleItems, deducedTypes, ctxt))
						return false;
				}
				else if (argEnum.Current != null)
				{
					if (!CheckAndDeduceTypeAgainstTplParameter(expectedParam, argEnum.Current, deducedTypes, ctxt))
						return false;
				}
				else if (useDefaultType && CheckAndDeduceTypeAgainstTplParameter(expectedParam, null, deducedTypes, ctxt))
				{
					// It's legit - just do nothing
				}
				else
					return false;
			}
			else if(expectedParam is TemplateTupleParameter)
			{
				if(!CheckAndDeduceTypeTuple(expectedParam as TemplateTupleParameter, null, deducedTypes, ctxt))
					return false;
			}
			// There might be too few args - but that doesn't mean that it's not correct - it's only required that all parameters got satisfied with a type
			else if (!deducedTypes.AllParamatersSatisfied)
				return false;

			return true;
		}

		public static bool HasDefaultType(TemplateParameter p)
		{
			if (p is TemplateTypeParameter)
				return ((TemplateTypeParameter)p).Default != null;
			else if (p is TemplateAliasParameter)
			{
				var ap = (TemplateAliasParameter)p;
				return ap.DefaultExpression != null || ap.DefaultType != null;
			}
			else if (p is TemplateThisParameter)
				return HasDefaultType(((TemplateThisParameter)p).FollowParameter);
			else if (p is TemplateValueParameter)
				return ((TemplateValueParameter)p).DefaultExpression != null;
			return false;
		}

		static bool CheckAndDeduceTypeAgainstTplParameter(TemplateParameter handledParameter, 
			ISemantic argumentToCheck,
			DeducedTypeDictionary deducedTypes,
			ResolutionContext ctxt)
		{
			return new Templates.TemplateParameterDeduction(deducedTypes, ctxt).Handle(handledParameter, argumentToCheck);
		}

		static bool CheckAndDeduceTypeTuple(TemplateTupleParameter tupleParameter, 
			IEnumerable<ISemantic> typeChain,
			DeducedTypeDictionary deducedTypes,
			ResolutionContext ctxt)
		{
			return new Templates.TemplateParameterDeduction(deducedTypes,ctxt).Handle(tupleParameter,typeChain);
		}
	}
}
