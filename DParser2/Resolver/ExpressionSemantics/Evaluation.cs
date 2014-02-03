using System;
using System.Collections.Generic;
using System.Diagnostics;

using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation : ExpressionVisitor<ISymbolValue>
	{
		#region Properties / Ctor
		private readonly ResolutionContext ctxt;
		public List<EvaluationException> Errors = new List<EvaluationException>();

		/// <summary>
		/// Is not null if the expression value shall be evaluated.
		/// </summary>
		private readonly AbstractSymbolValueProvider ValueProvider;
		bool resolveConstOnly { get { return ValueProvider == null || ValueProvider.ConstantOnly; } set { if(ValueProvider!=null) ValueProvider.ConstantOnly = value; } }

		[DebuggerStepThrough]
		Evaluation(AbstractSymbolValueProvider vp) { 
			this.ValueProvider = vp; 
			vp.ev = this;
			this.ctxt = vp.ResolutionContext;
		}
		#endregion
		
		#region Errors
		bool ignoreErrors = false;
		internal void EvalError(EvaluationException ex)
		{
			if(!ignoreErrors)
				Errors.Add(ex);
		}
		
		internal void EvalError(IExpression x, string msg, ISemantic[] lastResults = null)
		{
			if(!ignoreErrors)
				Errors.Add(new EvaluationException(x,msg,lastResults));
		}
		
		internal void EvalError(IExpression x, string msg, ISemantic lastResult)
		{
			if(!ignoreErrors)
				Errors.Add(new EvaluationException(x,msg,new[]{lastResult}));
		}
		#endregion

		/// <summary>
		/// Uses the standard value provider for expression value evaluation
		/// </summary>
		public static ISymbolValue EvaluateValue (IExpression x, ResolutionContext ctxt, bool lazyVariableValueEvaluation = false)
		{
			try 
			{
				var vp = new StandardValueProvider (ctxt);
				var v = EvaluateValue (x, vp);
				
				if (v is VariableValue && !lazyVariableValueEvaluation) {
					return EvaluateValue (v as VariableValue, vp);
				}

				return v;
			} 
			catch (Exception ex) 
			{
				return new ErrorValue(new EvaluationException(x, ex.Message + "\n\n" + ex.StackTrace));
			}
		}

		public static ISymbolValue EvaluateValue(IExpression x, AbstractSymbolValueProvider vp)
		{
			if (vp == null)
				vp = new StandardValueProvider(null);

			var ev = new Evaluation(vp);

			var v = x.Accept(ev);

			if(v == null && ev.Errors.Count != 0)
				return new ErrorValue(ev.Errors.ToArray());

			return v;
		}
		
		public static ISymbolValue EvaluateValue(VariableValue v, AbstractSymbolValueProvider vp)
		{
			if(v.RepresentedType is TemplateParameterSymbol)
			{
				var tps = v.RepresentedType as TemplateParameterSymbol;
				if(tps.ParameterValue != null)
					return tps.ParameterValue;
			}
			
			var bt = v.Member;
			var ctxt = vp.ResolutionContext;

			if(bt != null)
			{
				//TODO: This is not tested entirely - but it makes test passing successfully!
				ctxt.PushNewScope (bt.Definition.Parent as Dom.IBlockNode);
				ctxt.CurrentContext.IntroduceTemplateParameterTypes(bt);
			}
			
			var val = vp[v.Variable];
			
			if(bt != null)
				ctxt.Pop ();
			
			return val ?? v;
		}

		public ISymbolValue Visit(Expression ex)
		{
			/*
			 * The left operand of the ',' is evaluated, then the right operand is evaluated. 
			 * The type of the expression is the type of the right operand, 
			 * and the result is the result of the right operand.
			 */

			ISymbolValue ret = null;
			for (int i = 0; i < ex.Expressions.Count; i++)
			{
				var v = ex.Expressions[i].Accept(this);

				if (i == ex.Expressions.Count - 1)
				{
					ret = v;
					break;
				}
			}

			if (ret == null)
				EvalError(ex, "There must be at least one expression in the expression chain");
			return ret;
		}

		public ISymbolValue Visit(SurroundingParenthesesExpression x)
		{
			return x.Expression.Accept(this);
		}

		public static bool IsFalseZeroOrNull(ISymbolValue v)
		{
			var pv = v as PrimitiveValue;
			if (pv != null)
				try
				{
					return pv.Value == 0m;
				}
				catch { }
			else
				return v is NullValue;

			return v != null;
		}

		/// <summary>
		/// Removes all variable references by resolving them via the given value provider.
		/// Useful when only the value is of interest, not its container or other things.
		/// </summary>
		public static ISymbolValue GetVariableContents(ISymbolValue v, AbstractSymbolValueProvider vp)
		{
			while (v is VariableValue)
				v = vp[(v as VariableValue).Variable];

			return v;
		}
	}
}
