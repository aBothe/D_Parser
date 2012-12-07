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
		
		static string GetMixinContent(MixinStatement mx, ResolutionContext ctxt,out ISyntaxRegion cachedContent)
		{
			cachedContent = null;
			lock(stmtsBeingAnalysed)
			{
				if(stmtsBeingAnalysed.Contains(mx))
					return null;
				stmtsBeingAnalysed.Add(mx);
			}
			bool pop;
			if(pop = ctxt.ScopedBlock != mx.ParentNode)
				ctxt.PushNewScope(mx.ParentNode as IBlockNode, mx);
			
			var tStk = new Stack<ContextFrame>();
			while(ctxt.CurrentContext != null)
			{
				if(ctxt.CurrentContext.MixinCache.TryGetValue(mx,out cachedContent) ||
				  !ctxt.PrevContextIsInSameHierarchy)
					break;
				tStk.Push(ctxt.Pop());
			}
			for(int i = tStk.Count; i!=0;i--)
				ctxt.Push(tStk.Pop());
			
			if(cachedContent != null)
			{
				lock(stmtsBeingAnalysed)
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
				if(v is VariableValue)
					v = Evaluation.EvaluateValue(x=(v as VariableValue).Variable.Initializer, ctxt);
			}
			catch{}
			
			lock(stmtsBeingAnalysed)
				stmtsBeingAnalysed.Remove(mx);
			
			if(pop) 
				ctxt.Pop();
			
			// Ensure it's a string literal
			var av = v as ArrayValue;
			if(av != null && av.IsString)
				return av.StringValue;
			
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
			ctxt.CurrentContext.MixinCache[mx]=bs;
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
			
			ctxt.CurrentContext.MixinCache[mx]=ast;
			return ast;
		}
	}
	
	public class MixinCache
	{
		class CEntry
		{
			public AbstractType[] TemplateParameters;
			
			public DBlockNode MixedInAst;
		}
	}
}
