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
	public static class MixinAnalysis
	{
		internal class MixinCacheItem : Dictionary<INode, Tuple<VariableValue, ISyntaxRegion>>
		{
			public MixinCacheItem(INode parentNode, VariableValue v, ISyntaxRegion sr)
			{
				this[parentNode] = new Tuple<VariableValue, ISyntaxRegion>(v, sr);
			}

			public MixinCacheItem(){ }
		}

		[ThreadStatic]
		static List<MixinStatement> stmtsBeingAnalysed;
		
		static bool CheckAndPushAnalysisStack(MixinStatement mx)
		{
			if (mx == null)
				return false;

			if(stmtsBeingAnalysed == null)
				stmtsBeingAnalysed = new List<MixinStatement>();
			
			if(stmtsBeingAnalysed.Count != 0)
			{
				if(stmtsBeingAnalysed.Count > 5)
					return false;

				/*
				 * Only accept mixins that are located somewhere BEFORE the mixin that is the last inserted one in the stack.
				 * Also make sure mx and the peek mixin do have the same module root!
				 */
				var nr = mx.ParentNode != null ? mx.ParentNode.NodeRoot : null;
				foreach(var pk in stmtsBeingAnalysed)
				{
					if(nr == pk.ParentNode.NodeRoot)
					{
						if(mx == pk || mx.Location >= pk.Location)
							return false;
						break;
					}
				}
			}
			
			stmtsBeingAnalysed.Add(mx);
			
			return true;
		}
		
		static ISyntaxRegion GetMixinContent(MixinStatement mx, ResolutionContext ctxt, bool takeStmtCache , out VariableValue evaluatedVariable)
		{
			var parentNode = mx.ParentNode;
			evaluatedVariable = null;

			ISemantic v;
			MixinCacheItem mixinCacheItem;
			ISyntaxRegion parsedCode;

			using (ctxt.Push(mx.ParentNode, mx.Location))
			{
				mixinCacheItem = ctxt.MixinCache.TryGetType(mx);
				Tuple<VariableValue, ISyntaxRegion> cacheTuple;
				if (mixinCacheItem != null && mixinCacheItem.TryGetValue(parentNode, out cacheTuple))
				{
					evaluatedVariable = cacheTuple.Item1;
					return cacheTuple.Item2;
				}

				if (!CheckAndPushAnalysisStack(mx))
					return null;

				// Evaluate the mixin expression
				v = Evaluation.EvaluateValue(mx.MixinExpression, ctxt, true);
				evaluatedVariable = v as VariableValue;
				if (evaluatedVariable != null)
					v = Evaluation.EvaluateValue(evaluatedVariable, new StandardValueProvider(ctxt));

				stmtsBeingAnalysed.Remove(mx);
			}
			
			// Ensure it's a string literal
			var av = v as ArrayValue;
			if (av != null && av.IsString) {
				if (takeStmtCache) {
					parsedCode = DParser.ParseBlockStatement ("{" + av.StringValue + "}", mx.ParentNode);
				} else {
					var ast = DParser.ParseDeclDefs (av.StringValue);
					parsedCode = ast;

					ast.Parent = parentNode;

					foreach (var ch in ast) {
						if (mx.Attributes != null) {
							var dn = ch as DNode;
							if (dn != null) {
								if (dn.Attributes == null)
									dn.Attributes = new List<DAttribute> (mx.Attributes);
								else
									dn.Attributes.AddRange (mx.Attributes);
							}
						}
						ch.Parent = parentNode;
					}

					if (mx.Attributes != null)
						foreach (var ss in ast.StaticStatements) {
							if (ss.Attributes == null)
								ss.Attributes = mx.Attributes;
							else {
								var attrs = new DAttribute[mx.Attributes.Length + ss.Attributes.Length];
								mx.Attributes.CopyTo (attrs, 0);
								ss.Attributes.CopyTo (attrs, mx.Attributes.Length);
							}
						}
				}

				if (mixinCacheItem == null)
					ctxt.MixinCache.Add (new MixinCacheItem (parentNode, evaluatedVariable, parsedCode), mx);
				else
					mixinCacheItem.Add(parentNode,new Tuple<VariableValue, ISyntaxRegion>(evaluatedVariable, parsedCode));

				return parsedCode;
			}

			if(v is VariableValue)
				ctxt.MixinCache.Add(new MixinCacheItem(parentNode, v as VariableValue, null), mx);
			return null;
		}

		public static BlockStatement ParseMixinStatement(MixinStatement mx, ResolutionContext ctxt, out VariableValue vv)
		{
			return GetMixinContent(mx, ctxt, true, out vv) as BlockStatement;
		}
		
		public static DBlockNode ParseMixinDeclaration(MixinStatement mx, ResolutionContext ctxt, out VariableValue vv)
		{
			return GetMixinContent(mx, ctxt, false, out vv) as DBlockNode;
		}
	}
}
