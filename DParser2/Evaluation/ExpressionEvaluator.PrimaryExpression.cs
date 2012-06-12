using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;

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
						//TODO
						break;
					case DTokens.Super:
						break;
					case DTokens.Null:
						break;
					//return new PrimitiveValue(ExpressionValueType.Class, null, x);
					case DTokens.Dollar:
						//TODO
						break;
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

			}
			else if (x is AssertExpression)
			{

			}
			else if (x is MixinExpression)
			{

			}
			else if (x is ImportExpression)
			{

			}
			else if (x is TypeidExpression)
			{

			}
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
					ResolveResult _t = null;

					if (vp.ResolutionContext != null)
					{
						var obj = vp.ResolutionContext.ParseCache.LookupModuleName("object").First();

						string strType = id.Subformat == LiteralSubformat.Utf32 ? "dstring" :
							id.Subformat == LiteralSubformat.Utf16 ? "wstring" :
							"string";

						var strNode = obj[strType];

						if (strNode != null)
							_t = TypeDeclarationResolver.HandleNodeMatch(strNode, vp.ResolutionContext, null, id);
					}

					if (_t == null)
					{
						var ch = new DTokenDeclaration(id.Subformat == LiteralSubformat.Utf32 ? DTokens.Dchar :
							id.Subformat == LiteralSubformat.Utf16 ? DTokens.Wchar : DTokens.Char);

						var immutable = new MemberFunctionAttributeDecl(DTokens.Immutable)
						{
							InnerType = ch,
							Location = id.Location,
							EndLocation = id.EndLocation
						};

						_t = TypeDeclarationResolver.Resolve(new ArrayDecl { ValueType = immutable }, null)[0];
					}

					return new ArrayValue(_t, id);
			}
			return null;
		}

		/// <summary>
		/// http://dlang.org/expression.html#IsExpression
		/// </summary>
		public ISymbolValue Evaluate(IsExpression isExpression)
		{
			/*
			 * Note: It's needed to let the abstract ast scanner also scan through IsExpressions etc.
			 * in order to find aliases and/or specified template parameters!
			 */
			/*
			 * To handle semantic tokens like "return" or "super" it's just needed to 
			 * look into the current resolver context -
			 * then, we'll be able to gather either the parent method or the currently scoped class definition.
			 */

			// The probably most frequented usage of this expression
			if (string.IsNullOrEmpty(isExpression.TypeAliasIdentifier))
			{
				if (isExpression.TypeSpecialization == null && isExpression.TypeSpecializationToken == 0)
				{
					//TODO Find out in which cases expressions are allowed!
				}
			}
			return null;
		}
	}
}
