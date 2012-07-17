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
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Eval
{
	public partial class ExpressionEvaluator
	{
		public ISymbolValue Evaluate(PostfixExpression x)
		{
			if (x is PostfixExpression_MethodCall)
				return Evaluate((PostfixExpression_MethodCall)x);

			var foreExpression = Evaluate(x.PostfixForeExpression);

			if (foreExpression == null)
				throw new EvaluationException(x.PostfixForeExpression, "Evaluation returned invalid result");

			if (x is PostfixExpression_Access)
				return Evaluate((PostfixExpression_Access)x, foreExpression, true);
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

					int i = 0;
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

					throw new EvaluationException(x, "Could not find key '" + val + "'");
				}
			}
			else if (x is PostfixExpression_Slice)
			{
				if (!(foreExpression is ArrayValue))
					throw new EvaluationException(x.PostfixForeExpression, "Must be an array");

				var ar = (ArrayValue)foreExpression;
				var sl = (PostfixExpression_Slice)x;

				// If the [ ] form is used, the slice is of the entire array.
				if (sl.FromExpression == null && sl.ToExpression == null)
					return foreExpression;

				// Make $ operand available
				var arrLen_Backup = vp.CurrentArrayLength;
				vp.CurrentArrayLength = ar.Elements.Length;

				var bound_lower = Evaluate(sl.FromExpression) as PrimitiveValue;
				var bound_upper = Evaluate(sl.ToExpression) as PrimitiveValue;

				vp.CurrentArrayLength = arrLen_Backup;

				if (bound_lower == null || bound_upper == null)
					throw new EvaluationException(bound_lower == null ? sl.FromExpression : sl.ToExpression, "Must be of an integral type");

				int lower = -1, upper = -1;
				try
				{
					lower = Convert.ToInt32(bound_lower.Value);
					upper = Convert.ToInt32(bound_upper.Value);
				}
				catch { throw new EvaluationException(lower != -1 ? sl.FromExpression : sl.ToExpression, "Boundary expression must base an integral type"); }

				if (lower < 0)
					throw new EvaluationException(sl.FromExpression, "Lower boundary must be greater than 0");
				if (lower >= ar.Elements.Length)
					throw new EvaluationException(sl.FromExpression, "Lower boundary must be smaller than " + ar.Elements.Length);
				if (upper < lower)
					throw new EvaluationException(sl.ToExpression, "Upper boundary must be greater than " + lower);
				if (upper >= ar.Elements.Length)
					throw new EvaluationException(sl.ToExpression, "Upper boundary must be smaller than " + ar.Elements.Length);


				var rawArraySlice = new ISymbolValue[upper - lower];
				int j = 0;
				for (int i = lower; i < upper; i++)
					rawArraySlice[j++] = ar.Elements[i];

				return new ArrayValue(ar.RepresentedType as ArrayType, rawArraySlice);
			}

			return null;
		}

		public ISymbolValue Evaluate(PostfixExpression_Access acc, ISymbolValue foreExpression, bool ExecuteMethodRefs)
		{
			if (foreExpression == null)
				foreExpression = Evaluate(acc.PostfixForeExpression);

			if (acc.AccessExpression is NewExpression)
			{
				var nx = (NewExpression)acc.AccessExpression;

				if (Const)
					throw new NoConstException(acc);
			}
			else
			{
				ISymbolValue v = null;

				// If it's a simple identifier only, static properties are allowed

				AbstractType[] nextPart = null;

				if (acc.AccessExpression is IdentifierExpression)
				{
					if ((v = StaticPropertyEvaluator.TryEvaluateProperty(foreExpression, ((IdentifierExpression)acc.AccessExpression).Value as string, vp)) != null)
						return v;

					nextPart = TypeDeclarationResolver.ResolveFurtherTypeIdentifier(
						((IdentifierExpression)acc.AccessExpression).Value as string,
						new[] { foreExpression.RepresentedType }, vp.ResolutionContext, acc.AccessExpression);
				}
				else if (acc.AccessExpression is TemplateInstanceExpression)
				{
					var tix = (TemplateInstanceExpression)acc.AccessExpression;

					nextPart = Evaluation.Resolve(tix, vp.ResolutionContext, new[] { foreExpression.RepresentedType });
				}

				if (nextPart == null || nextPart.Length == 0)
					throw new EvaluationException(acc, "No such member found", foreExpression.RepresentedType);

				var r = nextPart[0];

				if (r is MemberSymbol)
				{
					var mr = (MemberSymbol)r;

					/*
					 * If accessed member is a variable, delegate its value evaluation to the value provider.
					 * If it's a method (mostly a simple getter method), execute it.
					 * If it's a type, return a (somewhat static) link to it.
					 */
					if (mr.Definition is DVariable)
						return vp[(DVariable)mr.Definition];
					else if (mr.Definition is DMethod)
					{
						if (ExecuteMethodRefs)
							return CTFE.FunctionEvaluation.Execute((DMethod)mr.Definition, null, vp);
						else
							return new InternalOverloadValue(nextPart, acc);
					}
				}
				else if (r is UserDefinedType)
					return new TypeValue(r, acc);

				throw new EvaluationException(acc, "Unhandled result type", nextPart);
			}

			throw new EvaluationException(acc, "Invalid access expression");
		}

		public ISymbolValue Evaluate(PostfixExpression_MethodCall c)
		{
			ISymbolValue v = null;
			var methods=new List<DMethod>();

			// Evaluate prior expression
			if (c.PostfixForeExpression is IdentifierExpression)
				v = EvalId(c.PostfixForeExpression, false);
			else if (c.PostfixForeExpression is PostfixExpression_Access)
				v = Evaluate((PostfixExpression_Access)c.PostfixForeExpression, null, false);
			else
				v = Evaluate(c.PostfixForeExpression);

			// Get all available method overloads
			if (v is InternalOverloadValue)
			{
				var methods_res = ((InternalOverloadValue)v).Overloads;

				if (methods_res != null && methods_res.Length != 0)
					foreach (var r in methods_res)
						if (r is MemberSymbol)
						{
							var mr = (MemberSymbol)r;

							if (mr.Definition is DMethod)
								methods.Add((DMethod)mr.Definition);
						}
			}
			else if (v is DelegateValue)
				methods.Add(((DelegateValue)v).Method);

			// Evaluate all arguments (What about lazy ones?)

			// Compare arguments' types to overloads' expected parameters (pay attention to template parameters!)

			// If finally one method found, execute it in CTFE

			return null;
		}
	}
}
