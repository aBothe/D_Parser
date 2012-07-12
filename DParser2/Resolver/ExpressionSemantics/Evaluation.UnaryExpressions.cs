using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Dom;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Evaluation;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		ISemantic E(UnaryExpression x)
		{
			if (x is UnaryExpression_Cat) // a = ~b;
				return E((x as SimpleUnaryExpression).UnaryExpression);

			else if (x is NewExpression)
			{
				// http://www.d-programming-language.org/expression.html#NewExpression
				var nex = x as NewExpression;
				ISemantic[] possibleTypes = null;

				if (nex.Type is IdentifierDeclaration)
					possibleTypes = TypeDeclarationResolver.Resolve((IdentifierDeclaration)nex.Type, ctxt, filterForTemplateArgs: false);
				else
					possibleTypes = TypeDeclarationResolver.Resolve(nex.Type, ctxt);

				var ctors = new Dictionary<DMethod, ClassType>();

				foreach (var t in possibleTypes)
				{
					if (t is ClassType)
					{
						var ct = (ClassType)t;

						bool foundExplicitCtor = false;

						foreach (var m in ct.Definition)
							if (m is DMethod && ((DMethod)m).SpecialType == DMethod.MethodType.Constructor)
							{
								ctors.Add((DMethod)m, ct);
								foundExplicitCtor = true;
							}

						if (!foundExplicitCtor)
							ctors.Add(new DMethod(DMethod.MethodType.Constructor) { Type = nex.Type }, ct);
					}
				}

				MemberSymbol finalCtor = null;

				var kvArray = ctors.ToArray();

				/*
				 * TODO: Determine argument types and filter out ctor overloads.
				 */

				if (kvArray.Length != 0)
					finalCtor = new MemberSymbol(kvArray[0].Key, kvArray[0].Value, x);
				else if (possibleTypes.Length != 0)
					return possibleTypes[0] as AbstractType;

				return finalCtor;
			}


			else if (x is CastExpression)
			{
				var ce = x as CastExpression;

				AbstractType castedType = null;

				if (ce.Type != null)
				{
					var castedTypes = TypeDeclarationResolver.Resolve(ce.Type, ctxt);

					ctxt.CheckForSingleResult(castedTypes, ce.Type);

					if (castedTypes != null && castedTypes.Length != 0)
						castedType = castedTypes[0];
				}
				else
				{
					castedType = E(ce.UnaryExpression) as AbstractType;

					if (castedType != null && ce.CastParamTokens != null && ce.CastParamTokens.Length > 0)
					{
						//TODO: Wrap resolved type with member function attributes
					}
				}

				return castedType;
			}

			else if (x is UnaryExpression_Add ||
				x is UnaryExpression_Decrement ||
				x is UnaryExpression_Increment ||
				x is UnaryExpression_Sub ||
				x is UnaryExpression_Not ||
				x is UnaryExpression_Mul)
				return E((x as SimpleUnaryExpression).UnaryExpression);

			else if (x is UnaryExpression_And)
				// &i -- makes an int* out of an int
				return new PointerType(E((x as UnaryExpression_And).UnaryExpression) as AbstractType, x);
			else if (x is DeleteExpression)
				return null;
			else if (x is UnaryExpression_Type)
			{
				var uat = x as UnaryExpression_Type;

				if (uat.Type == null)
					return null;

				var types = TypeDeclarationResolver.Resolve(uat.Type, ctxt);
				ctxt.CheckForSingleResult(types, uat.Type);

				if (types != null && types.Length != 0)
				{
					var id = new IdentifierDeclaration(uat.AccessIdentifier) { EndLocation = uat.EndLocation };

					// First off, try to resolve static properties
					var statProp = StaticPropertyResolver.TryResolveStaticProperties(types[0], uat.AccessIdentifier, ctxt, id);

					if (statProp != null)
						return statProp;

					// If it's not the case, try the conservative way
					var res = TypeDeclarationResolver.Resolve(id, ctxt, TypeDeclarationResolver.Convert(types));

					ctxt.CheckForSingleResult(res, x);

					if (res != null && res.Length != 0)
						return res[0] as AbstractType;
				}

				return null;
			}
			return null;
		}
	}
}
