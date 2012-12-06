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
			public ulong[] argumentValueHashes;
			
			public ISymbolValue val;
		}
		
		#region Properties
		Dictionary<ulong, List<EvEntry>> cache = new Dictionary<ulong, List<EvEntry>>();
		
		#endregion
		
		#region Querying
		ISymbolValue TryGetValue(IExpression x, params ISymbolValue[] argumentValues)
		{
			List<EvEntry> entries;
			if(!cache.TryGetValue(x.GetHash(), out entries))
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
			
			//var argHashes = new ulong[argumentValues.Length];
			var valHashes = new ulong[argumentValues.Length];
			
			for(int i = argumentValues.Length; i!=0;i--)
			{
				//argHashes[i] = parameters[i].GetHash();
				valHashes[i] = (ulong)argumentValues[i].GetHashCode();
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
		
		IExpression[] GetParameters(IExpression x)
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
		public ExpressionCache Instance
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
