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
					var parentNode = pk.ParentNode;
					if(parentNode != null && mx.ParentNode.NodeRoot == parentNode.NodeRoot)
					{
						if(mx == pk || mx.Location >= pk.Location)
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
		
		static string GetMixinContent(MixinStatement mx, ResolutionContext ctxt, bool takeStmtCache , out VariableValue evaluatedVariable)
		{
			evaluatedVariable = null;

			ISemantic v;
			using (ctxt.Push(mx.ParentNode, mx.Location))
			{
				var tup = ctxt.MixinCache.TryGetType(mx);
				if (tup != null)
				{
					evaluatedVariable = tup.Item2;
					return tup.Item1;
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
			if (av != null && av.IsString)
			{
				ctxt.MixinCache.Add(new Tuple<string, VariableValue>(av.StringValue, evaluatedVariable), mx);

				return av.StringValue;
			}
			
			return null;
		}

		public static BlockStatement ParseMixinStatement(MixinStatement mx, ResolutionContext ctxt, out VariableValue vv)
		{
			var literal = GetMixinContent(mx, ctxt, true, out vv);
			
			if(literal == null)
				return null;
			
			var bs = (BlockStatement)DParser.ParseBlockStatement("{"+literal+"}", mx.ParentNode);
			return bs;
		}
		
		public static DBlockNode ParseMixinDeclaration(MixinStatement mx, ResolutionContext ctxt, out VariableValue vv)
		{
			var literal = GetMixinContent(mx, ctxt, false, out vv);

			if(literal == null)
				return null;
			
			var ast = DParser.ParseDeclDefs(literal);
			
			if(ast == null)
				return null;

			ast.Parent = mx.ParentNode;
			
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
