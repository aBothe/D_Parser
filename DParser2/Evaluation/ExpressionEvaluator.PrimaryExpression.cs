using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.Templates;
using System.IO;

namespace D_Parser.Evaluation
{
	public partial class ExpressionEvaluator
	{
		public ISymbolValue Evaluate(PrimaryExpression x)
		{
			if (x is TemplateInstanceExpression)
				return EvalId(x);
			else if (x is IdentifierExpression)
				return Evaluate((IdentifierExpression)x);
			else if (x is TokenExpression)
			{
				var tkx = (TokenExpression)x;

				switch (tkx.Token)
				{
					case DTokens.This:
						if (Const) throw new NoConstException(x);
						//TODO; Non-constant!
						break;
					case DTokens.Super:
						if (Const) throw new NoConstException(x);
						//TODO; Non-const!
						break;
					case DTokens.Null:
						if (Const) throw new NoConstException(x);
						//TODO; Non-const!
						break;
					//return new PrimitiveValue(ExpressionValueType.Class, null, x);
					case DTokens.Dollar:
						// It's only allowed if the evaluation stack contains an array value
						if (vp.CurrentArrayLength != -1)
							return new PrimitiveValue(DTokens.Int, vp.CurrentArrayLength, x);

						throw new EvaluationException(x, "Dollar not allowed here!");

					case DTokens.True:
						return new PrimitiveValue(DTokens.Bool, true, x);
					case DTokens.False:
						return new PrimitiveValue(DTokens.Bool, false, x);
					case DTokens.__FILE__:
						break;
					/*return new PrimitiveValue(ExpressionValueType.String, 
						ctxt==null?"":((IAbstractSyntaxTree)ctxt.ScopedBlock.NodeRoot).FileName,x);*/
					case DTokens.__LINE__:
						return new PrimitiveValue(DTokens.Int, x.Location.Line, x);
				}
			}
			else if (x is TypeDeclarationExpression)
			{
				//TODO: Handle static properties like .length, .sizeof etc.
			}
			else if (x is ArrayLiteralExpression)
			{
				var ax = (ArrayLiteralExpression)x;

				var elements = new List<ISymbolValue>(ax.Elements.Count);

				foreach (var e in ax.Elements)
					elements.Add(Evaluate(e));

				return new ArrayValue(new ArrayType(elements[0].RepresentedType, ax), elements.ToArray());
			}
			else if (x is AssocArrayExpression)
			{
				var assx = (AssocArrayExpression)x;

				var elements = new List<KeyValuePair<ISymbolValue, ISymbolValue>>();

				foreach (var e in assx.Elements)
				{
					var keyVal = Evaluate(e.Key);
					var valVal = Evaluate(e.Value);

					elements.Add(new KeyValuePair<ISymbolValue, ISymbolValue>(keyVal, valVal));
				}

				return new AssociativeArrayValue(new AssocArrayType(elements[0].Value.RepresentedType, elements[0].Key.RepresentedType, assx), x, elements);
			}
			else if (x is FunctionLiteral)
			{
				var fl = (FunctionLiteral)x;

				var r = Evaluation.Resolve(fl, vp.ResolutionContext);

				if (!(r is DelegateType))
					throw new EvaluationException(x, "Wrong result type", r);

				return new DelegateValue((DelegateType)r);
			}
			else if (x is AssertExpression)
			{
				var ase = (AssertExpression)x;

				var assertVal = Evaluate(ase.AssignExpressions[0]);
				/*TODO
				// If it evaluates to a non-null class reference, the class invariant is run. 
				if(assertVal is ClassInstanceValue)
				{
				}

				// Otherwise, if it evaluates to a non-null pointer to a struct, the struct invariant is run.
				*/

				// Otherwise, if the result is false, an AssertError is thrown
				if (IsFalseZeroOrNull(assertVal))
				{
					string assertMsg = "";

					if (ase.AssignExpressions.Length > 1)
					{
						var assertMsg_v = Evaluate(ase.AssignExpressions[1]) as ArrayValue;

						if (assertMsg_v == null || !assertMsg_v.IsString)
							throw new InvalidStringException(ase.AssignExpressions[1]);

						assertMsg = assertMsg_v.StringValue;
					}

					throw new AssertException(ase, assertMsg);
				}

				return null;
			}
			else if (x is MixinExpression)
			{
				var mx = (MixinExpression)x;

				var cnst = Const;
				Const = true;
				var v = Evaluate(mx.AssignExpression);
				Const = cnst;
				var av = v as ArrayValue;

				if (av == null || !av.IsString)
					throw new InvalidStringException(x);

				var ex = DParser.ParseAssignExpression(av.StringValue);

				if (ex == null)
					throw new EvaluationException(x, "Invalid expression code given");

				return Evaluate(ex);
			}
			else if (x is ImportExpression)
			{
				var imp = (ImportExpression)x;

				var cnst = Const;
				Const = true;
				var v = Evaluate(imp.AssignExpression);
				Const = cnst;
				var av = v as ArrayValue;

				if (av == null || !av.IsString)
					throw new InvalidStringException(x);

				var fn = Path.IsPathRooted(av.StringValue) ?
							av.StringValue :
							Path.Combine(Path.GetDirectoryName((vp.ResolutionContext.ScopedBlock.NodeRoot as IAbstractSyntaxTree).FileName), av.StringValue);

				if (!File.Exists(fn))
					throw new EvaluationException(x, "Could not find \"" + fn + "\"");

				var text = File.ReadAllText(fn);

				return new ArrayValue(TryGetStringDefinition(LiteralSubformat.Utf8, x), x, text);
			}
			else if (x is TypeidExpression)
				return Evaluate((TypeidExpression)x);
			else if (x is IsExpression)
				return Evaluate((IsExpression)x);
			else if (x is TraitsExpression)
				return Evaluate((TraitsExpression)x);
			else if (x is SurroundingParenthesesExpression)
				return Evaluate(((SurroundingParenthesesExpression)x).Expression);

			return null;
		}

