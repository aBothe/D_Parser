using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Dom;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Evaluation;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		#region Properties / Ctor
		/// <summary>
		/// True, if the expression's value shall be evaluated.
		/// False, if the expression's type is wanted only.
		/// </summary>
		protected readonly bool eval;
		private ResolverContextStack _ctxt;
		/// <summary>
		/// Is not null if the expression value shall be evaluated.
		/// </summary>
		protected ISymbolValueProvider ValueProvider { get; private set; }
		ResolverContextStack ctxt { get { return ValueProvider!=null ? ValueProvider.ResolutionContext : _ctxt; } }

		private Evaluation(ISymbolValueProvider vp) { this.ValueProvider = vp; this.eval = true; }
		private Evaluation(ResolverContextStack ctxt) { this._ctxt = ctxt; }
		#endregion

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
	}
}
