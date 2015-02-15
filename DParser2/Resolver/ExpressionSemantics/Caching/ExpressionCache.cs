using System;
using System.Collections.Generic;
using D_Parser.Dom.Expressions;

namespace D_Parser.Resolver.ExpressionSemantics.Caching
{
	public class ExpressionCache
	{
		struct EvEntry
		{
			/// <summary>
			/// The hashes of the argument expressions
			/// </summary>
			//public ulong[] argumentHashes;
			/// <summary>
			/// The hashes of the argument values.
			/// </summary>
			public long[] argumentValueHashes;
			
			public ISymbolValue val;
		}
		
		#region Properties
		Dictionary<long, List<EvEntry>> cache = new Dictionary<long, List<EvEntry>>();
		
		#endregion
		
		#region Querying
		ISymbolValue TryGetValue(IExpression x, params ISymbolValue[] argumentValues)
		{
			List<EvEntry> entries;
			if(!cache.TryGetValue(x.Accept(D_Parser.Dom.Visitors.AstElementHashingVisitor.Instance), out entries))
				return null;
			
			var parameters = GetParameters(x);
			
			// If the expression is not depending on other expressions return the only expression cached so far.
			if((parameters == null || parameters.Length == 0) && 
			   (argumentValues == null || argumentValues.Length == 0))
			{
				if(entries.Count == 1)
					return entries[0].val;
				return null;
			}
			
			if(argumentValues == null || argumentValues.Length == 0)
				return null;
			
			//var argHashes = new long[argumentValues.Length];
			var valHashes = new long[argumentValues.Length];
			
			for(int i = argumentValues.Length; i!=0;i--)
			{
				//argHashes[i] = parameters[i].GetHash();
				valHashes[i] = D_Parser.Dom.Visitors.AstElementHashingVisitor.Hash(argumentValues[i]);
			}
			
			foreach(var e in entries)
			{
				for(int i = argumentValues.Length; i!=0; i--)
				{
					if(e.argumentValueHashes[i] != valHashes[i])
						goto cont;
				}
				
			cont: continue;
			}
			
			return null;
		}
		
		static IExpression[] GetParameters(IExpression x)
		{
			if(x is ContainerExpression)
			{
				return (x as ContainerExpression).SubExpressions;
			}
			
			return null;
		}
		#endregion
		
		#region Basic I/O
		static ExpressionCache _inst;
		public static ExpressionCache Instance
		{
			get{
				return _inst ?? (_inst = new ExpressionCache());
			}
		}
		
		public void Clear()
		{
			cache.Clear();
		}
		#endregion
	}
}
