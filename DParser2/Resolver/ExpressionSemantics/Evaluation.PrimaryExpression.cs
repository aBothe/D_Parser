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
		public static int stringTypeHash = "string".GetHashCode();
		public static int wstringTypeHash = "wstring".GetHashCode();
		public static int dstringTypeHash = "dstring".GetHashCode();

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

		public ISemantic Visit(TokenExpression x)
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
						return new PrimitiveType(DTokens.Int);
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
						return new PrimitiveType(DTokens.Bool);
					return new PrimitiveValue(DTokens.Bool, 1, x);
				case DTokens.False:
					if (!eval)
						return new PrimitiveType(DTokens.Bool);
					return new PrimitiveValue(DTokens.Bool, 0, x);
				case DTokens.__FILE__:
					if (!eval)
						return GetStringType();
					return new ArrayValue(GetStringType(), (ctxt.ScopedBlock.NodeRoot as DModule).FileName);
				case DTokens.__LINE__:
					if (!eval)
						return new PrimitiveType(DTokens.Int);
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
					return new ArrayValue(GetStringType(), dm == null ? "<not inside function>" : dm.ToString(false, true));
			}


			return null;
		}

		public ISemantic Visit(AssertExpression x)
		{
			if (!eval)
				return new PrimitiveType(DTokens.Void, 0, x);

			var assertVal = x.AssignExpressions.Length > 0 && x.AssignExpressions[0] != null ? x.AssignExpressions[0].Accept(this) as ISymbolValue : null;
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

				if (x.AssignExpressions.Length > 1 && x.AssignExpressions[1] != null)
				{
					var assertMsg_v = x.AssignExpressions[1].Accept(this) as ArrayValue;

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

		public ISemantic Visit(MixinExpression x)
		{
			// 1) Evaluate the mixin expression
			var cnst = resolveConstOnly;
			resolveConstOnly = true;
			var v = x.AssignExpression != null ? x.AssignExpression.Accept(this) as ArrayValue : null;
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
			return ex.Accept(this);
		}

		public ISemantic Visit(ImportExpression x)
		{
			var strType = GetStringType();

			if (eval)
			{
				var cnst = resolveConstOnly;
				resolveConstOnly = true;
				var v = x.AssignExpression != null ? x.AssignExpression.Accept(this) as ArrayValue : null;
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

		public ISemantic Visit(ArrayLiteralExpression arr)
		{
			if (eval)
			{
				var elements = new List<ISymbolValue>(arr.Elements.Count);

				//ISSUE: Type-check each item to distinguish not matching items
				foreach (var e in arr.Elements)
					elements.Add(e != null ? e.Accept(this) as ISymbolValue : null);

				if(elements.Count == 0){
					EvalError(arr, "Array literal must contain at least one element.");
					return null;
				}

				AbstractType baseType = null;
				foreach (var ev in elements)
					if (ev != null && (baseType = ev.RepresentedType) != null)
						break;

				return new ArrayValue(new ArrayType(baseType, arr), elements.ToArray());
			}

			if (arr.Elements != null && arr.Elements.Count > 0)
			{
				// Simply resolve the first element's type and take it as the array's value type
				var valueType = arr.Elements[0] != null ?  AbstractType.Get(arr.Elements[0].Accept(this)) : null;

				return new ArrayType(valueType, arr);
			}

			ctxt.LogError(arr, "Array literal must contain at least one element.");
			return null;
		}

		public ISemantic Visit(AssocArrayExpression aa)
		{
			if (eval)
			{
				var elements = new List<KeyValuePair<ISymbolValue, ISymbolValue>>();

				foreach (var e in aa.Elements)
				{
					var keyVal = e.Key != null ? e.Key.Accept(this) as ISymbolValue : null;
					var valVal = e.Value != null ? e.Value.Accept(this) as ISymbolValue : null;

					elements.Add(new KeyValuePair<ISymbolValue, ISymbolValue>(keyVal, valVal));
				}

				return new AssociativeArrayValue(new AssocArrayType(elements[0].Value.RepresentedType, elements[0].Key.RepresentedType, aa), elements);
			}

			if (aa.Elements != null && aa.Elements.Count > 0)
			{
				var firstElement = aa.Elements[0].Key;
				var firstElementValue = aa.Elements[0].Value;

				var keyType = firstElement != null ? AbstractType.Get(firstElement.Accept(this)) : null;
				var valueType = firstElementValue != null ? AbstractType.Get(firstElementValue.Accept(this)) : null;

				return new AssocArrayType(valueType, keyType, aa);
			}

			return null;
		}

		public ISemantic Visit(FunctionLiteral x)
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

		public ISemantic Visit(TypeDeclarationExpression x)
		{
			// should be containing a typeof() only; static properties etc. are parsed as access expressions
			var t = TypeDeclarationResolver.ResolveSingle(x.Declaration, ctxt);
			
			if (eval)
				return new TypeValue(t);
			return t;
		}


		public ISemantic Visit(AnonymousClassExpression x)
		{
			//TODO
			return null;
		}

		public ISemantic Visit(VoidInitializer x)
		{
			if (eval)
				return new PrimitiveValue(DTokens.Void, decimal.Zero, x);
			return new PrimitiveType(DTokens.Void);
		}

		public ISemantic Visit(ArrayInitializer x)
		{
			return Visit((AssocArrayExpression)x);
		}

		public ISemantic Visit(StructInitializer x)
		{
			if (!eval) {
				// Create struct node with initialized members etc.
			}
			//TODO
			return null;
		}

		public ISemantic Visit(StructMemberInitializer structMemberInitializer)
		{
			//TODO
			return null;
		}
	}
}
