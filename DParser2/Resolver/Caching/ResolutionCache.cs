using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Visitors;
using D_Parser.Misc;

namespace D_Parser.Resolver
{
	class ResolutionCache<T>
	{
		class CacheEntryDict : Dictionary<long, T>	{
			public T TryGetValue(ResolutionContext ctxt, long hashBias)
			{
				T t;
				Int64 d = unchecked(GetTemplateParamHash(ctxt) + hashBias);
				TryGetValue(d, out t);
				return t;
			}

			public void Add(ResolutionContext ctxt, T t, long hashBias)
			{
				Int64 d = unchecked(GetTemplateParamHash(ctxt) + hashBias);
				this[d] = t;
			}

			public bool Remove(ResolutionContext ctxt, long hashBias)
			{
				Int64 d = unchecked(GetTemplateParamHash(ctxt) + hashBias);
				return Remove(d);
			}

			static long GetTemplateParamHash(ResolutionContext ctxt)
			{
				var tpm = new List<TemplateParameter>();
				var hashVis = AstElementHashingVisitor.Instance;
				var h = ctxt.ScopedBlock == null ? 1 : Resolver.TypeResolution.DResolver.SearchBlockAt(ctxt.ScopedBlock, ctxt.CurrentContext.Caret).Accept(hashVis);
				foreach (var tps in ctxt.DeducedTypesInHierarchy)
				{
					if (tps == null || tpm.Contains(tps.Parameter))
						continue;

					h += tps.Accept(hashVis);
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

		public T TryGetType(ISyntaxRegion sr, long hashBias = 0)
		{
			CacheEntryDict ce;
			return sr != null && cache.TryGetValue(sr, out ce) ? ce.TryGetValue(ctxt, hashBias) : default(T);
		}

		public void Add(T t, ISyntaxRegion sr, long hashBias = 0)
		{
			if (t == null || sr == null || !CompletionOptions.Instance.EnableResolutionCache)
				return;

			CacheEntryDict ce;
			if (!cache.TryGetValue(sr, out ce))
				cache[sr] = ce = new CacheEntryDict();

			ce.Add(ctxt, t, hashBias);
		}

		public bool Remove(ISyntaxRegion sr, long hashBias = 0)
		{
			CacheEntryDict ce;
			return cache.TryGetValue(sr, out ce) && ce.Remove(ctxt, hashBias);
		}

		public void Clear()
		{
			cache.Clear ();
		}
	}
}
