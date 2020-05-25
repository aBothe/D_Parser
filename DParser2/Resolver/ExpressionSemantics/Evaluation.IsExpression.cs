﻿using System;
using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Parser;
using D_Parser.Resolver.Templates;
using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		/// <summary>
		/// http://dlang.org/expression.html#IsExpression
		/// </summary>
		public ISymbolValue Visit(IsExpression isExpression)
		{
			bool retTrue = false;

			if (isExpression.TestedType != null)
			{
				var typeToCheck = DResolver.StripMemberSymbols(TypeDeclarationResolver.ResolveSingle(isExpression.TestedType, ctxt));

				if (typeToCheck != null)
				{
					// case 1, 4
					if (isExpression.TypeSpecialization == null && isExpression.TypeSpecializationToken == 0)
						retTrue = true;

					// The probably most frequented usage of this expression
					else if (isExpression.TypeAliasIdentifierHash == 0)
						retTrue = evalIsExpression_NoAlias(isExpression, typeToCheck);
					else
						retTrue = evalIsExpression_WithAliases(isExpression, typeToCheck);
				}
			}

			return new PrimitiveValue(retTrue);
		}

		private bool evalIsExpression_WithAliases(IsExpression isExpression, AbstractType typeToCheck)
		{
			/*
			 * Note: It's needed to let the abstract ast scanner also scan through IsExpressions etc.
			 * in order to find aliases and/or specified template parameters!
			 */

			var expectedTemplateParams = new TemplateParameter[isExpression.TemplateParameterList == null  ? 1 : (isExpression.TemplateParameterList.Length + 1)];
			expectedTemplateParams [0] = isExpression.ArtificialFirstSpecParam;
			if(expectedTemplateParams.Length > 1)
				isExpression.TemplateParameterList.CopyTo (expectedTemplateParams, 1);

			var tpl_params = new DeducedTypeDictionary(expectedTemplateParams);


			var tpd = new TemplateParameterDeduction(tpl_params, ctxt);
			bool retTrue = false;

			if (isExpression.EqualityTest) // 6.
			{
				// a)
				if (isExpression.TypeSpecialization != null)
				{
					tpd.EnforceTypeEqualityWhenDeducing = true;
					retTrue = tpd.Handle(isExpression.ArtificialFirstSpecParam, typeToCheck);
					tpd.EnforceTypeEqualityWhenDeducing = false;
				}
				else // b)
				{
					var r = evalIsExpression_EvalSpecToken(isExpression, typeToCheck, true);
					retTrue = r.Item1;
					tpl_params[isExpression.ArtificialFirstSpecParam] = new TemplateParameterSymbol(isExpression.ArtificialFirstSpecParam, r.Item2);
				}
			}
			else // 5.
				retTrue = tpd.Handle(isExpression.ArtificialFirstSpecParam, typeToCheck);

			if (retTrue && isExpression.TemplateParameterList != null)
				foreach (var p in isExpression.TemplateParameterList)
					if (!tpd.Handle(p, tpl_params[p] != null ? tpl_params[p].Base : null))
						return false;

			if (retTrue)
			{
				foreach (var kv in tpl_params)
					if (kv.Key != null && kv.Value != null)
						ctxt.CurrentContext.DeducedTemplateParameters[kv.Key] = kv.Value;
			}

			return retTrue;
		}

		private bool evalIsExpression_NoAlias(IsExpression isExpression, AbstractType typeToCheck)
		{
			if (isExpression.TypeSpecialization != null)
			{
				var spec = TypeDeclarationResolver.ResolveSingle(isExpression.TypeSpecialization, ctxt);

				return spec != null && (isExpression.EqualityTest ?
					ResultComparer.IsEqual(typeToCheck, spec) :
					ResultComparer.IsImplicitlyConvertible(typeToCheck, spec, ctxt));
			}

			return isExpression.EqualityTest && evalIsExpression_EvalSpecToken(isExpression, typeToCheck, false).Item1;
		}

		/// <summary>
		/// Item1 - True, if isExpression returns true
		/// Item2 - If Item1 is true, it contains the type of the alias that is defined in the isExpression 
		/// </summary>
		private Tuple<bool, AbstractType> evalIsExpression_EvalSpecToken(IsExpression isExpression, AbstractType typeToCheck, bool DoAliasHandling = false)
		{
			bool r = false;
			AbstractType res = null;

			switch (isExpression.TypeSpecializationToken)
			{
				/*
				 * To handle semantic tokens like "return" or "super" it's just needed to 
				 * look into the current resolver context -
				 * then, we'll be able to gather either the parent method or the currently scoped class definition.
				 */
				case DTokens.Struct:
				case DTokens.Union:
				case DTokens.Class:
				case DTokens.Interface:
					if (r = typeToCheck is TemplateIntermediateType &&
						((TemplateIntermediateType)typeToCheck).Definition.ClassType == isExpression.TypeSpecializationToken)
						res = typeToCheck;
					break;

				case DTokens.Enum:
					if (!(typeToCheck is EnumType))
						break;
					{
						var tr = (UserDefinedType)typeToCheck;
						r = true;
						res = tr.Base;
					}
					break;

				case DTokens.Function:
				case DTokens.Delegate:
					if (typeToCheck is DelegateType)
					{
						var isFun = false;
						var dgr = (DelegateType)typeToCheck;
						if (!dgr.IsFunctionLiteral)
							r = isExpression.TypeSpecializationToken == ((isFun = dgr.IsFunction) ? DTokens.Function : DTokens.Delegate);
						// Must be a delegate otherwise
						else
							isFun = !(r = isExpression.TypeSpecializationToken == DTokens.Delegate);

						if (r)
						{
							//TODO
							if (isFun)
							{
								// TypeTuple of the function parameter types. For C- and D-style variadic functions, only the non-variadic parameters are included. 
								// For typesafe variadic functions, the ... is ignored.
							}
							else
							{
								// the function type of the delegate
							}
						}
					}
					else // Normal functions are also accepted as delegates
					{
						r = isExpression.TypeSpecializationToken == DTokens.Delegate &&
							typeToCheck is MemberSymbol &&
							((DSymbol)typeToCheck).Definition is DMethod;

						//TODO: Alias handling, same as couple of lines above
					}
					break;

				case DTokens.Super: //TODO: Test this
					var dc = ASTSearchHelper.SearchClassLikeAt(ctxt.ScopedBlock, isExpression.Location) as DClassLike;

					if (dc != null)
					{
						var udt = ClassInterfaceResolver.ResolveClassOrInterface(dc, ctxt, null, true) as ClassType;

						if (r = udt?.Base != null && ResultComparer.IsEqual(typeToCheck, udt.Base))
						{
							var l = new List<AbstractType>();
							if (udt.Base != null)
								l.Add(udt.Base);
							if (udt.BaseInterfaces != null && udt.BaseInterfaces.Length != 0)
								l.AddRange(udt.BaseInterfaces);

							res = new DTuple(l);
						}
					}
					break;

				case DTokens.Const:
				case DTokens.Immutable:
				case DTokens.InOut: // TODO?
				case DTokens.Shared:
					if (r = typeToCheck.HasModifier(isExpression.TypeSpecializationToken))
						res = typeToCheck;
					break;

				case DTokens.Return: // TODO: Test
					var dm = ASTSearchHelper.SearchBlockAt(ctxt.ScopedBlock, isExpression.Location) as DMethod;

					if (dm != null)
					{
						var retType_ = DSymbolBaseTypeResolver.GetMethodReturnType(dm, ctxt);

						if (r = retType_ != null && ResultComparer.IsEqual(typeToCheck, retType_))
							res = retType_;
					}
					break;

				case DTokens.__vector:
					if (typeToCheck is DSymbol)
					{
						var vd = (typeToCheck as DSymbol).Definition.Type as VectorDeclaration;
						if (vd != null)
						{
							r = true;
							if (vd.IdDeclaration != null)
								res = TypeDeclarationResolver.ResolveSingle(vd.IdDeclaration, ctxt);
						}
					}
					break;

				case DTokens.Module:
				case DTokens.Package:
				case DTokens.__parameters: // TODO
					break;
			}

			return new Tuple<bool, AbstractType>(r, res);
		}
	}
}
