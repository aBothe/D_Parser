using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom;

namespace D_Parser.Evaluation
{
	public partial class ExpressionEvaluator
	{
		#region Properties / Ctor
		ISymbolValueProvider vp;
		bool Const { get { return vp.ConstantOnly; } set { vp.ConstantOnly = value; } }

		private ExpressionEvaluator() { }
		#endregion

		#region Outer interaction
		public static ResolveResult Resolve(IExpression arg, ResolverContextStack ctxt)
		{
			var ev=Evaluate(arg, new StandardValueProvider(ctxt));

			if (ev == null)
				return null;

			return new ExpressionValueResult{ 
				DeclarationOrExpressionBase=arg, 
				Value=ev
			};
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
			
			if(r!=null)
				return TryToEvaluateConstInitializer(r, vp.ResolutionContext);
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

		#region Helpers
		public static bool ToBool(object value)
		{
			bool b = false;

			try
			{
				b = Convert.ToBoolean(value);
			}
			catch { }

			return b;
		}

		public static double ToDouble(object value)
		{
			double d = 0;

			try
			{
				d = Convert.ToDouble(value);
			}
			catch { }

			return d;
		}

		public static long ToLong(object value)
		{
			long d = 0;

			try
			{
				d = Convert.ToInt64(value);
			}
			catch { }

			return d;
		}
		#endregion
	}
}
