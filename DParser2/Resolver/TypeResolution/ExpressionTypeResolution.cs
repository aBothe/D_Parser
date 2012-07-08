using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Dom;

namespace D_Parser.Resolver.TypeResolution
{
	public partial class ExpressionTypeResolver
	{
		public static AbstractType Resolve(IExpression ex, ResolverContextStack ctxt)
		{
			#region Operand based/Trivial expressions
			if (ex is Expression) // a,b,c;
			{
				//TODO
				return null;
			}

			else if (ex is SurroundingParenthesesExpression)
				return Resolve((ex as SurroundingParenthesesExpression).Expression, ctxt);

			else if (ex is AssignExpression || // a = b
				ex is XorExpression || // a ^ b
				ex is OrExpression || // a | b
				ex is AndExpression || // a & b
				ex is ShiftExpression || // a << 8
				ex is AddExpression || // a += b; a -= b;
				ex is MulExpression || // a *= b; a /= b; a %= b;
				ex is CatExpression || // a ~= b;
				ex is PowExpression) // a ^^ b;
				return Resolve((ex as OperatorBasedExpression).LeftOperand, ctxt);

			else if (ex is ConditionalExpression) // a ? b : c
				return Resolve((ex as ConditionalExpression).TrueCaseExpression, ctxt);

			else if (ex is OrOrExpression || // a || b
				ex is AndAndExpression || // a && b
				ex is EqualExpression || // a==b
				ex is IdendityExpression || // a is T
				ex is RelExpression) // a <= b
				return TypeDeclarationResolver.Resolve(new DTokenDeclaration(DTokens.Bool));

			else if (ex is InExpression) // a in b
			{
				// The return value of the InExpression is null if the element is not in the array; 
				// if it is in the array it is a pointer to the element.

				return Resolve((ex as InExpression).RightOperand, ctxt);
			}
			#endregion

			#region UnaryExpressions
			else if (ex is UnaryExpression)
			{
				if (ex is UnaryExpression_Cat) // a = ~b;
					return Resolve((ex as SimpleUnaryExpression).UnaryExpression, ctxt);

				else if (ex is NewExpression)
				{
					// http://www.d-programming-language.org/expression.html#NewExpression
					var nex = ex as NewExpression;
					ISemantic[] possibleTypes = null;

					if (nex.Type is IdentifierDeclaration)
						possibleTypes = TypeDeclarationResolver.Resolve((IdentifierDeclaration)nex.Type, ctxt, filterForTemplateArgs: false);
					else
						possibleTypes = TypeDeclarationResolver.Resolve(nex.Type, ctxt);

					var ctors = new Dictionary<DMethod,ClassType>();

					foreach (var t in possibleTypes)
					{
						if (t is ClassType)
						{
							var ct = (ClassType)t;

							bool foundExplicitCtor=false;

							foreach (var m in ct.Definition)
								if (m is DMethod && ((DMethod)m).SpecialType == DMethod.MethodType.Constructor){
									ctors.Add((DMethod)m,ct);
									foundExplicitCtor=true;
								}

							if(!foundExplicitCtor)
								ctors.Add(new DMethod(DMethod.MethodType.Constructor) { Type = nex.Type	},ct);
						}
					}

					MemberSymbol finalCtor = null;

					var kvArray = ctors.ToArray();

					/*
					 * TODO: Determine argument types and filter out ctor overloads.
					 */

					if (kvArray.Length != 0)
						finalCtor = new MemberSymbol(kvArray[0].Key, kvArray[0].Value, ex);
					else if (possibleTypes.Length != 0)
						return possibleTypes[0] as AbstractType;

					return finalCtor;
				}


				else if (ex is CastExpression)
				{
					var ce = ex as CastExpression;

					AbstractType castedType = null;

					if (ce.Type != null){
						var castedTypes = TypeDeclarationResolver.Resolve(ce.Type, ctxt);

						ctxt.CheckForSingleResult(castedTypes, ce.Type);

						if(castedTypes!=null && castedTypes.Length!=0)
							castedType=castedTypes[0];
					}
					else
					{
						castedType = Resolve(ce.UnaryExpression, ctxt);

						if (castedType != null && ce.CastParamTokens != null && ce.CastParamTokens.Length > 0)
						{
							//TODO: Wrap resolved type with member function attributes
						}
					}

					return castedType;
				}

				else if (ex is UnaryExpression_Add ||
					ex is UnaryExpression_Decrement ||
					ex is UnaryExpression_Increment ||
					ex is UnaryExpression_Sub ||
					ex is UnaryExpression_Not ||
					ex is UnaryExpression_Mul)
					return Resolve((ex as SimpleUnaryExpression).UnaryExpression, ctxt);

				else if (ex is UnaryExpression_And)
					// &i -- makes an int* out of an int
					return new PointerType(Resolve((ex as UnaryExpression_And).UnaryExpression, ctxt), ex);
				else if (ex is DeleteExpression)
					return null;
				else if (ex is UnaryExpression_Type)
				{
					var uat = ex as UnaryExpression_Type;

					if (uat.Type == null)
						return null;

					var types = TypeDeclarationResolver.Resolve(uat.Type, ctxt);
					ctxt.CheckForSingleResult(types, uat.Type);

					if(types!=null && types.Length!=0)
					{
						var id = new IdentifierDeclaration(uat.AccessIdentifier) { EndLocation=uat.EndLocation };

						// First off, try to resolve static properties
						var statProp= StaticPropertyResolver.TryResolveStaticProperties(types[0], uat.AccessIdentifier, ctxt, id); 

						if(statProp!=null)
							return statProp;

						// If it's not the case, try the conservative way
						var res=TypeDeclarationResolver.Resolve(id, ctxt, TypeDeclarationResolver.Convert(types));

						ctxt.CheckForSingleResult(res, ex);

						if(res!=null && res.Length!=0)
							return res[0] as AbstractType;
					}

					return null;
				}
			}
			#endregion

			else if (ex is PostfixExpression)
				return Resolve(ex as PostfixExpression, ctxt);

			#region PrimaryExpressions
			else if (ex is IdentifierExpression)
			{
				var id = ex as IdentifierExpression;
				int tt=0;

				if (id.IsIdentifier)
					return TypeDeclarationResolver.ResolveSingle(id.Value as string, ctxt, id, id.ModuleScoped) as AbstractType;
				
				switch (id.Format)
				{
					case Parser.LiteralFormat.CharLiteral:
						switch(id.Subformat)
						{
							case LiteralSubformat.Utf8:
								return new PrimitiveType(DTokens.Char,0,id);
							case LiteralSubformat.Utf16:
								return new PrimitiveType(DTokens.Wchar,0,id);
							case LiteralSubformat.Utf32:
								return new PrimitiveType(DTokens.Dchar,0,id);
						}
						return null;

					case LiteralFormat.FloatingPoint | LiteralFormat.Scalar:
						var im = id.Subformat.HasFlag(LiteralSubformat.Imaginary);
						
						tt = im ? DTokens.Idouble : DTokens.Double;

						if (id.Subformat.HasFlag(LiteralSubformat.Float))
							tt = im ? DTokens.Ifloat : DTokens.Float;
						else if (id.Subformat.HasFlag(LiteralSubformat.Real))
							tt = im ? DTokens.Ireal : DTokens.Real;

						return new PrimitiveType(tt,0,id);

					case LiteralFormat.Scalar:
						var unsigned = id.Subformat.HasFlag(LiteralSubformat.Unsigned);

						if (id.Subformat.HasFlag(LiteralSubformat.Long))
							tt = unsigned ? DTokens.Ulong : DTokens.Long;
						else
							tt = unsigned ? DTokens.Uint : DTokens.Int;

						return new PrimitiveType(tt,0,id);

					case Parser.LiteralFormat.StringLiteral:
					case Parser.LiteralFormat.VerbatimStringLiteral:
						AbstractType _t = null;

						if (ctxt != null)
						{
							var obj = ctxt.ParseCache.LookupModuleName("object").First();

							string strType = id.Subformat == LiteralSubformat.Utf32 ? "dstring" :
								id.Subformat == LiteralSubformat.Utf16 ? "wstring" :
								"string";

							var strNode = obj[strType];

							if (strNode != null)
								_t = TypeDeclarationResolver.HandleNodeMatch(strNode, ctxt, null, id);
						}
						
						if(_t==null)
						{
							var ch=id.Subformat == LiteralSubformat.Utf32 ? DTokens.Dchar :
								id.Subformat == LiteralSubformat.Utf16 ? DTokens.Wchar : DTokens.Char;

							_t=new ArrayType(new PrimitiveType(ch, DTokens.Immutable),
								new ArrayDecl{ 
									ValueType=new MemberFunctionAttributeDecl(DTokens.Immutable) {
										InnerType=new DTokenDeclaration(ch)
									}
								});
						}

						return _t;
				}
			}

			else if (ex is TemplateInstanceExpression)
				return ExpressionTypeResolver.Resolve((TemplateInstanceExpression)ex, ctxt);

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
						var tr=DResolver.ResolveBaseClasses(new ClassType(classDef as DClassLike, null, null), ctxt,true);

						if (tr.Base!=null)
						{
							// Important: Overwrite type decl base with 'super' token
							tr.Base.DeclarationOrExpressionBase=ex;

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
					var valueType = Resolve(arr.Elements[0], ctxt);

					return new ArrayType(valueType, ex);
				}
			}

			else if (ex is AssocArrayExpression)
				return Resolve((AssocArrayExpression)ex, ctxt);

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
				return TraitsResolver.Resolve((TraitsExpression)ex,ctxt);
			#endregion

			return null;
		}

		public static AssocArrayType Resolve(AssocArrayExpression aa, ResolverContextStack ctxt)
		{
			if (aa.Elements != null && aa.Elements.Count > 0)
			{
				var firstElement = aa.Elements[0].Key;
				var firstElementValue = aa.Elements[0].Value;

				var keyType = Resolve(firstElement, ctxt);
				var valueType = Resolve(firstElementValue, ctxt);

				return new AssocArrayType(valueType, keyType, aa);
			}
			return null;
		}
	}
}