		#region IsExpression
		/// <summary>
		/// http://dlang.org/expression.html#IsExpression
		/// </summary>
		public ISymbolValue Evaluate(IsExpression isExpression)
		{
			bool retTrue = false;

			if (isExpression.TestedType != null)
			{
				var typeToCheck_ = DResolver.StripAliasSymbols(TypeDeclarationResolver.Resolve(isExpression.TestedType, vp.ResolutionContext));

				if (typeToCheck_ != null && typeToCheck_.Length != 0)
				{
					var typeToCheck = typeToCheck_[0];

					// case 1, 4
					if (isExpression.TypeSpecialization == null && isExpression.TypeSpecializationToken == 0)
						retTrue = typeToCheck != null;

					// The probably most frequented usage of this expression
					else if (string.IsNullOrEmpty(isExpression.TypeAliasIdentifier))
						retTrue= evalIsExpression_NoAlias(isExpression, typeToCheck);
					else
						retTrue= evalIsExpression_WithAliases(isExpression, typeToCheck);
				}
			}

			return new PrimitiveValue(DTokens.Bool, retTrue, isExpression);
		}

		private bool evalIsExpression_WithAliases(IsExpression isExpression, AbstractType typeToCheck)
		{
			/*
			 * Note: It's needed to let the abstract ast scanner also scan through IsExpressions etc.
			 * in order to find aliases and/or specified template parameters!
			 */

			var tpl_params = new Dictionary<string, ISemantic>();
			tpl_params[isExpression.TypeAliasIdentifier] = null;
			if (isExpression.TemplateParameterList != null)
				foreach (var p in isExpression.TemplateParameterList)
					tpl_params[p.Name] = null;

			var tpd = new TemplateParameterDeduction(tpl_params, vp.ResolutionContext);
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
					tpl_params[isExpression.TypeAliasIdentifier] = r.Item2;
				}
			}
			else // 5.
				retTrue = tpd.Handle(isExpression.ArtificialFirstSpecParam, typeToCheck);

