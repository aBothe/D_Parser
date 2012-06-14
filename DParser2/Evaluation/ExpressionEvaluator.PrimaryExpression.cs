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
using D_Parser.Evaluation.Exceptions;

namespace D_Parser.Evaluation
{
	public partial class ExpressionEvaluator
	{
		public ISymbolValue Evaluate(PrimaryExpression x)
		{
			int tt = 0;

			if (x is TemplateInstanceExpression)
			{
				//TODO
			}
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

				var arrayRes = new ArrayResult
				{
					DeclarationOrExpressionBase = ax,
					ResultBase = elements[0].RepresentedType
				};

				return new ArrayValue(arrayRes, elements.ToArray());
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

				var arr = ExpressionTypeResolver.Resolve(assx,
					new[] { elements[0].Key.RepresentedType },
					new[] { elements[0].Value.RepresentedType });

				return new AssociativeArrayValue(arr[0], x, elements);
			}
			else if (x is FunctionLiteral)
			{
				// return function/delegate value
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
				if(IsFalseZeroOrNull(assertVal))
				{
					string assertMsg= "";

					if(ase.AssignExpressions.Length > 1)
					{
						var assertMsg_v=Evaluate(ase.AssignExpressions[1]) as ArrayValue;

						if(assertMsg_v == null || !assertMsg_v.IsString)
							throw new InvalidStringException(ase.AssignExpressions[1]);

						assertMsg=assertMsg_v.StringValue;
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
				var v=Evaluate(imp.AssignExpression);
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

				return new ArrayValue(TryGetStringDefinition(LiteralSubformat.Utf8,x), x, text);
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

		public ISymbolValue Evaluate(IdentifierExpression id)
		{
			if (id.IsIdentifier)
			{
				return vp[(string)id.Value];
			}

			int tt = 0;
			switch (id.Format)
			{
				case Parser.LiteralFormat.CharLiteral:
					return new PrimitiveValue(DTokens.Char, id.Value, id);

				case LiteralFormat.FloatingPoint | LiteralFormat.Scalar:
					var im = id.Subformat.HasFlag(LiteralSubformat.Imaginary);

					tt = im ? DTokens.Idouble : DTokens.Double;

					if (id.Subformat.HasFlag(LiteralSubformat.Float))
						tt = im ? DTokens.Ifloat : DTokens.Float;
					else if (id.Subformat.HasFlag(LiteralSubformat.Real))
						tt = im ? DTokens.Ireal : DTokens.Real;

					return new PrimitiveValue(tt, id.Value, id);

				case Parser.LiteralFormat.Scalar:
					var unsigned = id.Subformat.HasFlag(LiteralSubformat.Unsigned);

					if (id.Subformat.HasFlag(LiteralSubformat.Long))
						tt = unsigned ? DTokens.Ulong : DTokens.Long;
					else
						tt = unsigned ? DTokens.Uint : DTokens.Int;

					return new PrimitiveValue(DTokens.Int, id.Value, id);

				case Parser.LiteralFormat.StringLiteral:
				case Parser.LiteralFormat.VerbatimStringLiteral:
					return new ArrayValue(TryGetStringDefinition(id), id);
			}
			return null;
		}

		ResolveResult TryGetStringDefinition(IdentifierExpression id)
		{
			return TryGetStringDefinition(id.Subformat, id);
		}

		ResolveResult TryGetStringDefinition(LiteralSubformat stringFmt, IExpression x)
		{
			if (vp.ResolutionContext != null)
			{
				var obj = vp.ResolutionContext.ParseCache.LookupModuleName("object").First();

				string strType = stringFmt == LiteralSubformat.Utf32 ? "dstring" :
					stringFmt == LiteralSubformat.Utf16 ? "wstring" :
					"string";

				var strDef = obj[strType];

				if(strDef!=null)
					return TypeDeclarationResolver.HandleNodeMatch(strDef, vp.ResolutionContext, null, x);
			}
			
			var ch = new DTokenDeclaration(stringFmt == LiteralSubformat.Utf32 ? DTokens.Dchar :
				stringFmt == LiteralSubformat.Utf16 ? DTokens.Wchar : DTokens.Char);

			var immutable = new MemberFunctionAttributeDecl(DTokens.Immutable)
			{
				InnerType = ch,
				Location = x.Location,
				EndLocation = x.EndLocation
			};

			return TypeDeclarationResolver.Resolve(new ArrayDecl { ValueType = immutable }, null)[0];
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
				var typeToCheck_ = DResolver.TryRemoveAliasesFromResult(TypeDeclarationResolver.Resolve(isExpression.TestedType, vp.ResolutionContext));

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

		private bool evalIsExpression_WithAliases(IsExpression isExpression, ResolveResult typeToCheck)
		{
			/*
			 * Note: It's needed to let the abstract ast scanner also scan through IsExpressions etc.
			 * in order to find aliases and/or specified template parameters!
			 */

			var tpl_params = new Dictionary<string, ResolveResult[]>();
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
					tpl_params[isExpression.TypeAliasIdentifier] = new[]{ r.Item2 };
				}
			}
			else // 5.
				retTrue = tpd.Handle(isExpression.ArtificialFirstSpecParam, typeToCheck);

			if (retTrue && isExpression.TemplateParameterList != null)
				foreach (var p in isExpression.TemplateParameterList)
					if (!tpd.Handle(p, tpl_params[p.Name] != null ? tpl_params[p.Name].First() : null))
						return false;

			//TODO: Put all tpl_params results into the resolver context or make a new scope or something! 

			return retTrue;
		}

		private bool evalIsExpression_NoAlias(IsExpression isExpression, ResolveResult typeToCheck)
		{
			if (isExpression.TypeSpecialization != null)
			{
				var spec = DResolver.TryRemoveAliasesFromResult(TypeDeclarationResolver.Resolve(isExpression.TypeSpecialization, vp.ResolutionContext));

				return spec != null && spec.Length != 0 && (isExpression.EqualityTest ? 
					ResultComparer.IsEqual(typeToCheck, spec[0]) : 
					ResultComparer.IsImplicitlyConvertible(typeToCheck, spec[0], vp.ResolutionContext));
			}

			return isExpression.EqualityTest && evalIsExpression_EvalSpecToken(isExpression, typeToCheck).Item1;
		}

		private Tuple<bool,ResolveResult> evalIsExpression_EvalSpecToken(IsExpression isExpression, ResolveResult typeToCheck, bool DoAliasHandling=false)
		{
			bool r = false;
			ResolveResult res = null;

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
					r = typeToCheck is TypeResult &&
						((TypeResult)typeToCheck).Node is DClassLike &&
						((DClassLike)((TypeResult)typeToCheck).Node).ClassType == isExpression.TypeSpecializationToken;

					if (r)
						res = typeToCheck;
					break;

				case DTokens.Enum:
					if(!(typeToCheck is TypeResult))
						break;

					var tr=(TypeResult)typeToCheck;
					if (r= tr.Node is DEnum && tr.BaseClass != null && tr.BaseClass.Length != 0)
						res =  tr.BaseClass[0];
					break;

				case DTokens.Function:
				case DTokens.Delegate:
					if (typeToCheck is DelegateResult)
					{
						var isFun = false;
						var dgr = (DelegateResult)typeToCheck;
						if (dgr.IsDelegateDeclaration)
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
					else
					{
						r= isExpression.TypeSpecializationToken == DTokens.Delegate &&
							typeToCheck is MemberResult &&
							((MemberResult)typeToCheck).Node is DMethod;

						//TODO: Alias handling, same as couple of lines above
					}
					break;

				case DTokens.Super: //TODO: Test this
					var dc = DResolver.SearchClassLikeAt(vp.ResolutionContext.ScopedBlock, isExpression.Location) as DClassLike;

					if (dc != null)
					{
						tr = new TypeResult { Node = dc };
						DResolver.ResolveBaseClasses(tr, vp.ResolutionContext, true);

						if (r = tr.BaseClass != null && tr.BaseClass.Length == 0 && ResultComparer.IsEqual(typeToCheck, tr.BaseClass[0]))
						{
							var l = new List<ResolveResult[]>();
							if (tr.BaseClass != null)
								l.Add(tr.BaseClass);
							if (tr.ImplementedInterfaces != null && tr.ImplementedInterfaces.Length != 0)
								l.AddRange(tr.ImplementedInterfaces);

							res = new TypeTupleResult { 
								TupleItems=l.ToArray(),
								DeclarationOrExpressionBase=isExpression,
							};
						}
					}
					break;

				case DTokens.Const:
				case DTokens.Immutable:
				case DTokens.InOut: // TODO?
				case DTokens.Shared:
					if (r = typeToCheck.DeclarationOrExpressionBase is MemberFunctionAttributeDecl &&
						((MemberFunctionAttributeDecl)typeToCheck.DeclarationOrExpressionBase).Modifier == isExpression.TypeSpecializationToken)
						res = typeToCheck;
					break;

				case DTokens.Return: // TODO: Test
					IStatement _u = null;
					var dm = DResolver.SearchBlockAt(vp.ResolutionContext.ScopedBlock, isExpression.Location, out _u) as DMethod;

					if (dm != null)
					{
						var retType_ = TypeDeclarationResolver.GetMethodReturnType(dm, vp.ResolutionContext);

						if(r = retType_ != null || retType_.Length != 0 && ResultComparer.IsEqual(typeToCheck, retType_[0]))
							res = retType_[0];
					}
					break;
			}

			return new Tuple<bool,ResolveResult>(r, res);
		}
		#endregion
	}
}
