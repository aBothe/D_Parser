using System;
using System.Collections.Generic;
using D_Parser.Dom.Expressions;

namespace D_Parser.Resolver.ExpressionSemantics.Caching
{
	/// <summary>
	/// Storage for all kinds of expressions that have been analyzed globally.
	/// </summary>
	public class ExpressionCache
	{
		// Important: Also store 'null cases', i.e. all situations that threw evaluation errors! 
		
		class EvEntry
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
			
			public EvaluationException[] errors;
			
			public EvEntry(ulong[] valHashes, ISymbolValue val, EvaluationException[] errors)
			{
				this.argumentValueHashes = valHashes;
				this.val = val;
				this.errors = errors;
			}
		}
		
		#region Properties
		Dictionary<ulong, List<EvEntry>> cache = new Dictionary<ulong, List<EvEntry>>();
		
		static ExpressionCache inst;
		public static ExpressionCache Instance
		{
			get{
				return inst ?? (inst = new ExpressionCache());
			}
		}
		#endregion
		
		#region Querying
		public ISymbolValue TryGetValue(IExpression x, params ISymbolValue[] argumentValues)
		{
			var hasArgs = argumentValues != null && argumentValues.Length != 0;
			
			// Note: After retrieving a value from the cache one must not add the value to the cache again!
			List<EvEntry> entries;
			if(!cache.TryGetValue(x.GetHash(), out entries) || entries.Count == 0)
				return null;
			
			var valHashes = hasArgs ? new ulong[argumentValues.Length] : null;
			
			if(hasArgs)
				for(int i = argumentValues.Length-1; i>=0;i--)
					if(argumentValues[i]!=null)
						valHashes[i] = argumentValues[i].GetHash();
			
			foreach(var e in entries)
			{
				if(hasArgs)
					for(int i = argumentValues.Length-1; i>=0; i--)
					{
						if(e.argumentValueHashes[i] != valHashes[i])
							goto cont;
					}
				else if(e.argumentValueHashes != null)
					continue;
				
				// If all argument values are matching, return this entry
				return e.val;
			cont: continue;
			}
			
			return null;
		}
		
		public ISymbolValue Cache(IExpression x, ISymbolValue val, params ISymbolValue[] arguments)
		{
			return Cache(x, val, null, arguments);
		}
		
		public ISymbolValue Cache(IExpression x, EvaluationException[] evaluationErrors, ISymbolValue[] arguments)
		{
			return Cache(x, null, evaluationErrors, arguments);
		}
		
		public ISymbolValue Cache(IExpression x, ISymbolValue val, EvaluationException[] evaluationErrors = null, ISymbolValue[] arguments = null)
		{
			var hasArgs = arguments != null && arguments.Length != 0;
			
			var hash = x.GetHash();
			List<EvEntry> entries;
			if(!cache.TryGetValue(hash, out entries) || entries.Count == 0)
				cache[hash] = entries = new List<ExpressionCache.EvEntry>();
			
			// Check for existing cache item matches and update their entry values if required
			for(int i = entries.Count-1; i >= 0; i--)
			{
				var e = entries[i];
				if(e.argumentValueHashes == null || e.argumentValueHashes.Length == 0 &&
				  arguments == null || arguments.Length == 0)
				{
					e.errors = evaluationErrors;
					e.val = val;
					return val;
				}
				
				if(arguments== null || arguments.Length == 0)
					continue;
				
				for(int k = arguments.Length-1; k >= 0; k--)
					if(arguments[k] != null && 
					   arguments[k].GetHash() != e.argumentValueHashes[k])
						goto cont;
				
				e.errors = evaluationErrors;
				e.val = val;
				return val;
			cont: continue;
			}
			
			// If no prior item altered, gen a new one
			var argHashes = hasArgs ? new ulong[arguments.Length] : null;
			if(hasArgs)
				for(int i = arguments.Length-1; i >= 0; i--)
					argHashes[i] = arguments[i].GetHash();
			entries.Add(new EvEntry(argHashes, val, evaluationErrors));
			return val;
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
		
		public void Clear()
		{
			cache.Clear();
		}
	}
}
