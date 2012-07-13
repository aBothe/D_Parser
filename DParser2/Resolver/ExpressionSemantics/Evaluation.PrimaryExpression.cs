using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Parser;
using D_Parser.Dom;
using D_Parser.Evaluation;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		ISemantic E(PrimaryExpression ex)
		{
			AbstractType type=null;

			if (ex is IdentifierExpression)
			{
				var id = ex as IdentifierExpression;
				int tt = 0;

				if (id.IsIdentifier)
					return TypeDeclarationResolver.ResolveSingle(id.Value as string, ctxt, id, id.ModuleScoped) as AbstractType;

				switch (id.Format)
				{
					case Parser.LiteralFormat.CharLiteral:
						var tk=id.Subformat == LiteralSubformat.Utf32 ? DTokens.Dchar :
							id.Subformat == LiteralSubformat.Utf16 ? DTokens.Wchar:
							DTokens.Char;

						if(eval)
							return new PrimitiveValue(tk, id.Value, ex);
						else
							return new PrimitiveType(tk,0,id);

					case LiteralFormat.FloatingPoint | LiteralFormat.Scalar:
						var im = id.Subformat.HasFlag(LiteralSubformat.Imaginary);

						tt = im ? DTokens.Idouble : DTokens.Double;

						if (id.Subformat.HasFlag(LiteralSubformat.Float))
							tt = im ? DTokens.Ifloat : DTokens.Float;
						else if (id.Subformat.HasFlag(LiteralSubformat.Real))
							tt = im ? DTokens.Ireal : DTokens.Real;

						if (eval)
							return new PrimitiveValue(tt, id.Value, ex);
						else
							return new PrimitiveType(tt, 0, id);

					case LiteralFormat.Scalar:
						var unsigned = id.Subformat.HasFlag(LiteralSubformat.Unsigned);

						if (id.Subformat.HasFlag(LiteralSubformat.Long))
							tt = unsigned ? DTokens.Ulong : DTokens.Long;
						else
							tt = unsigned ? DTokens.Uint : DTokens.Int;

						return eval ? (ISemantic)new PrimitiveValue(tt, id.Value, id) : new PrimitiveType(tt, 0, id);

					case Parser.LiteralFormat.StringLiteral:
					case Parser.LiteralFormat.VerbatimStringLiteral:
						ArrayType _t = null;

						if (ctxt != null)
						{
							var obj = ctxt.ParseCache.LookupModuleName("object").First();

							string strType = id.Subformat == LiteralSubformat.Utf32 ? "dstring" :
								id.Subformat == LiteralSubformat.Utf16 ? "wstring" :
								"string";

							var strNode = obj[strType];

							if (strNode != null)
								_t = DResolver.StripAliasSymbol(TypeDeclarationResolver.HandleNodeMatch(strNode, ctxt, null, id)) as ArrayType;
						}

						if (_t == null)
						{
							var ch = id.Subformat == LiteralSubformat.Utf32 ? DTokens.Dchar :
								id.Subformat == LiteralSubformat.Utf16 ? DTokens.Wchar : DTokens.Char;

							_t = new ArrayType(new PrimitiveType(ch, DTokens.Immutable),
								new ArrayDecl
								{
									ValueType = new MemberFunctionAttributeDecl(DTokens.Immutable)
									{
										InnerType = new DTokenDeclaration(ch)
									}
								});
						}

						return eval ? (ISemantic)new ArrayValue(_t, id) : _t;
				}
			}

			else if (ex is TemplateInstanceExpression)
			{
				var res = E((TemplateInstanceExpression)ex);

				ctxt.CheckForSingleResult(res, ex);
				return res != null && res.Length == 0 ? res[0] : null;
			}

			else if (ex is TokenExpression)
			{
				var token = (ex as TokenExpression).Token;

				// References current class scope
				if (token == DTokens.This)
				{
					var classDef = ctxt.ScopedBlock;

					while (!(classDef is DClassLike) && classDef != null)
						classDef = classDef.Parent as IBlockNode;

					if (classDef is DClassLike)
						return TypeDeclarationResolver.HandleNodeMatch(classDef, ctxt, null, ex);
				}
				// References super type of currently scoped class declaration
				else if (token == DTokens.Super)
				{
					var classDef = ctxt.ScopedBlock;

					while (!(classDef is DClassLike) && classDef != null)
						classDef = classDef.Parent as IBlockNode;

					if (classDef != null)
					{
						var tr = DResolver.ResolveBaseClasses(new ClassType(classDef as DClassLike, null, null), ctxt, true);

						if (tr.Base != null)
						{
							// Important: Overwrite type decl base with 'super' token
							tr.Base.DeclarationOrExpressionBase = ex;

							return tr.Base;
						}
					}
				}
			}

			else if (ex is ArrayLiteralExpression)
			{
				var arr = (ArrayLiteralExpression)ex;

				if (arr.Elements != null && arr.Elements.Count > 0)
				{
					// Simply resolve the first element's type and take it as the array's value type
					var valueType = E(arr.Elements[0]) as AbstractType;

					return new ArrayType(valueType, ex);
				}
			}

			else if (ex is AssocArrayExpression)
				return E((AssocArrayExpression)ex);

			else if (ex is FunctionLiteral)
				return new DelegateType(
					TypeDeclarationResolver.GetMethodReturnType(((FunctionLiteral)ex).AnonymousMethod, ctxt),
					(FunctionLiteral)ex);

			else if (ex is AssertExpression)
				return new PrimitiveType(DTokens.Void);

			else if (ex is MixinExpression)
			{
				/*
				 * 1) Evaluate the mixin expression
				 * 2) Parse it as an expression
				 * 3) Evaluate the expression's type
				 */
				//TODO
			}

			else if (ex is ImportExpression)
				return StaticPropertyResolver.getStringType(ctxt);

			else if (ex is TypeDeclarationExpression) // should be containing a typeof() only; static properties etc. are parsed as access expressions
				return TypeDeclarationResolver.ResolveSingle(((TypeDeclarationExpression)ex).Declaration, ctxt);

			else if (ex is TypeidExpression) //TODO: Split up into more detailed typeinfo objects (e.g. for arrays, pointers, classes etc.)
				return TypeDeclarationResolver.ResolveSingle(new IdentifierDeclaration("TypeInfo") { InnerDeclaration = new IdentifierDeclaration("object") }, ctxt) as AbstractType;

			else if (ex is IsExpression)
				return new PrimitiveType(DTokens.Bool);

			else if (ex is TraitsExpression)
				return E((TraitsExpression)ex);

			return null;
		}

		ISemantic E(AssocArrayExpression aa)
		{
			if (aa.Elements != null && aa.Elements.Count > 0)
			{
				var firstElement = aa.Elements[0].Key;
				var firstElementValue = aa.Elements[0].Value;

				var keyType = E(firstElement) as AbstractType;
				var valueType = E(firstElementValue) as AbstractType;

				return new AssocArrayType(valueType, keyType, aa);
			}
			return null;
		}

		ISemantic[] E(IdentifierExpression id)
		{
			return TypeDeclarationResolver.ResolveIdentifier(id.Value as string, ctxt, id, id.ModuleScoped);
		}
	}
}
