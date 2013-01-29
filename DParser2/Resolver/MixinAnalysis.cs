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
		static ResolutionCache<DModule> mixinDeclCache = new ResolutionCache<DModule>();
		static ResolutionCache<BlockStatement> mixinStmtCache = new ResolutionCache<BlockStatement>();
		
		[ThreadStatic]
		static List<MixinStatement> stmtsBeingAnalysed;
		
		static bool CheckAndPushAnalysisStack(MixinStatement mx)
		{
			if(stmtsBeingAnalysed == null)
				stmtsBeingAnalysed = new List<MixinStatement>();
			
			if(stmtsBeingAnalysed.Count != 0)
			{
				/*
				 * Only accept mixins that are located somewhere BEFORE the mixin that is the last inserted one in the stack.
				 * Also make sure mx and the peek mixin do have the same module root!
				 */
				foreach(var pk in stmtsBeingAnalysed)
				{
					if(mx.ParentNode.NodeRoot == pk.ParentNode.NodeRoot)
					{
						if(mx == pk || mx.Location > pk.Location)
							return false;
						break;
					}
				}

				if(stmtsBeingAnalysed.Count > 5)
					return false;
			}
			
			stmtsBeingAnalysed.Add(mx);
			
			return true;
		}
		
		static string GetMixinContent(MixinStatement mx, ResolutionContext ctxt, bool takeStmtCache ,out ISyntaxRegion cachedContent)
		{
			cachedContent = null;
			
			if(!CheckAndPushAnalysisStack(mx))
				return null;
			
			bool pop;
			if(pop = (ctxt.ScopedBlock != mx.ParentNode && mx.ParentNode != null))
				ctxt.PushNewScope(mx.ParentNode as IBlockNode, mx);
			
			bool hadCachedItem;
			if(takeStmtCache)
			{
				BlockStatement stmt;
				hadCachedItem = mixinStmtCache.TryGet(ctxt, mx, out stmt);
				cachedContent = stmt;
			}
			else
			{
				DModule mod;
				hadCachedItem = mixinDeclCache.TryGet(ctxt, mx, out mod);
				cachedContent = mod;
			}
			
			if(hadCachedItem)
			{
				stmtsBeingAnalysed.Remove(mx);
				if(pop)
					ctxt.Pop();
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
			
			stmtsBeingAnalysed.Remove(mx);
			if(pop) 
				ctxt.Pop();
			
			// Ensure it's a string literal
			var av = v as ArrayValue;
			if(av != null && av.IsString)
				return av.StringValue;
			
			if(takeStmtCache)
				mixinStmtCache.Add(ctxt, mx, null);
			else
				mixinDeclCache.Add(ctxt, mx, null);
			return null;
		}
		
		public static BlockStatement ParseMixinStatement(MixinStatement mx, ResolutionContext ctxt)
		{
			ISyntaxRegion sr;
			var literal = GetMixinContent(mx, ctxt, true, out sr);
			
			if(sr is BlockStatement)
				return (BlockStatement)sr;
			else if(literal == null)
				return null;
			
			var bs = (BlockStatement)DParser.ParseBlockStatement("{"+literal+"}", mx.ParentNode);
			mixinStmtCache.Add(ctxt, mx, bs);
			return bs;
		}
		
		public static DModule ParseMixinDeclaration(MixinStatement mx, ResolutionContext ctxt)
		{
			ISyntaxRegion sr;
			var literal = GetMixinContent(mx, ctxt, false, out sr);
			
			if(sr is DModule)
				return (DModule)sr;
			else if(literal == null)
				return null;
			
			var ast = (DModule)DParser.ParseString(literal, true);
			mixinDeclCache.Add(ctxt, mx, ast);
			
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
}
