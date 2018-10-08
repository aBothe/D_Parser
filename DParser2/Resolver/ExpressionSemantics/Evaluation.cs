using System;
using System.Collections.Generic;
using System.Diagnostics;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation : ExpressionVisitor<ISymbolValue>
	{
		#region Properties
		private readonly ResolutionContext ctxt;
		readonly List<EvaluationException> Errors = new List<EvaluationException>();

		/// <summary>
		/// Is not null if the expression value shall be evaluated.
		/// </summary>
		private readonly StatefulEvaluationContext evaluationState;
		bool readonlyEvaluation => evaluationState == null;

		bool ignoreErrors;

		/// <summary>
		/// Intermediate left operand operator-expressions.
		/// </summary>
		ISymbolValue lValue;
		/// <summary>
		/// Intermediate right operand operator-expressions.
		/// </summary>
		ISymbolValue rValue;
		#endregion

		[DebuggerStepThrough]
		Evaluation(StatefulEvaluationContext vp) : this(vp.ResolutionContext) {
			evaluationState = vp;
		}

		Evaluation(ResolutionContext ctxt)
		{
			evaluationState = null;
			this.ctxt = ctxt;
		}
		
		#region Errors
		void EvalError(EvaluationException ex)
		{
			if(!ignoreErrors)
				Errors.Add(ex);
		}
		
		void EvalError(IExpression x, string msg, ISemantic[] lastResults = null)
		{
			if(!ignoreErrors)
				Errors.Add(new EvaluationException(x,msg,lastResults));
		}
		
		void EvalError(IExpression x, string msg, ISemantic lastResult)
		{
			if(!ignoreErrors)
				Errors.Add(new EvaluationException(x,msg,new[]{lastResult}));
		}
		#endregion

		/// <summary>
		/// Readonly expression evaluation.
		/// </summary>
		public static ISymbolValue EvaluateValue (IExpression x, ResolutionContext ctxt)
		{
			return EvaluateValue(x, ctxt, out _);
		}

		/// <summary>
		/// Readonly expression evaluation.
		/// </summary>
		public static ISymbolValue EvaluateValue (IExpression x, ResolutionContext ctxt, out VariableValue evaluatedVariableValue)
		{
			evaluatedVariableValue = null;
			if (x == null)
				return null;

			if (ctxt == null)
				throw new ArgumentNullException(nameof(ctxt));

			if (ctxt.CancellationToken.IsCancellationRequested)
				return new TypeValue(new UnknownType(x));

			var evaluation = new Evaluation (ctxt);

			var v = x.Accept(evaluation);

			if (!(v is VariableValue variableValue))
			{
				if(v == null && evaluation.Errors.Count != 0)
					return new ErrorValue(evaluation.Errors.ToArray());
				return v;
			}

			evaluatedVariableValue = variableValue;
			return new Evaluation(ctxt).EvaluateVariableValue(variableValue);
		}

		public static ISymbolValue EvaluateValue(IExpression x, StatefulEvaluationContext vp)
		{
			if (x == null)
				return null;

			if (vp == null)
				throw new ArgumentNullException(nameof(vp));

			if (vp.ResolutionContext != null && vp.ResolutionContext.CancellationToken.IsCancellationRequested)
				return new TypeValue(new UnknownType(x));

			var ev = new Evaluation(vp);

			var v = x.Accept(ev);

			if(v == null && ev.Errors.Count != 0)
				return new ErrorValue(ev.Errors.ToArray());

			return v;
		}
		
		ISymbolValue EvaluateVariableValue(VariableValue v)
		{
			if (ctxt != null && ctxt.CancellationToken.IsCancellationRequested)
				return v;

			if(v.RepresentedType is TemplateParameterSymbol tps && tps.ParameterValue != null)
			{
				return tps.ParameterValue;
			}

			if (readonlyEvaluation || v.Variable.IsConst)
			{
				using (ctxt?.Push(v.RepresentedType))
					return EvaluateConstVariablesValue(v.Variable);
			}
			return evaluationState.GetLocalValue(v.Variable);
		}

		ISymbolValue EvaluateConstVariablesValue(DVariable variable)
		{
			if (!variable.IsConst)
				return new ErrorValue(new EvaluationException(variable + " must have a constant initializer"));

			if (variable is DEnumValue enumValueVariable && enumValueVariable.Initializer == null)
				return EvaluateNonInitializedEnumValue(enumValueVariable);

			return variable.Initializer?.Accept(this);
		}

		ISymbolValue EvaluateNonInitializedEnumValue(DEnumValue enumValue)
		{
			// Find previous enumvalue entry of parent enum
			var parentEnum = (DEnum)enumValue.Parent;

			var startIndex = parentEnum.Children.IndexOf(enumValue);
			if(startIndex == -1)
				throw new InvalidOperationException("enumValue must be child of its parent enum.");

			IExpression previousInitializer = null;
			var enumValueIncrementStepsToAdd = 0;
			for (var currentEnumChildIndex = startIndex - 1; currentEnumChildIndex >= 0; currentEnumChildIndex--)
			{
				var enumChild = (DEnumValue)parentEnum.Children[currentEnumChildIndex];
				if (enumChild.Initializer != null)
				{
					previousInitializer = enumChild.Initializer;
					enumValueIncrementStepsToAdd = startIndex - currentEnumChildIndex;
					break;
				}
			}

			if(previousInitializer == null)
				return new PrimitiveValue(DTokens.Int, startIndex); //TODO: Must be EnumBaseType.init, not only int.init

			var incrementExpression = BuildEnumValueIncrementExpression(previousInitializer, enumValueIncrementStepsToAdd);
			return incrementExpression.Accept(this);
		}

		private static AddExpression BuildEnumValueIncrementExpression(IExpression previousInitializer,
			int enumValueIncrementStepsToAdd)
		{
			var incrementExpression = new AddExpression(false)
			{
				LeftOperand = previousInitializer,
				RightOperand = new ScalarConstantExpression((decimal) enumValueIncrementStepsToAdd, LiteralFormat.Scalar)
				{
					Location = previousInitializer.EndLocation,
					EndLocation = previousInitializer.EndLocation
				}
			};
			return incrementExpression;
		}

		public ISymbolValue Visit(Expression ex)
		{
			/*
			 * The left operand of the ',' is evaluated, then the right operand is evaluated. 
			 * The type of the expression is the type of the right operand, 
			 * and the result is the result of the right operand.
			 */

			ISymbolValue ret = null;
			for (int i = 0; i < ex.Expressions.Count; i++)
			{
				var v = ex.Expressions[i].Accept(this);

				if (i == ex.Expressions.Count - 1)
				{
					ret = v;
					break;
				}
			}

			if (ret == null)
				EvalError(ex, "There must be at least one expression in the expression chain");
			return ret;
		}

		public ISymbolValue Visit(AsmRegisterExpression x)
		{
			EvalError(x, "Cannot evaluate inline assembly.");
			return null;
		}

		public ISymbolValue Visit(UnaryExpression_SegmentBase x)
		{
			return x.UnaryExpression.Accept(this);
		}

		public ISymbolValue Visit(SurroundingParenthesesExpression x)
		{
			return x.Expression.Accept(this);
		}

		public static bool IsFalseZeroOrNull(ISymbolValue v)
		{
			var pv = v as PrimitiveValue;
			if (pv != null)
				try
				{
					return pv.Value == 0m;
				}
				catch { }
			else
				return v is NullValue;

			return v != null;
		}
	}
}
