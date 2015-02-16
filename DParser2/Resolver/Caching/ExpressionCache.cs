using System;
using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver
{
	class ExpressionCache
	{
		#region Properties
		readonly ResolutionCache<ISymbolValue> cache;
		#endregion

		public ExpressionCache(ResolutionContext ctxt)
		{
			cache = new ResolutionCache<ISymbolValue> (ctxt);
		}

		#region Querying

		public ISymbolValue TryGetValue (IExpression x, params ISymbolValue[] argumentValues)
		{
			if (x == null)
				return null;
			
			var hashVis = D_Parser.Dom.Visitors.AstElementHashingVisitor.Instance;

			long hash = 0;

			foreach (var arg in argumentValues)
				unchecked {
					hash += arg.Accept (hashVis);	
				}
				
			return cache.TryGetType (x, hash);
		}

		public void Add (IExpression x, ISymbolValue v, params ISymbolValue[] argumentValues)
		{
			if (x == null || v == null)
				return;
			
			var hashVis = D_Parser.Dom.Visitors.AstElementHashingVisitor.Instance;

			long hash = 0;

			foreach (var arg in argumentValues)
				unchecked {
					hash += arg.Accept (hashVis);	
				}

			cache.Add (v, x, hash);
		}

		#endregion
	}
}
