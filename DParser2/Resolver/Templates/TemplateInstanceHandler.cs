using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Evaluation;
using D_Parser.Resolver.Templates;

namespace D_Parser.Resolver.TypeResolution
{
	public class TemplateInstanceHandler
	{
		public static List<ResolveResult[]> PreResolveTemplateArgs(TemplateInstanceExpression tix, ResolverContextStack ctxt)
		{
			// Resolve given argument expressions
			var templateArguments = new List<ResolveResult[]>();

			if (tix != null && tix.Arguments!=null)
				foreach (var arg in tix.Arguments)
				{
					if (arg is TypeDeclarationExpression)
					{
						var tde = (TypeDeclarationExpression)arg;

						var r = TypeDeclarationResolver.Resolve(tde.Declaration, ctxt);

						var eval = ExpressionEvaluator.TryToEvaluateConstInitializer(r, ctxt);

						if (eval == null)
							templateArguments.Add(r);
						else
							templateArguments.Add(new[] { new ExpressionValueResult{
								DeclarationOrExpressionBase=eval.BaseExpression,
								Value=eval
							} });
					}
					else
						templateArguments.Add(new[] { ExpressionEvaluator.Resolve(arg, ctxt) });
				}

			return templateArguments;
		}

		public static ResolveResult[] EvalAndFilterOverloads(IEnumerable<ResolveResult> rawOverloadList,
			TemplateInstanceExpression templateInstanceExpr,
			ResolverContextStack ctxt)
		{
			return EvalAndFilterOverloads(rawOverloadList, PreResolveTemplateArgs(templateInstanceExpr, ctxt), false, ctxt);
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
		public static ResolveResult[] EvalAndFilterOverloads(IEnumerable<ResolveResult> rawOverloadList,
			IEnumerable<ResolveResult[]> givenTemplateArguments,
			bool isMethodCall,
			ResolverContextStack ctxt)
		{
			if (rawOverloadList == null)
				return null;

			bool hasTemplateArgsPassed = givenTemplateArguments != null;
			if (hasTemplateArgsPassed)
			{
				var enumm = givenTemplateArguments.GetEnumerator();
				hasTemplateArgsPassed = enumm.MoveNext();
				enumm.Dispose();
			}

			var filteredOverloads = new List<ResolveResult>();

			foreach (var overload in rawOverloadList)
			{
				var tplResult = overload as TemplateInstanceResult;

				// If result is not a node-related result (like Arrayresult or StaticType), add it if no arguments were passed
				if (tplResult == null)
				{
					if (!hasTemplateArgsPassed)
						filteredOverloads.Add(overload);
					continue;
				}

				var tplNode = tplResult.Node as DNode;

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

				var deducedTypes = new Dictionary<string, ResolveResult[]>();
				foreach (var param in tplNode.TemplateParameters)
					deducedTypes[param.Name] = null; // Init all params to null to let deduction functions know what params there are

				bool isLegitOverload = true;

				#region Deduction part
				var argEnum = givenTemplateArguments!=null ? givenTemplateArguments.GetEnumerator() :null;
				foreach (var expectedParam in tplNode.TemplateParameters)
				{
					// Used when no argument but default arg given
					bool useDefaultType = false;
					if ((argEnum!=null && argEnum.MoveNext()) || (useDefaultType = HasDefaultType(expectedParam)))
					{
						bool isLegitArgument = true;

						// On tuples, take all following arguments and pass them to the check function
						if (expectedParam is TemplateTupleParameter)
						{
							var tupleItems = new List<ResolveResult[]>();
							// A tuple must at least contain one item!
							tupleItems.Add(argEnum.Current);
							while (argEnum.MoveNext())
								tupleItems.Add(argEnum.Current);

							if (!CheckAndDeduceTypeTuple((TemplateTupleParameter)expectedParam, tupleItems, deducedTypes,ctxt))
								isLegitArgument = false;
						}
						else if (argEnum.Current!=null)
						{
							// Should contain one result usually
							foreach (var templateInstanceArg in argEnum.Current)
							{
								if (!CheckAndDeduceTypeAgainstTplParameter(expectedParam, templateInstanceArg,deducedTypes,ctxt))
								{
									isLegitArgument = false;
									continue;
								}
							}
						}
						else if (useDefaultType && CheckAndDeduceTypeAgainstTplParameter(expectedParam,null,deducedTypes,ctxt))
						{
							// It's legit - just do nothing
						}
						else
							isLegitArgument = false;

						if (!isLegitArgument)
							isLegitOverload = false;
					}
					// There might be too few args - but that doesn't mean that it's not correct - it's only required that all parameters got satisfied with a type
					else if(!AllParamatersSatisfied(deducedTypes))
					{
						// There's an insufficient number of arguments passed - discard this overload
						isLegitOverload = false;
					}

					// Don't check further params if mismatch has been found
					if (!isLegitOverload)
						break;
				}

				if (!isMethodCall && argEnum.MoveNext())
				{
					// There are too many arguments passed - discard this overload
					isLegitOverload = false;
				}
				#endregion

				if (isLegitOverload)
				{
					// Assign calculated types to final result
					tplResult.DeducedTypes = deducedTypes;

					filteredOverloads.Add(overload);
				}
				else
					tplResult.DeducedTypes = null;
			}

			// If there are >1 overloads, filter from most to least specialized template param
			if (filteredOverloads.Count > 1)
			{
				var specFiltered = SpecializationOrdering.FilterFromMostToLeastSpecialized(filteredOverloads, ctxt);
				return specFiltered == null ? null : specFiltered.ToArray();
			}
			else
				return filteredOverloads.Count == 0 ? null : filteredOverloads.ToArray();
		}

		static bool AllParamatersSatisfied(Dictionary<string, ResolveResult[]> deductions)
		{
			foreach (var kv in deductions)
				if (kv.Value == null || kv.Value==null || kv.Value.Length == 0)
					return false;

			return true;
		}

		static bool HasDefaultType(ITemplateParameter p)
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

		static bool CheckAndDeduceTypeAgainstTplParameter(ITemplateParameter handledParameter, 
			ResolveResult argumentToCheck, 
			Dictionary<string,ResolveResult[]> deducedTypes,
			ResolverContextStack ctxt)
		{
			return new Templates.TemplateParameterDeduction(deducedTypes, ctxt).Handle(handledParameter, argumentToCheck);
		}

		static bool CheckAndDeduceTypeTuple(TemplateTupleParameter tupleParameter, 
			IEnumerable<ResolveResult[]> typeChain, 
			Dictionary<string,ResolveResult[]> deducedTypes,
			ResolverContextStack ctxt)
		{
			return new Templates.TemplateParameterDeduction(deducedTypes,ctxt).Handle(tupleParameter,typeChain);
		}
	}
}
