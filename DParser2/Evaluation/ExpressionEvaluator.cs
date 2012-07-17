using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Eval
{
	public partial class ExpressionEvaluator
	{
		#region Properties / Ctor
		ISymbolValueProvider vp;
		bool Const { get { return vp.ConstantOnly; } set { vp.ConstantOnly = value; } }

		private ExpressionEvaluator() { }
		#endregion

		#region Outer interaction
		public static ISymbolValue Resolve(IExpression arg, ResolverContextStack ctxt)
		{
			return Evaluate(arg, new StandardValueProvider(ctxt));
		}

		public static ISymbolValue Evaluate(IExpression expression, ISymbolValueProvider vp)
		{
			return new ExpressionEvaluator { vp=vp }.Evaluate(expression);
		}
		#endregion

		public ISymbolValue Evaluate(IExpression x)
		{
			if (x is TypeDeclarationExpression)
				return Evaluate((TypeDeclarationExpression)x);
			else if (x is PrimaryExpression)
				return Evaluate((PrimaryExpression)x);
			else if (x is PostfixExpression)
				return Evaluate((PostfixExpression)x);

			return null;
		}

		public ISymbolValue Evaluate(TypeDeclarationExpression x)
		{
			var r=TypeDeclarationResolver.Resolve(x.Declaration, vp.ResolutionContext);

			
		}
	}
}
