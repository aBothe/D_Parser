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

		public ISymbolValue Visit(TokenExpression x)
		{
			switch (x.Token)
			{
				// References current class scope
				case DTokens.This:
					if (resolveConstOnly)
					{
						EvalError(new NoConstException(x));
						return null;
					}

					var classDef = ctxt.ScopedBlock;

					while (!(classDef is DClassLike) && classDef != null)
						classDef = classDef.Parent as IBlockNode;

					/*
					 * TODO: Return an object reference to the 'this' object.
					 */
					return null;


				case DTokens.Super:
					// References super type of currently scoped class declaration

					if (resolveConstOnly)
					{
						EvalError(new NoConstException(x));
						return null;
					}

					/*classDef = ctxt.ScopedBlock;

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
					}*/

					/*
					 * TODO: Return an object reference to 'this', wheras the type is the superior type.
					 */
					return null;

				case DTokens.Null:
					if (resolveConstOnly)
					{
						EvalError(new NoConstException(x));
						return null;
					}

					//TODO
					return null;

				case DTokens.Dollar:
					// It's only allowed if the evaluation stack contains an array value
					if (ValueProvider.CurrentArrayLength != -1)
						return new PrimitiveValue(DTokens.Int, ValueProvider.CurrentArrayLength, x);
					else
					{
						EvalError(x, "Dollar not allowed here!");
						return null;
					}

				case DTokens.True:
					return new PrimitiveValue(DTokens.Bool, 1, x);
				case DTokens.False:
					return new PrimitiveValue(DTokens.Bool, 0, x);
				case DTokens.__FILE__:
					return new ArrayValue(GetStringType(), (ctxt.ScopedBlock.NodeRoot as DModule).FileName);
				case DTokens.__LINE__:
					return new PrimitiveValue(DTokens.Int, x.Location.Line, x);
				case DTokens.__MODULE__:
					return new ArrayValue(GetStringType(), (ctxt.ScopedBlock.NodeRoot as DModule).ModuleName);
				case DTokens.__FUNCTION__:
					//TODO
					return null;
				case DTokens.__PRETTY_FUNCTION__:
					var dm = ctxt.ScopedStatement.ParentNode as DMethod;
					return new ArrayValue(GetStringType(), dm == null ? "<not inside function>" : dm.ToString(false, true));
				default:
					return null;
			}
		}

		public ISymbolValue Visit(AssertExpression x)
		{
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

		public static string EvaluateMixinExpressionContent(ResolutionContext ctxt, MixinExpression x)
		{
			return EvaluateMixinExpressionContent(new StandardValueProvider(ctxt), x);
		}

		public static string EvaluateMixinExpressionContent(AbstractSymbolValueProvider vp,MixinExpression x)
		{
			var ev = new Evaluation(vp);
			ev.resolveConstOnly = true;

			var v = x.AssignExpression != null ? x.AssignExpression.Accept(ev) as ArrayValue : null;

			return v != null && v.IsString ? v.StringValue : null;
		}

		public ISymbolValue Visit(MixinExpression x)
		{
			var s = EvaluateMixinExpressionContent(ValueProvider, x);

			if (s == null)
			{
				EvalError(new InvalidStringException(x));
				return null;
			}

			// Parse it as an expression
			var ex = DParser.ParseAssignExpression(s);

			if (ex == null){
				EvalError( new EvaluationException(x, "Invalid expression code given"));
				return null;
			}
			//TODO: Excessive caching
			// Evaluate the expression's type/value
			return ex.Accept(this);
		}

		public ISymbolValue Visit(ImportExpression x)
		{
			var strType = GetStringType();

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

		public ISymbolValue Visit(ArrayLiteralExpression arr)
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

		public ISymbolValue Visit(AssocArrayExpression aa)
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

		public ISymbolValue Visit(FunctionLiteral x)
		{
			var dg = new DelegateType(
				(ctxt.Options & ResolutionOptions.DontResolveBaseTypes | ResolutionOptions.ReturnMethodReferencesOnly) != 0 ? null : TypeDeclarationResolver.GetMethodReturnType (x.AnonymousMethod, ctxt),
				x,
				TypeResolution.TypeDeclarationResolver.HandleNodeMatches(x.AnonymousMethod.Parameters, ctxt));

			return new DelegateValue(dg);
		}

		public ISymbolValue Visit(TypeDeclarationExpression x)
		{
			// should be containing a typeof() only; static properties etc. are parsed as access expressions
			var t = TypeDeclarationResolver.ResolveSingle(x.Declaration, ctxt);
			
			return new TypeValue(t);
		}


		public ISymbolValue Visit(AnonymousClassExpression x)
		{
			//TODO
			return null;
		}

		public ISymbolValue Visit(VoidInitializer x)
		{
			return new PrimitiveValue(DTokens.Void, decimal.Zero, x);
		}

		public ISymbolValue Visit(ArrayInitializer x)
		{
			return Visit((AssocArrayExpression)x);
		}

		public ISymbolValue Visit(StructInitializer x)
		{
			//TODO
			return null;
		}

		public ISymbolValue Visit(StructMemberInitializer structMemberInitializer)
		{
			//TODO
			return null;
		}
	}
}
