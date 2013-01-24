using System;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver.ExpressionSemantics;
using System.Collections.Generic;

namespace D_Parser.Resolver
{
	/// <summary>
	/// Description of MixinAnalysis.
	/// </summary>
	public class MixinAnalysis
	{
		static List<MixinStatement> stmtsBeingAnalysed = new List<MixinStatement>();
		[ThreadStatic]
		static uint stk;
		
		static string GetMixinContent(MixinStatement mx, ResolutionContext ctxt,out ISyntaxRegion cachedContent)
		{
			cachedContent = null;
			if(stk > 3)
				return null;
			
			lock(stmtsBeingAnalysed)
			{
				if(stmtsBeingAnalysed.Contains(mx))
					return null;
				stmtsBeingAnalysed.Add(mx);
			}
			stk++;
			
			bool pop;
			if(pop = ctxt.ScopedBlock != mx.ParentNode)
				ctxt.PushNewScope(mx.ParentNode as IBlockNode, mx);
			
			cachedContent = ctxt.MixinCache.Get<ISyntaxRegion>(mx);
			
			if(cachedContent != null)
			{
				lock(stmtsBeingAnalysed)
					stmtsBeingAnalysed.Remove(mx);
				if(pop)
					ctxt.Pop();
				stk--;
				return null;
			}
			
			var x = mx.MixinExpression;
			ISemantic v = null;
			try // 'try' because there is always a risk of e.g. not having something implemented or having an evaluation exception...
			{
				// Evaluate the mixin expression
				v = Evaluation.EvaluateValue(x, ctxt);
			}
			catch{}
			
			lock(stmtsBeingAnalysed)
				stmtsBeingAnalysed.Remove(mx);
			
			if(pop) 
				ctxt.Pop();
			
			stk--;
			// Ensure it's a string literal
			var av = v as ArrayValue;
			if(av != null && av.IsString)
				return av.StringValue;
			
			ctxt.MixinCache.Cache(mx, null);
			return null;
		}
		
		public static BlockStatement ParseMixinStatement(MixinStatement mx, ResolutionContext ctxt)
		{
			ISyntaxRegion sr;
			var literal = GetMixinContent(mx, ctxt, out sr);
			
			if(sr is BlockStatement)
				return (BlockStatement)sr;
			else if(literal == null)
				return null;
			
			var bs = (BlockStatement)DParser.ParseBlockStatement("{"+literal+"}", mx.ParentNode);
			ctxt.MixinCache.Cache(mx, bs);
			return bs;
		}
		
		public static DModule ParseMixinDeclaration(MixinStatement mx, ResolutionContext ctxt)
		{
			ISyntaxRegion sr;
			var literal = GetMixinContent(mx, ctxt, out sr);
			
			if(sr is DModule)
				return (DModule)sr;
			else if(literal == null)
				return null;
			
			var ast = (DModule)DParser.ParseString(literal, true);
			ctxt.MixinCache.Cache(mx, ast);
			
			if(ast == null)
				return null;
			
			foreach(var ch in ast)
			{
				if(mx.Attributes!=null)
				{
					var dn = ch as DNode;
					if(dn!=null)
					{
						if(dn.Attributes==null)
							dn.Attributes = new List<DAttribute>(mx.Attributes);
						else
							dn.Attributes.AddRange(mx.Attributes);
					}
				}
				ch.Parent = mx.ParentNode;
			}
				
			if(mx.Attributes!=null)
				foreach(var ss in ast.StaticStatements)
				{
					if(ss.Attributes == null)
						ss.Attributes = mx.Attributes;
					else{
						var attrs = new DAttribute[mx.Attributes.Length + ss.Attributes.Length];
						mx.Attributes.CopyTo(attrs,0);
						ss.Attributes.CopyTo(attrs,mx.Attributes.Length);
					}
				}
			
			return ast;
		}
	}
	
	public class MixinCache
	{
		ResolutionContext ctxt;
		Dictionary<MixinStatement, List<MxEntry>> cache = new Dictionary<MixinStatement, List<MxEntry>>();
		
		public MixinCache(ResolutionContext ctxt)
		{
			this.ctxt = ctxt;
		}
		
		class MxEntry
		{
			public TemplateParameterSymbol[] templateParams;
			
			public ISyntaxRegion mixinContent;
		}
		
		public void Cache(MixinStatement mx, ISyntaxRegion mixedInContent)
		{
			List<MxEntry> mxList;
			if(!cache.TryGetValue(mx,out mxList))
				cache[mx] = mxList = new List<MxEntry>();
			
			var parms = GetParameters(mx);
			var parms_array = parms.Count == 0 ? null : parms.ToArray();
			
			if(mixedInContent == null)
			{
				foreach(var e_ in mxList)
				{
					if(e_.mixinContent == null)
					{
						if(CompareParameterEquality(parms, e_.templateParams))
							return;
						break;
					}
				}
			}
			
			var e = new MxEntry{ templateParams = parms_array, mixinContent = mixedInContent };
			mxList.Add(e);
		}
		
		public T Get<T>(MixinStatement mx) where T : ISyntaxRegion
		{
			List<MxEntry> mxList;
			if(!cache.TryGetValue(mx,out mxList))
				return default(T);
			
			foreach(var e in mxList)
			{
				if(e.templateParams == null)
					return (T)e.mixinContent;
				
				var l = GetParameters(mx);
				if(CompareParameterEquality(l, e.templateParams))
					return (T)e.mixinContent;
			}
			
			return default(T);
		}
		
		bool CompareParameterEquality(List<TemplateParameterSymbol> l1, TemplateParameterSymbol[] l2)
		{
			if(l2 == null || l2.Length == 0)
			{
				return l1 == null || l1.Count == 0;
			}
			
			foreach(var p in l2)
			{
				for(int i = 0; i<l1.Count; i++)
				{
					var ex = l1[i];
					if(p.Parameter == ex.Parameter)
					{
						l1.Remove(ex);
						if(!ResultComparer.IsEqual(p.Base,ex.Base)){
							return false;
						}
						break;
					}
				}
			}
			
			return true;
		}
		
		List<TemplateParameterSymbol> GetParameters(MixinStatement mx)
		{
			var l = new List<TemplateParameterSymbol>();
			var curBn = mx.ParentNode;
			while(curBn!= null)
			{
				if(ctxt.ScopedBlock == curBn)
					break;
				curBn = curBn.Parent;
			}
			
			if(curBn == null)
				return l;
			
			var stk = new Stack<ContextFrame>();
			while(ctxt.CurrentContext != null)
			{
				l.AddRange(ctxt.CurrentContext.DeducedTemplateParameters.Values);
				if(!ctxt.PrevContextIsInSameHierarchy)
					break;
				stk.Push(ctxt.Pop());
			}
			
			while(stk.Count != 0)
				ctxt.Push(stk.Pop());
			
			return l;
		}
	}
}
