using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Dom;
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
		private readonly ResolverContextStack ctxt;
		/// <summary>
		/// Is not null if the expression value shall be evaluated.
		/// </summary>
		private readonly ISymbolValueProvider ValueProvider;
		bool resolveConstOnly { get { return ValueProvider == null || ValueProvider.ConstantOnly; } set { if(ValueProvider!=null) ValueProvider.ConstantOnly = value; } }

		private Evaluation(ISymbolValueProvider vp) { 
			this.ValueProvider = vp; 
			this.eval = true;
			this.ctxt = vp.ResolutionContext;
		}
		private Evaluation(ResolverContextStack ctxt) {
			this.ctxt = ctxt;
		}
		#endregion

		/// <summary>
		/// Uses the standard value provider for expression value evaluation
		/// </summary>
		public static ISymbolValue EvaluateValue(IExpression x, ResolverContextStack ctxt)
		{
			return EvaluateValue(x, new StandardValueProvider(ctxt));
		}

		public static ISymbolValue EvaluateValue(IExpression x, ISymbolValueProvider vp)
		{
			return new Evaluation(vp).E(x) as ISymbolValue;
		}

		public static AbstractType EvaluateType(IExpression x, ResolverContextStack ctxt)
		{
			return new Evaluation(ctxt).E(x) as AbstractType;
		}

		ISemantic E(IExpression x)
		{
			if (x is Expression) // a,b,c;
			{
				//TODO
				return null;
			}

			else if (x is SurroundingParenthesesExpression)
				return E((x as SurroundingParenthesesExpression).Expression);

			else if (x is ConditionalExpression) // a ? b : c
				return E((ConditionalExpression)x);

			else if (x is OperatorBasedExpression)
				return E((OperatorBasedExpression)x);

			else if (x is UnaryExpression)
				return E((UnaryExpression)x);

			else if (x is PostfixExpression)
				return E((PostfixExpression)x);

			else if (x is PrimaryExpression)
				return E((PrimaryExpression)x);

			return null;
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
	}
}
