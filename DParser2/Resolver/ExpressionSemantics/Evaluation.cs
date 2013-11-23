using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		#region Properties / Ctor
		/// <summary>
		/// True, if the expression's value shall be evaluated.
		/// False, if the expression's type is wanted only.
		/// </summary>
		private bool eval;
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
			if(vp!=null)
				vp.ev = this;
			this.eval = true;
			this.ctxt = vp.ResolutionContext;
		}
		[DebuggerStepThrough]
		Evaluation(ResolutionContext ctxt) {
			this.ctxt = ctxt;
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

			var v = ev.E(x) as ISymbolValue;

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
			bool pop=false;

			if(bt != null)
			{
				//TODO: This is not tested entirely - but it makes test passing successfully!
				pop = true;
				ctxt.PushNewScope (bt.Definition.Parent as Dom.IBlockNode);
				ctxt.CurrentContext.IntroduceTemplateParameterTypes(bt);
			}
			
			var val = vp[v.Variable];
			
			if(bt != null)
			{
				if (pop)
					ctxt.Pop ();
				vp.ResolutionContext.CurrentContext.RemoveParamTypesFromPreferredLocals(bt);
			}
			
			return val ?? v;
		}

		/// <summary>
		/// Since most expressions should return a single type only, it's not needed to use this function unless you might
		/// want to pay attention on (illegal) multiple overloads.
		/// </summary>
		public static AbstractType[] EvaluateTypes(IExpression x, ResolutionContext ctxt)
		{
			var ev = new Evaluation(ctxt);
			ISemantic t = null;
			if(!Debugger.IsAttached)
				try { t = ev.E(x); }
				catch { }
			else
				t = ev.E(x);

			if (t is InternalOverloadValue)
				return ((InternalOverloadValue)t).Overloads;

			return t == null ? null : new[]{ AbstractType.Get(t) };
		}

		public static AbstractType EvaluateType(IExpression x, ResolutionContext ctxt)
		{
			var ev = new Evaluation(ctxt);
			ISemantic t = null;
			if(!Debugger.IsAttached)
				try { t = ev.E(x); }
				catch { }
			else
				t = ev.E(x);

			return AbstractType.Get(t);
		}

		/// <summary>
		/// HACK: SO prevention
		/// </summary>
		[ThreadStatic]
		static uint evaluationDepth = 0;
		
		ISemantic E(IExpression x)
		{
			if (evaluationDepth > 10)
				return null;
			evaluationDepth++;

			ISemantic ret = null;

			try{
			if (x is Expression) // a,b,c;
				{
					var ex = (Expression)x;
					/*
					 * The left operand of the ',' is evaluated, then the right operand is evaluated. 
					 * The type of the expression is the type of the right operand, 
					 * and the result is the result of the right operand.
					 */

					if (eval)
					{
						for (int i = 0; i < ex.Expressions.Count; i++)
						{
							var v = E(ex.Expressions[i]);

							if (i == ex.Expressions.Count - 1)
							{
								ret = v;
								break;
							}
						}

						if (ret == null)
							EvalError(x, "There must be at least one expression in the expression chain");
					}
					else
						ret = ex.Expressions.Count == 0 ? null : E(ex.Expressions[ex.Expressions.Count - 1]);
				}

				else if (x is SurroundingParenthesesExpression)
					ret = E((x as SurroundingParenthesesExpression).Expression);

				else if (x is ConditionalExpression) // a ? b : c
					ret = E((ConditionalExpression)x);

				else if (x is OperatorBasedExpression)
					ret = E(x as OperatorBasedExpression);

				else if (x is UnaryExpression)
					ret = E(x as UnaryExpression);

				else if (x is PostfixExpression)
					ret = E(x as PostfixExpression);

				else if (x is PrimaryExpression)
					ret = E(x as PrimaryExpression);
			}
			finally
			{
				if(evaluationDepth>0)
					evaluationDepth--;
			}
			return ret;
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

		public static AbstractType[] GetUnfilteredMethodOverloads(IExpression foreExpression, ResolutionContext ctxt, IExpression supExpression = null)
		{
			AbstractType[] overloads = null;

			if (foreExpression is TemplateInstanceExpression)
				overloads = Evaluation.GetOverloads(foreExpression as TemplateInstanceExpression, ctxt, null);
			else if (foreExpression is IdentifierExpression)
				overloads = Evaluation.GetOverloads(foreExpression as IdentifierExpression, ctxt, false);
			else if (foreExpression is PostfixExpression_Access)
			{
				overloads = Evaluation.GetAccessedOverloads((PostfixExpression_Access)foreExpression, ctxt, null, false);
			}
			else if (foreExpression is TokenExpression)
				overloads = GetResolvedConstructorOverloads((TokenExpression)foreExpression, ctxt);
			else
				overloads = new[] { Evaluation.EvaluateType(foreExpression, ctxt) };

			var l = new List<AbstractType>();
			bool staticOnly = true;

			foreach (var ov in DResolver.StripAliasSymbols(overloads))
			{
				var t = ov;
				if (ov is MemberSymbol)
				{
					var ms = ov as MemberSymbol;
					if (ms.Definition is Dom.DMethod)
					{
						l.Add(ms);
						continue;
					}

					staticOnly = false;
					t = DResolver.StripAliasSymbol(ms.Base);
				}

				if (t is TemplateIntermediateType)
				{
					var tit = t as TemplateIntermediateType;

					var m = TypeDeclarationResolver.HandleNodeMatches(
						GetOpCalls(tit, staticOnly), ctxt,
						null, supExpression ?? foreExpression);

					/*
					 * On structs, there must be a default () constructor all the time.
					 * If there are (other) constructors in structs, the explicit member initializer constructor is not
					 * provided anymore. This will be handled in the GetConstructors() method.
					 * If there are opCall overloads, canCreateeExplicitStructCtor overrides the ctor existence check in GetConstructors()
					 * and enforces that the explicit ctor will not be generated.
					 * An opCall overload with no parameters supersedes the default ctor.
					 */
					var canCreateExplicitStructCtor = m == null || m.Length == 0;

					if (!canCreateExplicitStructCtor)
						l.AddRange(m);

					m = TypeDeclarationResolver.HandleNodeMatches(
						GetConstructors(tit, canCreateExplicitStructCtor), ctxt,
						null, supExpression ?? foreExpression);

					if (m != null && m.Length != 0)
						l.AddRange(m);
				}
				else
					l.Add(ov);
			}

			return l.ToArray();
		}

		public static AbstractType[] GetResolvedConstructorOverloads(TokenExpression tk, ResolutionContext ctxt)
		{
			if (tk.Token == DTokens.This || tk.Token == DTokens.Super)
			{
				var classRef = EvaluateType(tk, ctxt) as TemplateIntermediateType;

				if (classRef != null)
					return D_Parser.Resolver.TypeResolution.TypeDeclarationResolver.HandleNodeMatches(GetConstructors(classRef), ctxt, classRef, tk);
			}
			return null;
		}
	}
}
