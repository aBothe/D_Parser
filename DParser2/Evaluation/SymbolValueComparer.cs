using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver;

namespace D_Parser.Evaluation
{
	public class SymbolValueComparer
	{


		public static bool IsEqual(IExpression ex, IExpression ex2, ISymbolValueProvider vp)
		{
			var val_x1 = ExpressionEvaluator.Evaluate(ex, vp);
			var val_x2 = ExpressionEvaluator.Evaluate(ex2, vp);

			//TEMPORARILY: Remove the string comparison
			if (val_x1 == null && val_x2 == null)
				return ex.ToString() == ex2.ToString();

			return IsEqual(val_x1, val_x2);
		}

		public static bool IsEqual(ISymbolValue val_x1, ISymbolValue val_x2)
		{
			//TODO
			return val_x1 != null && val_x2 != null && val_x1.ToString() == val_x2.ToString();
		}

		
	}
}