			if (retTrue && isExpression.TemplateParameterList != null)
				foreach (var p in isExpression.TemplateParameterList)
					if (!tpd.Handle(p, tpl_params[p.Name] != null ? tpl_params[p.Name] : null))
						return false;

			//TODO: Put all tpl_params results into the resolver context or make a new scope or something! 

			return retTrue;
		}

		private bool evalIsExpression_NoAlias(IsExpression isExpression, AbstractType typeToCheck)
		{
			if (isExpression.TypeSpecialization != null)
			{
				var spec = DResolver.StripAliasSymbols(TypeDeclarationResolver.Resolve(isExpression.TypeSpecialization, vp.ResolutionContext));

				return spec != null && spec.Length != 0 && (isExpression.EqualityTest ? 
					ResultComparer.IsEqual(typeToCheck, spec[0]) : 
					ResultComparer.IsImplicitlyConvertible(typeToCheck, spec[0], vp.ResolutionContext));
			}

			return isExpression.EqualityTest && evalIsExpression_EvalSpecToken(isExpression, typeToCheck, false).Item1;
		}

		/// <summary>
		/// Item1 - True, if isExpression returns true
		/// Item2 - If Item1 is true, it contains the type of the alias that is defined in the isExpression 
		/// </summary>
		private Tuple<bool,AbstractType> evalIsExpression_EvalSpecToken(IsExpression isExpression, AbstractType typeToCheck, bool DoAliasHandling=false)
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
					if(r = typeToCheck is UserDefinedType &&
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
							r = isExpression.TypeSpecializationToken == (
								(isFun = ((DelegateDeclaration)dgr.DeclarationOrExpressionBase).IsFunction) ? DTokens.Function : DTokens.Delegate);
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
						r= isExpression.TypeSpecializationToken == DTokens.Delegate &&
							typeToCheck is MemberSymbol &&
							((DSymbol)typeToCheck).Definition is DMethod;

						//TODO: Alias handling, same as couple of lines above
					}
					break;

				case DTokens.Super: //TODO: Test this
					var dc = DResolver.SearchClassLikeAt(vp.ResolutionContext.ScopedBlock, isExpression.Location) as DClassLike;

					if (dc != null)
					{
						var udt = DResolver.ResolveBaseClasses(new ClassType(dc, dc, null), vp.ResolutionContext, true) as ClassType;
						
						if (r = udt.Base != null && ResultComparer.IsEqual(typeToCheck, udt.Base))
						{
							var l = new List<AbstractType>();
							if (udt.Base != null)
								l.Add(udt.Base);
							if (udt.BaseInterfaces != null && udt.BaseInterfaces.Length!=0)
								l.AddRange(udt.BaseInterfaces);

							res = new TypeTuple(isExpression, l);
						}
					}
					break;

				case DTokens.Const:
				case DTokens.Immutable:
				case DTokens.InOut: // TODO?
				case DTokens.Shared:
					if (r = typeToCheck.Modifier == isExpression.TypeSpecializationToken)
						res = typeToCheck;
					break;

				case DTokens.Return: // TODO: Test
					IStatement _u = null;
					var dm = DResolver.SearchBlockAt(vp.ResolutionContext.ScopedBlock, isExpression.Location, out _u) as DMethod;

					if (dm != null)
					{
						var retType_ = TypeDeclarationResolver.GetMethodReturnType(dm, vp.ResolutionContext);

						if(r = retType_ != null && ResultComparer.IsEqual(typeToCheck, retType_))
							res = retType_;
					}
					break;
			}

			return new Tuple<bool,AbstractType>(r, res);
		}
		#endregion
	}
}
