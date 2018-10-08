using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver.TypeResolution
{
	class SingleResolverVisitor : TypeDeclarationVisitor<AbstractType>
	{
		readonly ResolutionContext ctxt;
		bool filterTemplates;

		public SingleResolverVisitor(ResolutionContext ctxt, bool filterTemplates)
		{
			this.filterTemplates = filterTemplates;
			this.ctxt = ctxt;
		}

		public AbstractType Visit (IdentifierDeclaration id)
		{
			return AmbiguousType.Get(ExpressionTypeEvaluation.GetOverloads (id, ctxt, null, filterTemplates));
		}

		public AbstractType Visit (DTokenDeclaration td)
		{
			var tk = td.Token;

			if (DTokensSemanticHelpers.IsBasicType(tk))
				return new PrimitiveType(tk);

			return null;
		}

		public AbstractType Visit (ArrayDecl ad)
		{
			filterTemplates = true;
			var valueType = ad.ValueType?.Accept(this);

			AbstractType keyType = null;
			int fixedArrayLength = -1;

			if (ad.KeyExpression != null)
			{
				var val = Evaluation.EvaluateValue(ad.KeyExpression, ctxt);

				if (val != null)
				{
					// It should be mostly a number only that points out how large the final array should be
					var pv = val as PrimitiveValue;
					if (pv != null)
					{
						fixedArrayLength = System.Convert.ToInt32(pv.Value);

						if (fixedArrayLength < 0)
							ctxt.LogError(ad, "Invalid array size: Length value must be greater than 0");
					}
					//TODO Is there any other type of value allowed?
					else
						// Take the value's type as array key type
						keyType = val.RepresentedType;
				}
			}
			else if(ad.KeyType != null)
				keyType = TypeDeclarationResolver.ResolveSingle(ad.KeyType, ctxt);

			if (ad.KeyType == null && (ad.KeyExpression == null || fixedArrayLength >= 0)) {
				if (fixedArrayLength >= 0) {
					// D Magic: One might access tuple items directly in the pseudo array declaration - so stuff like Tup[0] i; becomes e.g. int i;
					var dtup = DResolver.StripMemberSymbols (valueType) as DTuple;
					if (dtup == null)
						return new ArrayType (valueType, fixedArrayLength);

					if (dtup.Items != null && fixedArrayLength < dtup.Items.Length)
						return AbstractType.Get(dtup.Items [fixedArrayLength]);
					else {
						ctxt.LogError (ad, "TypeTuple only consists of " + (dtup.Items != null ? dtup.Items.Length : 0) + " items. Can't access item at index " + fixedArrayLength);
						return null;
					}
				}
				return new ArrayType (valueType);
			}

			return new AssocArrayType(valueType, keyType);
		}

		public AbstractType Visit (DelegateDeclaration dg)
		{
			filterTemplates = true;
			var returnTypes = TypeDeclarationResolver.ResolveSingle(dg.ReturnType, ctxt);

			List<AbstractType> paramTypes=null;
			if(dg.Parameters!=null && 
				dg.Parameters.Count != 0)
			{	
				paramTypes = new List<AbstractType>();
				foreach(var par in dg.Parameters)
					paramTypes.Add(TypeDeclarationResolver.ResolveSingle(par.Type, ctxt));
			}

			return new DelegateType(returnTypes, dg, paramTypes);
		}

		public AbstractType Visit (PointerDecl td)
		{
			filterTemplates = true;
			var ptrBaseTypes = td.InnerDeclaration.Accept(this);

			if (ptrBaseTypes != null)
				ptrBaseTypes.NonStaticAccess = true;

			return new PointerType(ptrBaseTypes);
		}

		public AbstractType Visit (MemberFunctionAttributeDecl td)
		{
			filterTemplates = true;
			if (td.InnerType == null)
				return null;
				
			var ret = td.InnerType.Accept(this);

			if (ret == null)
				return null;// new UnknownType(attrDecl);

			ret.Modifiers = new [] { td.Modifier };

			return ret;
		}

		public AbstractType Visit (TypeOfDeclaration typeOf)
		{
			filterTemplates = true;
			// typeof(return)
			if (typeOf.Expression is TokenExpression && (typeOf.Expression as TokenExpression).Token == DTokens.Return)
			{
				return TypeDeclarationResolver.HandleNodeMatch(ctxt.ScopedBlock, ctxt, null, typeOf);
			}
			// typeOf(myInt)  =>  int
			else if (typeOf.Expression != null)
			{
				var wantedTypes = ExpressionTypeEvaluation.EvaluateType(typeOf.Expression, ctxt);
				return DResolver.StripMemberSymbols(wantedTypes);
			}

			return null;
		}

		public AbstractType Visit (VectorDeclaration td)
		{
			filterTemplates = true;
			return null;
		}

		public AbstractType Visit (VarArgDecl td)
		{
			filterTemplates = true;
			return td.InnerDeclaration != null ? td.InnerDeclaration.Accept(this) : null;
		}

		public AbstractType Visit (TemplateInstanceExpression td)
		{
			if (filterTemplates)
				return ExpressionTypeEvaluation.EvaluateType (td, ctxt);
			else
				return AmbiguousType.Get (ExpressionTypeEvaluation.GetOverloads (td, ctxt));
		}
	}
}
