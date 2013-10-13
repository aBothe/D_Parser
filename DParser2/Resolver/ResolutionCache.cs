using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using D_Parser.Dom;

namespace D_Parser.Resolver
{
	public class ResolutionCache<T> where T : class
	{
		ConditionalWeakTable<ISyntaxRegion, T> paramLessCache 
			= new ConditionalWeakTable<ISyntaxRegion, T>();
		ConditionalWeakTable<ISyntaxRegion, Dictionary<TemplateParameterSymbol[], T>> paramBoundCache
			= new ConditionalWeakTable<ISyntaxRegion, Dictionary<TemplateParameterSymbol[], T>>();
		
		/// <summary>
		/// Adds a result to the cache.
		/// Warning: Does not check for double occurences of the same set of surrounding template parameters - 
		/// 		 so call TryGet first to ensure that the element hasn't been enlisted yet under these specific circumstances.
		/// </summary>
		public void Add(ResolutionContext ctxt, ISyntaxRegion element, T resolvedElement)
		{
			var parameters = GetParameters(ctxt, GetRelatedNode(element));
			
			if(resolvedElement is AbstractType && 
			   (resolvedElement as AbstractType).DeclarationOrExpressionBase == element)
				(resolvedElement as AbstractType).DeclarationOrExpressionBase = null;
			
			if(parameters.Count == 0)
			{
				try{
					lock(paramLessCache)
						paramLessCache.Add(element, resolvedElement);
				}catch(Exception)
				{
					
				}
				return;
			}
			
			lock(paramBoundCache)
			{
				Dictionary<TemplateParameterSymbol[], T> dict;
				if(!paramBoundCache.TryGetValue(element, out dict))
				{
					dict = new Dictionary<TemplateParameterSymbol[], T>();
					paramBoundCache.Add(element, dict);
				}
				
				dict.Add(parameters.ToArray(), resolvedElement);
			}
		}
		
		public bool TryGet(ResolutionContext ctxt, ISyntaxRegion element, out T resolvedElement)
		{
			resolvedElement = null;
			var parameters = GetParameters(ctxt, GetRelatedNode(element));
			
			if(parameters.Count == 0)
			{
				lock(paramLessCache)
					return paramLessCache.TryGetValue(element, out resolvedElement);
			}
			
			lock(paramBoundCache)
			{
				Dictionary<TemplateParameterSymbol[], T> dict;
				if(!paramBoundCache.TryGetValue(element, out dict))
					return false;
				
				foreach(var kv in dict)
				{
					if(CompareParameterEquality(parameters, kv.Key))
					{
						resolvedElement = kv.Value;
						return true;
					}
				}
			}
			return false;
		}
		
		static bool CompareParameterEquality(List<TemplateParameterSymbol> l1, TemplateParameterSymbol[] l2)
		{
			foreach(var p in l2)
			{
				foreach (var ex in l1)
				{
					if(p.Parameter == ex.Parameter)
					{
						if(!ResultComparer.IsEqual(p.Base,ex.Base)){
							return false;
						}
						break;
					}
				}
			}
			
			return true;
		}
		
		static List<TemplateParameterSymbol> GetParameters(ResolutionContext ctxt,INode relatedNode)
		{
			var relatedNodes = new List<INode>();
			while(relatedNode != null)
			{
				relatedNodes.Add(relatedNode);
				relatedNode = relatedNode.Parent;
			}
			
			var relatedTemplateParameters = new List<TemplateParameterSymbol>();
			
			var stk = new Stack<ContextFrame>();
			while(ctxt.CurrentContext != null)
			{
				foreach(var tparam in ctxt.CurrentContext.DeducedTemplateParameters.Values)
					if(relatedNodes.Contains(tparam.Parameter.Parent))
						relatedTemplateParameters.Add(tparam);
				
				if(!ctxt.PrevContextIsInSameHierarchy)
					break;
				stk.Push(ctxt.Pop());
			}
			
			while(stk.Count != 0)
				ctxt.Push(stk.Pop());
			
			return relatedTemplateParameters;
		}
		
		static INode GetRelatedNode(ISyntaxRegion sr)
		{
			if(sr is INode)
				return sr as INode;
			else if(sr is StaticStatement)
				return (sr as StaticStatement).ParentNode;
			
			return null;
		}
	}
}
