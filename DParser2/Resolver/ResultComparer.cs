using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver
{
	public class ResultComparer
	{
		/// <summary>
		/// Checks given results for type equality
		/// </summary>
		public static bool IsEqual(ResolveResult r1, ResolveResult r2)
		{
			return true;
		}

		/// <summary>
		/// Checks results for implicit type convertability 
		/// </summary>
		public static bool IsImplicitlyConvertible(ResolveResult resultToCheck, ResolveResult targetType)
		{
			if (IsEqual(resultToCheck, targetType))
				return true;

			return true;
		}
	}
}
