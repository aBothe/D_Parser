using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Parser;
using D_Parser.Dom;
using D_Parser.Evaluation;
using System.IO;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		public static ArrayType GetStringType(ResolverContextStack ctxt, LiteralSubformat fmt = LiteralSubformat.Utf8)
		{
			ArrayType _t = null;

			if (ctxt != null)
			{
				var obj = ctxt.ParseCache.LookupModuleName("object").First();

				string strType = fmt == LiteralSubformat.Utf32 ? "dstring" :
					fmt == LiteralSubformat.Utf16 ? "wstring" :
					"string";

				var strNode = obj[strType];

				if (strNode != null)
					_t = DResolver.StripAliasSymbol(TypeDeclarationResolver.HandleNodeMatch(strNode, ctxt, null, id)) as ArrayType;
			}

			if (_t == null)
			{
				var ch = fmt == LiteralSubformat.Utf32 ? DTokens.Dchar :
					fmt == LiteralSubformat.Utf16 ? DTokens.Wchar : DTokens.Char;

				_t = new ArrayType(new PrimitiveType(ch, DTokens.Immutable),
					new ArrayDecl
					{
						ValueType = new MemberFunctionAttributeDecl(DTokens.Immutable)
						{
							InnerType = new DTokenDeclaration(ch)
						}
					});
			}

			return _t;
		}

		ArrayType GetStringType(LiteralSubformat fmt = LiteralSubformat.Utf8)
		{
			return GetStringType(ctxt, fmt);
		}

		ISemantic E(PrimaryExpression ex)
		{
			if (ex is IdentifierExpression)
			{
				var id = ex as IdentifierExpression;
				int tt = 0;

				if (id.IsIdentifier)
					return TypeDeclarationResolver.ResolveSingle(id.Value as string, ctxt, id, id.ModuleScoped) as AbstractType;

				switch (id.Format)
				{
					case Parser.LiteralFormat.CharLiteral:
						var tk = id.Subformat == LiteralSubformat.Utf32 ? DTokens.Dchar :
							id.Subformat == LiteralSubformat.Utf16 ? DTokens.Wchar :
							DTokens.Char;

						if (eval)
							return new PrimitiveValue(tk, id.Value, ex);
						else
							return new PrimitiveType(tk, 0, id);

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

						var _t = GetStringType(id.Subformat);
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
				return E((TokenExpression)ex);

			else if (ex is ArrayLiteralExpression)
			{
				var arr = (ArrayLiteralExpression)ex;

				if (eval)
				{
					var elements = new List<ISymbolValue>(arr.Elements.Count);

					//ISSUE: Type-check each item to distinguish not matching items
					foreach (var e in arr.Elements)
						elements.Add(E(e) as ISymbolValue);

					return new ArrayValue(new ArrayType(elements[0].RepresentedType, arr), elements.ToArray());
				}

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
			{
				var fl = (FunctionLiteral)ex;

				var dg = new DelegateType(
					TypeDeclarationResolver.GetMethodReturnType(((FunctionLiteral)ex).AnonymousMethod, ctxt),
					(FunctionLiteral)ex);

				if (eval)
					return new DelegateValue(dg);
				else
					return dg;
			}

			else if (ex is AssertExpression)
				return E((AssertExpression)ex);

			else if (ex is MixinExpression)
				return E((MixinExpression)ex);

			else if (ex is ImportExpression)
				return E((ImportExpression)ex);

			else if (ex is TypeDeclarationExpression) // should be containing a typeof() only; static properties etc. are parsed as access expressions
				return TypeDeclarationResolver.ResolveSingle(((TypeDeclarationExpression)ex).Declaration, ctxt);

			else if (ex is TypeidExpression) //TODO: Split up into more detailed typeinfo objects (e.g. for arrays, pointers, classes etc.)
				return E((TypeidExpression)ex);

			else if (ex is IsExpression)
				return E((IsExpression)ex);

			else if (ex is TraitsExpression)
				return E((TraitsExpression)ex);

			return null;
		}

		ISemantic E(TokenExpression x)
		{
			var token = x.Token;

			// References current class scope
			if (token == DTokens.This)
			{
				var classDef = ctxt.ScopedBlock;

				while (!(classDef is DClassLike) && classDef != null)
					classDef = classDef.Parent as IBlockNode;

				if (classDef is DClassLike)
					return TypeDeclarationResolver.HandleNodeMatch(classDef, ctxt, null, x);
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
						tr.Base.DeclarationOrExpressionBase = x;

						return tr.Base;
					}
				}
			}
		}

		ISemantic E(AssertExpression x)
		{
			if (!eval)
				return new PrimitiveType(DTokens.Void,0,x);

			var assertVal = E(x.AssignExpressions[0]) as ISymbolValue;
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

				if (x.AssignExpressions.Length > 1)
				{
					var assertMsg_v = E(x.AssignExpressions[1]) as ArrayValue;

					if (assertMsg_v == null || !assertMsg_v.IsString)
						throw new InvalidStringException(x.AssignExpressions[1]);

					assertMsg = assertMsg_v.StringValue;
				}

				throw new AssertException(x, assertMsg);
			}

			return null;
		}

		ISemantic E(MixinExpression x)
		{
			// 1) Evaluate the mixin expression
			var cnst = resolveConstOnly;
			resolveConstOnly = true;
			var v = E(((MixinExpression)x).AssignExpression) as ArrayValue;
			resolveConstOnly = cnst;

			if (v == null || !v.IsString)
				throw new InvalidStringException(x);

			// 2) Parse it as an expression
			var ex = DParser.ParseAssignExpression(v.StringValue);

			if (ex == null)
				throw new EvaluationException(x, "Invalid expression code given");

			// 3) Evaluate the expression's type/value
			return E(ex);
		}

		ISemantic E(ImportExpression x)
		{
			var strType = GetStringType();

			if (eval)
			{
				var cnst = resolveConstOnly;
				resolveConstOnly = true;
				var v = E(((ImportExpression)x).AssignExpression) as ArrayValue;
				resolveConstOnly = cnst;

				if (v == null || !v.IsString)
					throw new InvalidStringException(x);

				var fn = Path.IsPathRooted(v.StringValue) ? v.StringValue :
							Path.Combine(Path.GetDirectoryName((ctxt.ScopedBlock.NodeRoot as IAbstractSyntaxTree).FileName), 
							v.StringValue);

				if (!File.Exists(fn))
					throw new EvaluationException(x, "Could not find \"" + fn + "\"");

				var text = File.ReadAllText(fn);

				return new ArrayValue(GetStringType(), x, text);
			}
			else
				return strType;
		}

		ISemantic E(AssocArrayExpression aa)
		{
			if (eval)
			{
				var elements = new List<KeyValuePair<ISymbolValue, ISymbolValue>>();

				foreach (var e in aa.Elements)
				{
					var keyVal = E(e.Key) as ISymbolValue;
					var valVal = E(e.Value) as ISymbolValue;

					elements.Add(new KeyValuePair<ISymbolValue, ISymbolValue>(keyVal, valVal));
				}

				return new AssociativeArrayValue(new AssocArrayType(elements[0].Value.RepresentedType, elements[0].Key.RepresentedType, aa), aa, elements);
			}

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
