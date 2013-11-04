using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		public static ArrayType GetStringType(ResolutionContext ctxt, LiteralSubformat fmt = LiteralSubformat.Utf8)
		{
			ArrayType _t = null;

			if (ctxt != null)
			{
				var obj = ctxt.ParseCache.LookupModuleName("object").FirstOrDefault();

				if (obj != null)
				{
					string strType = fmt == LiteralSubformat.Utf32 ? "dstring" :
						fmt == LiteralSubformat.Utf16 ? "wstring" :
						"string";

					var strNode = obj[strType];

					if (strNode != null)
						foreach (var n in strNode) {
							_t = DResolver.StripAliasSymbol(TypeDeclarationResolver.HandleNodeMatch(n, ctxt)) as ArrayType;
							if (_t != null)
								break;
						}
				}
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

		ISemantic E(PrimaryExpression x)
		{
			if (x is IdentifierExpression)
				return E((IdentifierExpression)x);

			else if (x is TemplateInstanceExpression)
				return E((TemplateInstanceExpression)x);

			else if (x is TokenExpression)
				return E((TokenExpression)x);

			else if (x is ArrayLiteralExpression)
				return E((ArrayLiteralExpression)x);

			else if (x is AssocArrayExpression)
				return E((AssocArrayExpression)x);

			else if (x is FunctionLiteral)
				return E((FunctionLiteral)x);

			else if (x is AssertExpression)
				return E((AssertExpression)x);

			else if (x is MixinExpression)
				return E((MixinExpression)x);

			else if (x is ImportExpression)
				return E((ImportExpression)x);

			else if (x is TypeDeclarationExpression) // should be containing a typeof() only; static properties etc. are parsed as access expressions
				return E((TypeDeclarationExpression)x);

			else if (x is TypeidExpression)
				return E((TypeidExpression)x);

			else if (x is IsExpression)
				return E((IsExpression)x);

			else if (x is TraitsExpression)
				return E((TraitsExpression)x);

			return null;
		}

		ISemantic E(TokenExpression x)
		{
			switch (x.Token)
			{
				// References current class scope
				case DTokens.This:
					if (eval && resolveConstOnly) 
					{
						EvalError(new NoConstException(x));
							return null;
					}

					var classDef = ctxt.ScopedBlock;

					while (!(classDef is DClassLike) && classDef != null)
						classDef = classDef.Parent as IBlockNode;

					if (classDef is DClassLike)
						return TypeDeclarationResolver.HandleNodeMatch(classDef, ctxt, null, x);

					/*
					 * TODO: Return an object reference to the 'this' object.
					 */
					break;


				case DTokens.Super:
					// References super type of currently scoped class declaration

					if (eval && resolveConstOnly) 
					{
						EvalError(new NoConstException(x));
						return null;
					}

					classDef = ctxt.ScopedBlock;

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

					/*
					 * TODO: Return an object reference to 'this', wheras the type is the superior type.
					 */
					break;

				case DTokens.Null:
					if (eval && resolveConstOnly)
					{
						EvalError(new NoConstException(x));
						return null;
					}

					if (eval)
					{
						//TODO
					}

					return null;

				case DTokens.Dollar:
					if (!eval)
						return new PrimitiveType (DTokens.Int);
					// It's only allowed if the evaluation stack contains an array value
					if (ValueProvider.CurrentArrayLength != -1)
						return new PrimitiveValue(DTokens.Int, ValueProvider.CurrentArrayLength, x);
					else
					{
						EvalError(x, "Dollar not allowed here!");
						return null;
					}

				case DTokens.True:
					if (!eval)
						return new PrimitiveType (DTokens.Bool);
					return new PrimitiveValue(DTokens.Bool, 1, x);
				case DTokens.False:
					if (!eval)
						return new PrimitiveType (DTokens.Bool);
					return new PrimitiveValue(DTokens.Bool, 0, x);
				case DTokens.__FILE__:
					if (!eval)
						return GetStringType();
					return new ArrayValue(GetStringType(), (ctxt.ScopedBlock.NodeRoot as DModule).FileName);
				case DTokens.__LINE__:
					if (!eval)
						return new PrimitiveType (DTokens.Int);
					return new PrimitiveValue(DTokens.Int, x.Location.Line, x);
				case DTokens.__MODULE__:
					if (!eval)
						return GetStringType();
					return new ArrayValue(GetStringType(), (ctxt.ScopedBlock.NodeRoot as DModule).ModuleName);
				case DTokens.__FUNCTION__:
					//TODO
				case DTokens.__PRETTY_FUNCTION__:
					if (!eval)
						return GetStringType();
					var dm = ctxt.ScopedStatement.ParentNode as DMethod;
					return new ArrayValue(GetStringType(), dm == null ? "<not inside function>" : dm.ToString(false,true));
			}


			return null;
		}

		ISemantic E(AssertExpression x)
		{
			if (!eval)
				return new PrimitiveType(DTokens.Void, 0, x);

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
					{
						EvalError(new InvalidStringException(x.AssignExpressions[1]));
						return null;
					}

					assertMsg = assertMsg_v.StringValue;
				}

				EvalError(new AssertException(x, assertMsg));
				return null;
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

			if (v == null || !v.IsString){
				EvalError( new InvalidStringException(x));
				return null;
			}

			// 2) Parse it as an expression
			var ex = DParser.ParseAssignExpression(v.StringValue);

			if (ex == null){
				EvalError( new EvaluationException(x, "Invalid expression code given"));
				return null;
			}
			//TODO: Excessive caching
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

				if (v == null || !v.IsString){
					EvalError( new InvalidStringException(x));
					return null;
				}

				var fn = Path.IsPathRooted(v.StringValue) ? v.StringValue :
							Path.Combine(Path.GetDirectoryName((ctxt.ScopedBlock.NodeRoot as DModule).FileName),
							v.StringValue);

				if (!File.Exists(fn)){
					EvalError(x, "Could not find \"" + fn + "\"");
					return null;
				}

				var text = File.ReadAllText(fn);

				return new ArrayValue(GetStringType(), text);
			}
			else
				return strType;
		}

		ISemantic E(ArrayLiteralExpression arr)
		{
			if (eval)
			{
				var elements = new List<ISymbolValue>(arr.Elements.Count);

				//ISSUE: Type-check each item to distinguish not matching items
				foreach (var e in arr.Elements)
					elements.Add(E(e) as ISymbolValue);

				if(elements.Count == 0){
					EvalError(arr, "Array literal must contain at least one element.");
					return null;
				}

				return new ArrayValue(new ArrayType(elements[0].RepresentedType, arr), elements.ToArray());
			}

			if (arr.Elements != null && arr.Elements.Count > 0)
			{
				// Simply resolve the first element's type and take it as the array's value type
				var valueType = AbstractType.Get(E(arr.Elements[0]));

				return new ArrayType(valueType, arr);
			}

			ctxt.LogError(arr, "Array literal must contain at least one element.");
			return null;
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

				return new AssociativeArrayValue(new AssocArrayType(elements[0].Value.RepresentedType, elements[0].Key.RepresentedType, aa), elements);
			}

			if (aa.Elements != null && aa.Elements.Count > 0)
			{
				var firstElement = aa.Elements[0].Key;
				var firstElementValue = aa.Elements[0].Value;

				var keyType = AbstractType.Get(E(firstElement));
				var valueType = AbstractType.Get(E(firstElementValue));

				return new AssocArrayType(valueType, keyType, aa);
			}

			return null;
		}

		ISemantic E(FunctionLiteral x)
		{
			var dg = new DelegateType(
				(ctxt.Options & ResolutionOptions.DontResolveBaseTypes | ResolutionOptions.ReturnMethodReferencesOnly) != 0 ? null : TypeDeclarationResolver.GetMethodReturnType (x.AnonymousMethod, ctxt),
				x,
				TypeResolution.TypeDeclarationResolver.HandleNodeMatches(x.AnonymousMethod.Parameters, ctxt));

			if (eval)
				return new DelegateValue(dg);
			else
				return dg;
		}

		ISemantic E(TypeDeclarationExpression x)
		{
			var t = TypeDeclarationResolver.ResolveSingle(x.Declaration, ctxt);
			
			if (eval)
				return new TypeValue(t);
			return t;
		}
	}
}
