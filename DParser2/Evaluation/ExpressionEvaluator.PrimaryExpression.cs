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
			{
				var id = (IdentifierExpression)x;

				if (id.IsIdentifier)
				{
					//TODO
				}

				switch (id.Format)
				{
					case Parser.LiteralFormat.CharLiteral:
						return new PrimitiveValue(DTokens.Char, id.Value, x);

					case LiteralFormat.FloatingPoint | LiteralFormat.Scalar:
						var im = id.Subformat.HasFlag(LiteralSubformat.Imaginary);
						
						tt = im ? DTokens.Idouble : DTokens.Double;

						if (id.Subformat.HasFlag(LiteralSubformat.Float))
							tt = im ? DTokens.Ifloat : DTokens.Float;
						else if (id.Subformat.HasFlag(LiteralSubformat.Real))
							tt = im ? DTokens.Ireal : DTokens.Real;

						return new PrimitiveValue(tt, id.Value, x);

					case Parser.LiteralFormat.Scalar:
						var unsigned = id.Subformat.HasFlag(LiteralSubformat.Unsigned);

						if (id.Subformat.HasFlag(LiteralSubformat.Long))
							tt = unsigned ? DTokens.Ulong : DTokens.Long;
						else
							tt = unsigned ? DTokens.Uint : DTokens.Int;

						return new PrimitiveValue(DTokens.Int, id.Value, x);

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
						
						if(_t==null)
						{
							var ch=new DTokenDeclaration(id.Subformat == LiteralSubformat.Utf32 ? DTokens.Dchar :
								id.Subformat == LiteralSubformat.Utf16 ? DTokens.Wchar : DTokens.Char);

							var immutable = new MemberFunctionAttributeDecl(DTokens.Immutable) {
								InnerType=ch,
								Location=id.Location,
								EndLocation= id.EndLocation
							};

							_t=TypeDeclarationResolver.Resolve(new ArrayDecl { ValueType = immutable }, null)[0];
						}

						return new ArrayValue(_t, id);
				}
			}
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

			}
			else if (x is ArrayLiteralExpression)
			{

			}
			else if (x is AssocArrayExpression)
			{

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
			{

			}
			else if (x is TraitsExpression)
			{

			}
			else if (x is SurroundingParenthesesExpression)
				return Evaluate(((SurroundingParenthesesExpression)x).Expression);

			return null;
		}
	}
}
