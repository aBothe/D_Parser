using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver.Templates;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver
{
	/// <summary>
	/// Provides methods to check if resolved types are matching each other and/or can be converted into each other.
	/// Used for UFCS completion, argument-parameter matching, template parameter deduction
	/// </summary>
	public class ResultComparer
	{
		/// <summary>
		/// Checks given results for type equality
		/// </summary>
		public static bool IsEqual(AbstractType r1, AbstractType r2)
		{
			if (r1 is TemplateIntermediateType && r2 is TemplateIntermediateType)
			{
				var tr1 = (TemplateIntermediateType)r1;
				var tr2 = (TemplateIntermediateType)r2;

				if (tr1.Definition != tr2.Definition)
					return false;

				//TODO: Compare deduced types
				return true;
			}
			else if (r1 is PrimitiveType && r2 is PrimitiveType)
				return ((PrimitiveType)r1).TypeToken == ((PrimitiveType)r2).TypeToken;
			else if (r1 is ArrayType && r2 is ArrayType)
			{
				var ar1 = (ArrayType)r1;
				var ar2 = (ArrayType)r2;

				if (!IsEqual(ar1.KeyType, ar2.KeyType))
					return false;

				return IsEqual(ar1.Base, ar2.Base);
			}

			//TODO: Handle other types

			return false;
		}

		/// <summary>
		/// Checks results for implicit type convertability 
		/// </summary>
		public static bool IsImplicitlyConvertible(AbstractType resultToCheck, AbstractType targetType, ResolverContextStack ctxt=null)
		{
			// Initially remove aliases from results
			bool resMem = false;
			var _r=DResolver.StripMemberSymbols(resultToCheck,out resMem);
			if(_r==null)
				return IsEqual(resultToCheck,targetType);
			resultToCheck = _r;

			_r=DResolver.StripMemberSymbols(targetType, out resMem);
			if(_r==null)
				return false;
			targetType = _r;


			if (targetType is MemberSymbol)
			{
				var mr2 = (MemberSymbol)targetType;

				if (mr2.Definition is TemplateParameterNode)
				{
					var tpn = (TemplateParameterNode)mr2.Node;

					var dedParam=new Dictionary<string, ResolveResult[]>();
					foreach(var tp in tpn.Owner.TemplateParameters)
						dedParam[tp.Name]=null;

					return new TemplateParameterDeduction(dedParam, ctxt).Handle(tpn.TemplateParameter, resultToCheck);
				}
			}

			if (resultToCheck is StaticTypeResult && targetType is StaticTypeResult)
			{
				var sr1 = (StaticTypeResult)resultToCheck;
				var sr2 = (StaticTypeResult)targetType;

				if (sr1.BaseTypeToken == sr2.BaseTypeToken)
					return true;

				switch (sr2.BaseTypeToken)
				{
					case DTokens.Int:
						return sr1.BaseTypeToken == DTokens.Uint;
					case DTokens.Uint:
						return sr1.BaseTypeToken == DTokens.Int;
					//TODO: Further types that can be converted into each other implicitly
				}
			}
			else if (resultToCheck is TypeResult && targetType is TypeResult)
				return IsImplicitlyConvertible((TypeResult)resultToCheck, (TypeResult)targetType);
			else if (resultToCheck is DelegateResult && targetType is DelegateResult)
			{
				//TODO
			}
			else if (resultToCheck is ArrayResult && targetType is ArrayResult)
			{
				var ar1 = (ArrayResult)resultToCheck;
				var ar2 = (ArrayResult)targetType;

				// Key as well as value types must be matching!
				var ar1_n= ar1.KeyType==null || ar1.KeyType.Length == 0;
				var ar2_n=ar2.KeyType==null || ar2.KeyType.Length == 0;

				if (ar1_n != ar2_n)
					return false;

				if(ar1_n || IsImplicitlyConvertible(ar1.KeyType[0], ar2.KeyType[0], ctxt))
					return IsImplicitlyConvertible(ar1.ResultBase, ar2.ResultBase, ctxt);
			}

			else if (resultToCheck is TypeTupleResult && targetType is TypeTupleResult)
			{
				return true;
			}
			else if (resultToCheck is ExpressionTupleResult && targetType is ExpressionTupleResult)
			{
				return true;
			}
			else if (resultToCheck is ExpressionValueResult && targetType is ExpressionValueResult)
			{
				return ((ExpressionValueResult)resultToCheck).Value.Equals(((ExpressionValueResult)targetType).Value);
			}

			// http://dlang.org/type.html
			//TODO: Pointer to non-pointer / vice-versa checkability? -- Can it really be done implicitly?

			return false;
		}

		public static bool IsImplicitlyConvertible(TemplateIntermediateType r, TemplateIntermediateType target)
		{
			if (r == null || target == null)
				return false;

			if (r.Definition == target.Definition)
				return true;

			if (r.Base != null && IsImplicitlyConvertible(r.Base, target))
				return true;

			if (r.BaseInterfaces!=null && 
				r.BaseInterfaces.Length != 0 && 
				target is InterfaceIntermediateType)
			{
				foreach(var I in r.BaseInterfaces)
					if(IsImplicitlyConvertible(I, target))
						return true;
			}

			return false;
		}
	}
}
