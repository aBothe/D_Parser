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

			if (x is PostfixExpression_Access)
			{
				var acc = (PostfixExpression_Access)x;

				if (Const && acc.AccessExpression is NewExpression)
					throw new NoConstException(x);

				if (acc.AccessExpression is IdentifierExpression)
				{
					var id = ((IdentifierExpression)acc.AccessExpression).Value as string;

					var results = TypeDeclarationResolver.ResolveFurtherTypeIdentifier(id, new[] {foreExpression.RepresentedType}, vp.ResolutionContext, acc);

					var init= ExpressionEvaluator.TryToEvaluateConstInitializer(results,vp.ResolutionContext);

					if (Const || init != null)
						return init;

					//TODO: Get the variable content from the value provider
				}
				else if (acc.AccessExpression is TemplateInstanceExpression)
				{
					var tix = (TemplateInstanceExpression)acc.AccessExpression;

					var results = TypeDeclarationResolver.ResolveFurtherTypeIdentifier(tix.TemplateIdentifier.Id, new[] { foreExpression.RepresentedType }, vp.ResolutionContext, acc);

					var init = ExpressionEvaluator.TryToEvaluateConstInitializer(results, vp.ResolutionContext);

					if (Const || init != null)
						return init;

					//TODO: See what's also possible in this case - throw an exception otherwise
				}
				else if (acc.AccessExpression is NewExpression)
				{
					var nx = (NewExpression)acc.AccessExpression;


				}
			}
			else if (x is PostfixExpression_Increment)
			{
				if (Const)
					throw new NoConstException(x);
				// Must be implemented anyway regarding ctfe
			}
			else if (x is PostfixExpression_Decrement)
			{
				if (Const)
					throw new NoConstException(x);
			}
			else if (x is PostfixExpression_MethodCall)
			{
				if (Const)
					throw new NoConstException(x);
				// At this point, CTFE will happen later on.
			}

			//TODO: opIndex & opSlice overloads -- will be handled in ctfe

			else if (x is PostfixExpression_Index)
			{
				var pfi = (PostfixExpression_Index)x;
				if (foreExpression is ArrayValue)
				{
					var av = foreExpression as ArrayValue;

					// Make $ operand available
					var arrLen_Backup = vp.CurrentArrayLength;
					vp.CurrentArrayLength = av.Elements.Length;

					var n = Evaluate(pfi.Arguments[0]) as PrimitiveValue;

					vp.CurrentArrayLength = arrLen_Backup;

					if (n == null)
						throw new EvaluationException(pfi.Arguments[0], "Returned no value");

					int i=0;
					try
					{
						i = Convert.ToInt32(n.Value);
					}
					catch { throw new EvaluationException(pfi.Arguments[0], "Index expression must be of type int"); }

					if (i < 0 || i > av.Elements.Length)
						throw new EvaluationException(pfi.Arguments[0], "Index out of range - it must be between 0 and " + av.Elements.Length);

					return av.Elements[i];
				}
				else if (foreExpression is AssociativeArrayValue)
				{
					var aa = (AssociativeArrayValue)foreExpression;

					var key = Evaluate(pfi.Arguments[0]);

					if (key == null)
						throw new EvaluationException(pfi.Arguments[0], "Returned no value");

					ISymbolValue val = null;

					foreach (var kv in aa.Elements)
						if (kv.Key.Equals(key))
							return kv.Value;

					throw new EvaluationException(x,"Could not find key '"+val+"'");
				}
			}
			else if (x is PostfixExpression_Slice)
			{
				if (!(foreExpression is ArrayValue))
					throw new EvaluationException(x.PostfixForeExpression, "Must be an array");

				var ar = (ArrayValue)foreExpression;
				var sl = (PostfixExpression_Slice)x;

				// If the [ ] form is used, the slice is of the entire array.
				if (sl.FromExpression == null && sl.ToExpression==null)
					return foreExpression;

				// Make $ operand available
				var arrLen_Backup = vp.CurrentArrayLength;
				vp.CurrentArrayLength = ar.Elements.Length;

				var bound_lower = Evaluate(sl.FromExpression) as PrimitiveValue;
				var bound_upper = Evaluate(sl.ToExpression) as PrimitiveValue;

				vp.CurrentArrayLength = arrLen_Backup;

				if (bound_lower == null || bound_upper==null)
					throw new EvaluationException(bound_lower==null ? sl.FromExpression : sl.ToExpression, "Must be of an integral type");

				int lower=-1,upper=-1;
				try
				{
					lower = Convert.ToInt32(bound_lower.Value);
					upper = Convert.ToInt32(bound_upper.Value);
				}
				catch { throw new EvaluationException(lower != -1 ? sl.FromExpression : sl.ToExpression, "Boundary expression must base an integral type"); }

				if (lower < 0)
					throw new EvaluationException(sl.FromExpression, "Lower boundary must be greater than 0");
				if (lower >= ar.Elements.Length)
					throw new EvaluationException(sl.FromExpression, "Lower boundary must be smaller than "+ar.Elements.Length);
				if (upper < lower)
					throw new EvaluationException(sl.ToExpression, "Upper boundary must be greater than " + lower);
				if (upper >= ar.Elements.Length)
					throw new EvaluationException(sl.ToExpression, "Upper boundary must be smaller than "+ar.Elements.Length);


				var rawArraySlice=new ISymbolValue[upper-lower];
				int j=0;
				for(int i=lower; i < upper ; i++)
					rawArraySlice[j++]=ar.Elements[i];
				
				return new ArrayValue(ar.RepresentedType, rawArraySlice);
			}

			return null;
		}
	}
}
