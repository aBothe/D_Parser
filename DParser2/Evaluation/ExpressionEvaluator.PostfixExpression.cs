using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.Templates;

namespace D_Parser.Evaluation
{
	public partial class ExpressionEvaluator
	{
		public ISymbolValue Evaluate(PostfixExpression x)
		{
			var foreExpression = Evaluate(x.PostfixForeExpression);

			if (x is PostfixExpression_Index)
			{
				var pfi = (PostfixExpression_Index)x;

			}

			return null;
		}
	}
}
