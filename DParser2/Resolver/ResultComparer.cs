using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver
{
	public class ResultComparer
	{
		/// <summary>
		/// Checks given results for type equality
		/// </summary>
		public static bool IsEqual(ResolveResult r1, ResolveResult r2)
		{
			if (r1 is TemplateInstanceResult && r2 is TemplateInstanceResult)
				return ((TemplateInstanceResult)r1).Node == ((TemplateInstanceResult)r2).Node;
			else if (r1 is StaticTypeResult && r2 is StaticTypeResult)
				return ((StaticTypeResult)r1).BaseTypeToken == ((StaticTypeResult)r2).BaseTypeToken;


			return false;
		}

		/// <summary>
		/// Checks results for implicit type convertability 
		/// </summary>
		public static bool IsImplicitlyConvertible(ResolveResult resultToCheck, ResolveResult targetType)
		{
			// Initially remove aliases from results
			var _r=DResolver.TryRemoveAliasesFromResult(new[]{resultToCheck});
			if(_r==null || _r.Length==0)
				return IsEqual(resultToCheck,targetType);
			resultToCheck = _r[0];

			_r=DResolver.TryRemoveAliasesFromResult(new[]{targetType});
			if(_r==null || _r.Length == 0)
				return false;
			targetType = _r[0];

			if (resultToCheck is MemberResult && targetType is MemberResult)
			{
				var mr1 = (MemberResult)resultToCheck;
				var mr2 = (MemberResult)targetType;

				if (mr1.MemberBaseTypes != null && mr1.MemberBaseTypes.Length != 0 &&
					mr2.MemberBaseTypes != null && mr2.MemberBaseTypes.Length != 0)
					return IsImplicitlyConvertible(mr1.MemberBaseTypes[0], mr2.MemberBaseTypes[0]);
			}
			
			else if (resultToCheck is StaticTypeResult && targetType is StaticTypeResult)
			{

			}
			else if (resultToCheck is TypeResult && targetType is TypeResult)
				return IsImplicitlyConvertible((TypeResult)resultToCheck, (TypeResult)targetType);
			else if (resultToCheck is DelegateResult && targetType is DelegateResult)
			{

			}
			else if (resultToCheck is ArrayResult && targetType is ArrayResult)
			{

			}

			else if (resultToCheck is TypeTupleResult && targetType is TypeTupleResult)
			{

			}
			else if (resultToCheck is ExpressionTupleResult && targetType is ExpressionTupleResult)
			{

			}
			else if (resultToCheck is ExpressionValueResult && targetType is ExpressionValueResult)
			{

			}

			// http://dlang.org/type.html
			//TODO: Pointer to non-pointer / vice-versa checkability

			return false;
		}

		public static bool IsImplicitlyConvertible(TypeResult r, TypeResult target)
		{
			if (r.Node == target.Node)
				return true;

			if (r.BaseClass != null && r.BaseClass.Length != 0)
			{
				if (IsImplicitlyConvertible(r.BaseClass[0], target))
					return true;
			}

			if (r.ImplementedInterfaces != null && 
				r.ImplementedInterfaces.Length != 0 &&
				target.Node is DClassLike &&
				((DClassLike)target.Node).ClassType == DTokens.Interface)
			{
				foreach(var I in r.ImplementedInterfaces)
					if(IsImplicitlyConvertible(I[0], target))
						return true;
			}

			return false;
		}
	}
}
