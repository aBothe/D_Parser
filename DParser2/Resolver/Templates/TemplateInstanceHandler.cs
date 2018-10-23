﻿using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.Templates;
using D_Parser.Resolver.ExpressionSemantics;
using System;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.Templates
{
	public static class TemplateInstanceHandler
	{
		public static List<ISemantic> PreResolveTemplateArgs(IEnumerable<DNode> rawOverloadList, TemplateInstanceExpression tix, ResolutionContext ctxt)
		{
			// Resolve given argument expressions
			var templateArguments = new List<ISemantic>();

			int currentTemplateArgumentIndex = -1;
			if (tix != null && tix.Arguments!=null)
				foreach (var optionallyWrappedArgument in tix.Arguments)
				{
					currentTemplateArgumentIndex++;
					ISyntaxRegion arg;
					if (optionallyWrappedArgument is TypeDeclarationExpression)
						arg = (optionallyWrappedArgument as TypeDeclarationExpression).Declaration;
					else
						arg = optionallyWrappedArgument;

					if (arg == null)
						continue;

					var aliasThisParam = arg is IntermediateIdType
							&& IsTemplateAliasParameterAtIndex(rawOverloadList, currentTemplateArgumentIndex);

					if (!aliasThisParam && arg is IExpression)
					{
						ISemantic v = Evaluation.EvaluateValue(arg as IExpression, ctxt);
						v = StripValueTypeWrappers(v);
						templateArguments.Add(v);
					}
					else
					{
						AbstractType res;
						if (aliasThisParam)
							res = AmbiguousType.Get(ExpressionTypeEvaluation.GetOverloads(arg as IntermediateIdType, ctxt, null, false));
						else if (arg is ITypeDeclaration)
							res = TypeDeclarationResolver.ResolveSingle(arg as ITypeDeclaration, ctxt);
						else
							throw new ArgumentNullException("arg");

						if (res is AmbiguousType)
						{
							// Error
							res = (res as AmbiguousType).Overloads[0];
						}

						var mr = res as MemberSymbol;
						if (null != mr && mr.Definition is DVariable)
						{
							var dv = mr.Definition as DVariable;
							if (dv.IsAlias || dv.Initializer == null)
							{
								templateArguments.Add(mr);
								continue;
							}

							ISemantic eval = null;
							try
							{
								eval = Evaluation.EvaluateValue(dv.Initializer, ctxt);
							}
							catch (Exception ee) // Should be a non-const-expression error here only
							{
								ctxt.LogError(dv.Initializer, ee.Message);
							}

							templateArguments.Add(eval ?? mr);
						}
						else
							templateArguments.Add(res);
					}
				}

			return templateArguments;
		}

		static ISemantic StripValueTypeWrappers(ISemantic s)
		{
			while (true)
				if (s is TypeValue)
					s = (s as TypeValue).RepresentedType;
				else
					return s;
		}

		static bool IsTemplateAliasParameterAtIndex(IEnumerable<DNode> nodeOverloads, int argumentIndex)
		{
			foreach(var dc in nodeOverloads)
			{
				var templateParameters = dc.TemplateParameters;
				if (templateParameters != null
					&& templateParameters.Length > argumentIndex
					&& templateParameters[argumentIndex] is TemplateAliasParameter)
					return true;
			}
			return false;
		}

		internal static bool IsNonFinalArgument(ISemantic v)
		{
			return (v is TypeValue && (v as TypeValue).RepresentedType is TemplateParameterSymbol) ||
				(v is TemplateParameterSymbol && (v as TemplateParameterSymbol).Base == null) ||
				v is ErrorValue;
		}

		public static List<AbstractType> DeduceParamsAndFilterOverloads(IEnumerable<AbstractType> rawOverloadList,
			TemplateInstanceExpression templateInstanceExpr,
			ResolutionContext ctxt, bool isMethodCall = false)
		{
			var dnodeOverloads = new List<DNode>();
			foreach(var at in rawOverloadList)
			{
				if (at is DSymbol)
					dnodeOverloads.Add((at as DSymbol).Definition);
			}
			var args = PreResolveTemplateArgs(dnodeOverloads, templateInstanceExpr, ctxt);
			return DeduceParamsAndFilterOverloads(rawOverloadList, args, isMethodCall, ctxt);
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
		public static List<AbstractType> DeduceParamsAndFilterOverloads(IEnumerable<AbstractType> rawOverloadList,
					IEnumerable<ISemantic> givenTemplateArguments,
					bool isMethodCall,
					ResolutionContext ctxt)
		{
			if (rawOverloadList == null)
				return new List<AbstractType>();

			var unfilteredOverloads = DeduceOverloads(rawOverloadList, givenTemplateArguments, isMethodCall, ctxt);

			IEnumerable<AbstractType> preFilteredOverloads;

			// If there are >1 overloads, filter from most to least specialized template param
			if (unfilteredOverloads.Count > 1)
				preFilteredOverloads = SpecializationOrdering.FilterFromMostToLeastSpecialized(unfilteredOverloads, ctxt);
			else if (unfilteredOverloads.Count == 1)
				preFilteredOverloads = unfilteredOverloads;
			else
				return new List<AbstractType>();

			var templateConstraintFilteredOverloads = new List<AbstractType>();
			foreach (var overload in preFilteredOverloads)
			{
				var ds = overload as DSymbol;
				if (ds != null && ds.Definition.TemplateConstraint != null)
				{
					ctxt.CurrentContext.IntroduceTemplateParameterTypes(ds);
					try
					{
						var v = Evaluation.EvaluateValue(ds.Definition.TemplateConstraint, ctxt);
						if (!Evaluation.IsFalsy(v))
							templateConstraintFilteredOverloads.Add(ds);
					}
					catch { } //TODO: Handle eval exceptions
					ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(ds);
				}
				else
					templateConstraintFilteredOverloads.Add(overload);
			}
			if (templateConstraintFilteredOverloads.Count == 0)
				return new List<AbstractType>();

			var implicitPropertiesOrEponymousTemplatesOrOther = new List<AbstractType>();
			foreach(var t in templateConstraintFilteredOverloads)
			{
				if (t is TemplateType)
				{
					var implicitProperties = TryGetImplicitProperty(t as TemplateType, ctxt);
					if (implicitProperties.Count > 0)
						implicitPropertiesOrEponymousTemplatesOrOther.AddRange(implicitProperties);
					else
						implicitPropertiesOrEponymousTemplatesOrOther.AddRange(templateConstraintFilteredOverloads);
				}
				else if (t is EponymousTemplateType)
				{
					var eponymousResolvee = DeduceEponymousTemplate(t as EponymousTemplateType, ctxt);
					if(eponymousResolvee != null)
						implicitPropertiesOrEponymousTemplatesOrOther.Add(eponymousResolvee);
				}
				else
					implicitPropertiesOrEponymousTemplatesOrOther.Add(t);
			}

			return implicitPropertiesOrEponymousTemplatesOrOther;
		}

		static List<AbstractType> TryGetImplicitProperty(TemplateType template, ResolutionContext ctxt)
		{
			try
			{
				ctxt.CurrentContext.IntroduceTemplateParameterTypes(template);
				return TypeDeclarationResolver.ResolveFurtherTypeIdentifier(template.NameHash, template, ctxt, null, false);
			}
			finally
			{
				ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(template);
			}
		}

		static AbstractType DeduceEponymousTemplate(EponymousTemplateType ept, ResolutionContext ctxt)
		{
			if (ept.Definition.Initializer == null &&
				ept.Definition.Type == null) {
				ctxt.LogError(ept.Definition, "Can't deduce type from empty initializer!");
				return null;
			}

			// Introduce the deduced params to the current resolution context
			ctxt.CurrentContext.IntroduceTemplateParameterTypes(ept);

			// Get actual overloads
			AbstractType deducedType = null;
			var def = ept.Definition;
			deducedType = new MemberSymbol(def, def.Type != null ?
				TypeDeclarationResolver.ResolveSingle(def.Type, ctxt) :
				ExpressionTypeEvaluation.EvaluateType(def.Initializer, ctxt), ept.DeducedTypes); //ept; //ExpressionTypeEvaluation.EvaluateType (ept.Definition.Initializer, ctxt);

			deducedType.AssignTagsFrom(ept); // Currently requried for proper UFCS resolution - sustain ept's Tags

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

			foreach (var originalOverload in rawOverloadList)
			{
				var overload = originalOverload as DSymbol;
				while (overload is TemplateParameterSymbol)
					overload = overload.Base as DSymbol;

				if (overload == null)
				{
					if (!hasTemplateArgsPassed)
						filteredOverloads.Add(originalOverload);
					continue;
				}

				var tplNode = overload.Definition;

				// Generically, the node should never be null -- except for TemplateParameterNodes that encapsule such params
				if (tplNode == null)
				{
					filteredOverloads.Add(originalOverload);
					continue;
				}

				bool ignoreOtherOverloads;
				var hook = D_Parser.Resolver.ResolutionHooks.HookRegistry.TryDeduce(overload, givenTemplateArguments, out ignoreOtherOverloads);
				if (hook != null)
				{
					filteredOverloads.Add(hook);
					if (ignoreOtherOverloads)
						break;
					continue;
				}

				// If the type or method has got no template parameters and if there were no args passed, keep it - it's legit.
				if (tplNode.TemplateParameters == null)
				{
					if (!hasTemplateArgsPassed || isMethodCall)
						filteredOverloads.Add(originalOverload);
					continue;
				}

				var deducedTypes = new DeducedTypeDictionary(overload);

				if (DeduceParams(givenTemplateArguments, isMethodCall, ctxt, overload, tplNode, deducedTypes))
					filteredOverloads.Add(ResolvedTypeCloner.Clone(overload, deducedTypes));
			}

			return filteredOverloads;
		}

		internal static bool DeduceParams(IEnumerable<ISemantic> givenTemplateArguments,
			bool isMethodCall,
			ResolutionContext ctxt,
			DSymbol overload,
			DNode tplNode,
			DeducedTypeDictionary deducedTypes)
		{
			var argEnum = givenTemplateArguments.GetEnumerator();
			if(tplNode.TemplateParameters != null)
				foreach (var expectedParam in tplNode.TemplateParameters)
					if (!DeduceParam(ctxt, overload, deducedTypes, argEnum, expectedParam))
						return false;

			if (!isMethodCall && argEnum.MoveNext())
			{
				// There are too many arguments passed - discard this overload
				return false;
			}
			return true;
		}

		private static bool DeduceParam(ResolutionContext ctxt,
			DSymbol overload,
			DeducedTypeDictionary deducedTypes,
			IEnumerator<ISemantic> argEnum,
			TemplateParameter expectedParam)
		{
			if (expectedParam is TemplateThisParameter && overload != null && overload.Base != null)
			{
				var ttp = (TemplateThisParameter)expectedParam;

				// Get the type of the type of 'this' - so of the result that is the overload's base
				var t = DResolver.StripMemberSymbols(overload.Base);

				if (t == null)
					return false;

				//TODO: Still not sure if it's ok to pass a type result to it
				// - looking at things like typeof(T) that shall return e.g. const(A) instead of A only.

				if (!CheckAndDeduceTypeAgainstTplParameter(ttp, t, deducedTypes, ctxt))
					return false;

				return true;
			}

			if (argEnum.MoveNext())
			{
				// On tuples, take all following arguments and pass them to the check function
				if (expectedParam is TemplateTupleParameter)
				{
					var tupleItems = new List<ISemantic>();
					// A tuple must at least contain one item!
					tupleItems.Add(argEnum.Current);
					while (argEnum.MoveNext())
						tupleItems.Add(argEnum.Current);

					return CheckAndDeduceTypeTuple((TemplateTupleParameter)expectedParam, tupleItems, deducedTypes, ctxt);
				}
				else
					return DeduceOrInduceSingleTemplateParameter(expectedParam, argEnum.Current, deducedTypes, ctxt);
			}
			else if (HasDefaultType(expectedParam))
				return CheckAndDeduceTypeAgainstTplParameter(expectedParam, null, deducedTypes, ctxt);
			else if (expectedParam is TemplateTupleParameter)
				return CheckAndDeduceTypeTuple(expectedParam as TemplateTupleParameter, Enumerable.Empty<ISemantic>(), deducedTypes, ctxt);
			// There might be too few args - but that doesn't mean that it's not correct - it's only required that all parameters got satisfied with a type
			else return deducedTypes.AllParamatersSatisfied;
		}

		static bool DeduceOrInduceSingleTemplateParameter(TemplateParameter parameter, ISemantic argumentToAnalyze, DeducedTypeDictionary deducedTypes, ResolutionContext ctxt)
		{
			if (argumentToAnalyze == null)
				return false;

			var tps = argumentToAnalyze as TemplateParameterSymbol;
			if (tps != null && tps.Base == null && tps.Parameter.Parent is DClassLike)
			{
				// (Only) In the context of template parameter deduction
				var probablyInducableParameter = DSymbolBaseTypeResolver.ResolveTemplateParameter(ctxt, new TemplateParameter.Node(parameter));
				return CheckAndDeduceTypeAgainstTplParameter(tps.Parameter, probablyInducableParameter, deducedTypes, ctxt);
			}
			else return CheckAndDeduceTypeAgainstTplParameter(parameter, argumentToAnalyze, deducedTypes, ctxt);
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
			return new TemplateParameterDeduction(deducedTypes, ctxt).Handle(handledParameter, argumentToCheck);
		}

		static bool CheckAndDeduceTypeTuple(TemplateTupleParameter tupleParameter,
			IEnumerable<ISemantic> typeChain,
			DeducedTypeDictionary deducedTypes,
			ResolutionContext ctxt)
		{
			return new TemplateParameterDeduction(deducedTypes,ctxt).Handle(tupleParameter,new DTuple(typeChain));
		}
	}
}
