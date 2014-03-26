using D_Parser.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver
{
	class ResolutionCache
	{
		class CacheEntryDict : Dictionary<long, AbstractType> {
			public AbstractType TryGetValue(ResolutionContext ctxt)
			{
				AbstractType t;
				TryGetValue(GetTemplateParamHash(ctxt), out t);
				return t;
			}

			public void Add(ResolutionContext ctxt, AbstractType t)
			{
				this[GetTemplateParamHash(ctxt)] = t;
			}

			static long GetTemplateParamHash(ResolutionContext ctxt)
			{
				var tpm = new List<TemplateParameter>();
				long h = 0;
				foreach (var tps in ctxt.DeducedTypesInHierarchy)
					unchecked
					{
						if (tpm.Contains(tps.Parameter))
							continue;

						h += tps.Parameter.GetHashCode() + (tps.Base != null ? tps.Base.ToCode(false).GetHashCode() : tps.ParameterValue != null ? tps.ParameterValue.ToCode().GetHashCode() : 0);
						tpm.Add(tps.Parameter);
					}
				return h;
			}
		}

		readonly Dictionary<ISyntaxRegion, CacheEntryDict> cache = new Dictionary<ISyntaxRegion, CacheEntryDict>();
		public readonly ResolutionContext ctxt;

		public ResolutionCache(ResolutionContext ctxt) {
			this.ctxt = ctxt;
		}

		public AbstractType TryGetType(ISyntaxRegion sr)
		{
			CacheEntryDict ce;
			return cache.TryGetValue(sr, out ce) ? ce.TryGetValue(ctxt) : null;
		}

		public void Add(AbstractType t, ISyntaxRegion sr = null)
		{
			if (t == null)
				return;
			if (sr == null)
				sr = t.DeclarationOrExpressionBase;
			if (sr == null)
				return;

			CacheEntryDict ce;
			if (!cache.TryGetValue(sr, out ce))
				cache[sr] = ce = new CacheEntryDict();

			ce.Add(ctxt, t);
		}
	}
}
