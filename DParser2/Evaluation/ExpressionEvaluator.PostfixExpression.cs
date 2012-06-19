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
using D_Parser.Evaluation.Exceptions;

namespace D_Parser.Evaluation
{
	public partial class ExpressionEvaluator
	{
		public ISymbolValue Evaluate(PostfixExpression x)
		{
			var foreExpression = Evaluate(x.PostfixForeExpression);

			if (foreExpression == null)
				throw new EvaluationException(x.PostfixForeExpression, "Evaluation returned invalid result");

			if (x is PostfixExpression_Index)
			{
				var av = foreExpression as ArrayValue;

				if (av == null)
					throw new EvaluationException(x, "Expression must be an array value");

				var pfi = (PostfixExpression_Index)x;

				//TODO: opIndex & opSlice overloads -- will be handled in ctfe

				// Make $ operand possible
				var arrLen_Backup = vp.CurrentArrayLength;
				vp.CurrentArrayLength = av.Elements.Length;

				var n = Evaluate(pfi.Arguments[0]) as PrimitiveValue;

				vp.CurrentArrayLength = arrLen_Backup;

				if (n == null || !(n.Value is int))
					throw new EvaluationException(pfi.Arguments[0], "Index must be of type int");

				var i = (int)n.Value;

				if (i < 0 || i > av.Elements.Length)
					throw new EvaluationException(pfi.Arguments[0], "Index out of range - it must be between 0 and "+av.Elements.Length);

				return av.Elements[(int)n.Value];
			}
			else if (x is PostfixExpression_Slice)
			{
				var sl = (PostfixExpression_Slice)x;

				
			}

			return null;
		}
	}
}
