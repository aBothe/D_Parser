
using System;
using System.Collections.Generic;
using System.Threading;
namespace D_Parser.Dom
{
	public interface ISyntaxRegion
	{
		CodeLocation Location { get; }
		CodeLocation EndLocation { get; }

		//int Kind { get; }
	}

	public static class SyntaxRegionHelper
	{
		public static ISyntaxRegion First(this ISyntaxRegion a, ISyntaxRegion b)
		{
			return a.Location <= b.Location ? a : b;
		}
		
		static int NextTypeKind = 0;
		static Dictionary<Type, int> Kinds = new Dictionary<Type, int>();

		public static int Kind(this ISyntaxRegion sr)
		{
			int k;
			if (Kinds.TryGetValue(sr.GetType(), out k))
				return k;

			k = Interlocked.Increment(ref NextTypeKind);
			Kinds[sr.GetType()] = k;
			return k;
		}

		public static int Kind<T>()
		{
			int k;
			if (Kinds.TryGetValue(typeof(T), out k))
				return k;

			k = Interlocked.Increment(ref NextTypeKind);
			Kinds[typeof(T)] = k;
			return k;
		}
	}
}
