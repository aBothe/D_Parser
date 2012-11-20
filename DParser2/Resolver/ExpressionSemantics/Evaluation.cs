using System;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using System.Linq;
using System.Collections.Generic;
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
		private readonly bool eval;
		private readonly ResolutionContext ctxt;
		/// <summary>
		/// Is not null if the expression value shall be evaluated.
		/// </summary>
		private readonly AbstractSymbolValueProvider ValueProvider;
		bool resolveConstOnly { get { return ValueProvider == null || ValueProvider.ConstantOnly; } set { if(ValueProvider!=null) ValueProvider.ConstantOnly = value; } }

		private Evaluation(AbstractSymbolValueProvider vp) { 
			this.ValueProvider = vp; 
			this.eval = true;
			this.ctxt = vp.ResolutionContext;
		}
		private Evaluation(ResolutionContext ctxt) {
			this.ctxt = ctxt;
		}
		#endregion

		/// <summary>
		/// Uses the standard value provider for expression value evaluation
		/// </summary>
		public static ISymbolValue EvaluateValue(IExpression x, ResolutionContext ctxt)
		{
			try
			{
				return EvaluateValue(x, new StandardValueProvider(ctxt));
			}
			catch
			{
				//TODO Redirect evaluation exception to some outer logging service
			}
			return null;
		}

		public static ISymbolValue EvaluateValue(IExpression x, AbstractSymbolValueProvider vp)
		{
			if (vp == null)
				vp = new StandardValueProvider(null);

			return new Evaluation(vp).E(x) as ISymbolValue;
		}

		/// <summary>
		/// Since most expressions should return a single type only, it's not needed to use this function unless you might
		/// want to pay attention on (illegal) multiple overloads.
		/// </summary>
		public static AbstractType[] EvaluateTypes(IExpression x, ResolutionContext ctxt)
		{
			var t = new Evaluation(ctxt).E(x);

			if (t is InternalOverloadValue)
				return ((InternalOverloadValue)t).Overloads;

			return new[]{ AbstractType.Get(t) };
		}

		public static AbstractType EvaluateType(IExpression x, ResolutionContext ctxt)
		{
			return AbstractType.Get(new Evaluation(ctxt).E(x));
		}

		/// <summary>
		/// HACK: SO prevention
		/// </summary>
		[ThreadStatic]
		static int evaluationDepth = 0;
		
		ISemantic E(IExpression x)
		{
			if(evaluationDepth > 20){
				evaluationDepth = 0;
				return null;
			}
			evaluationDepth++;
			
			ISemantic ret = null;
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

					if(ret == null)
						throw new EvaluationException(x, "There must be at least one expression in the expression chain");
				}
				else
					ret = ex.Expressions.Count == 0 ? null : E(ex.Expressions[ex.Expressions.Count - 1]);
			}

			else if (x is SurroundingParenthesesExpression)
				ret= E((x as SurroundingParenthesesExpression).Expression);

			else if (x is ConditionalExpression) // a ? b : c
				ret= E((ConditionalExpression)x);

			else if (x is OperatorBasedExpression)
				ret= E((OperatorBasedExpression)x);

			else if (x is UnaryExpression)
				ret= E((UnaryExpression)x);

			else if (x is PostfixExpression)
				ret= E((PostfixExpression)x);

			else if (x is PrimaryExpression)
				ret= E((PrimaryExpression)x);

			evaluationDepth--;
			
			return ret;
		}

		public static bool IsFalseZeroOrNull(ISymbolValue v)
		{
			var pv = v as PrimitiveValue;
			if (pv != null)
				try
				{
					return !Convert.ToBoolean(pv.Value);
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
				v = vp[((VariableValue)v).Variable];

			return v;
		}

		public static AbstractType[] GetUnfilteredMethodOverloads(IExpression foreExpression, ResolutionContext ctxt, IExpression supExpression = null)
		{
			AbstractType[] overloads = null;

			if (foreExpression is TemplateInstanceExpression)
				overloads = Evaluation.GetOverloads((TemplateInstanceExpression)foreExpression, ctxt, null);
			else if (foreExpression is IdentifierExpression)
				overloads = Evaluation.GetOverloads((IdentifierExpression)foreExpression, ctxt);
			else if (foreExpression is PostfixExpression_Access)
			{
				bool ufcs = false; // TODO?
				overloads = Evaluation.GetAccessedOverloads((PostfixExpression_Access)foreExpression, ctxt, out ufcs, null, false);
			}
			else if (foreExpression is TokenExpression)
				overloads = GetResolvedConstructorOverloads((TokenExpression)foreExpression, ctxt);
			else
				overloads = new[] { Evaluation.EvaluateType(foreExpression, ctxt) };

			var l = new List<AbstractType>();

			foreach (var ov in overloads)
				if (ov is TemplateIntermediateType)
				{
					var tit = (TemplateIntermediateType)ov;
					
					var m = TypeDeclarationResolver.HandleNodeMatches(
						GetOpCalls(tit), ctxt,
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

					if(m!=null && m.Length != 0)
						l.AddRange(m);
				}
				else
					l.Add(ov);

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
