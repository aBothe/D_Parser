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
