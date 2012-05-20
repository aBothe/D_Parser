using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Parser;

namespace D_Parser.Resolver.TypeResolution
{
	public class TemplateInstanceHandler
	{
		public static ResolveResult[] EvalAndFilterOverloads(IEnumerable<ResolveResult> rawOverloadList,
			TemplateInstanceExpression templateInstanceExpr,
			ResolverContextStack ctxt)
		{
			var templateArguments = new List<ResolveResult[]>();

			if(templateInstanceExpr != null)
				foreach (var arg in templateInstanceExpr.Arguments)
					templateArguments.Add(ExpressionTypeResolver.Resolve(arg,ctxt));

			return EvalAndFilterOverloads(rawOverloadList, templateArguments, false, ctxt);
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
		/// <param name="isMethodCall">True if the givenTemplateArguments shall be treated as types of normal arguments</param>
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

				// Generically, the node should never be null
				if (tplNode == null)
					continue;

				// If the type or method has got no template parameters and if there were no args passed, keep it - it's legit.
				if (tplNode.TemplateParameters == null)
				{
					if (!hasTemplateArgsPassed || isMethodCall)
						filteredOverloads.Add(overload);
					continue;
				}

				tplResult.DeducedTypes = new Dictionary<string, ResolveResult[]>();
				foreach (var param in tplNode.TemplateParameters)
					tplResult.DeducedTypes[param.Name] = null; // Init all params to null to let deduction functions know what params there are

				bool isLegitOverload = true;

				if (isMethodCall)
				{
					//TODO
				}
				else
				{
					var argEnum = givenTemplateArguments.GetEnumerator();
					foreach (var expectedParam in tplNode.TemplateParameters)
					{
						if (argEnum.MoveNext())
						{
							bool isLegitArgument = true;

							// On tuples, take all following arguments and pass them to the check function
							if (expectedParam is TemplateTupleParameter)
							{
								var tupleItems = new List<ResolveResult[]>();
								tupleItems.Add(argEnum.Current);
								while (argEnum.MoveNext())
									tupleItems.Add(argEnum.Current);

								if (!CheckAndDeduceTypeTuple((TemplateTupleParameter)expectedParam, tupleItems, tplResult.DeducedTypes))
									isLegitArgument = false;
							}
							else if (argEnum.Current!=null)
							{
								// Should contain one result usually
								foreach (var templateInstanceArg in argEnum.Current)
								{
									if (!CheckAndDeduceTypeAgainstTplParameter(expectedParam, templateInstanceArg, tplResult.DeducedTypes))
									{
										isLegitArgument = false;
										continue;
									}
								}
							}
							else
								isLegitArgument = false;

							if (!isLegitArgument)
								isLegitOverload = false;
						}
						else
						{
							// There's an insufficient number of arguments passed - discard this overload
							isLegitOverload = false;
						}
					}

					if (argEnum.MoveNext())
					{
						// There are too many arguments passed - discard this overload
						isLegitOverload = false;
					}
				}

				if (isLegitOverload)
					filteredOverloads.Add(overload);
				else
					tplResult.DeducedTypes = null;
			}

			return filteredOverloads.Count == 0 ? null : filteredOverloads.ToArray();
		}

		static bool HasDefaultType(ITemplateParameter p)
		{
			if (p is TemplateTypeParameter)
				return ((TemplateTypeParameter)p).Default != null;
			else if (p is TemplateThisParameter)
				return HasDefaultType(((TemplateThisParameter)p).FollowParameter);
			else if (p is TemplateValueParameter)
				return ((TemplateValueParameter)p).DefaultExpression != null;
			return false;
		}

		static bool CheckAndDeduceTypeAgainstTplParameter(ITemplateParameter handledParameter, ResolveResult argumentToCheck, Dictionary<string,ResolveResult[]> deducedTypes)
		{
			return new Templates.TemplateParameterDeduction(deducedTypes).Handle(handledParameter,argumentToCheck);
		}

		static bool CheckAndDeduceTypeTuple(TemplateTupleParameter tupleParameter, IEnumerable<ResolveResult[]> typeChain, Dictionary<string,ResolveResult[]> deducedTypes)
		{
			return new Templates.TemplateParameterDeduction(deducedTypes).Handle(tupleParameter,typeChain);
		}
	}
}
