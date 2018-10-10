using System;
using System.Linq;
using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver.Templates;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		bool? returnBaseTypeOnly;

		public ISymbolValue VisitPostfixExpression_Methodcall(PostfixExpression_MethodCall call)
		{
			var returnBaseTypeOnly = !this.returnBaseTypeOnly.HasValue ? 
				!ctxt.Options.HasFlag(ResolutionOptions.ReturnMethodReferencesOnly) : 
				this.returnBaseTypeOnly.Value;
			this.returnBaseTypeOnly = null;

			var callArguments = new List<ISymbolValue>();
			var callArgument_Semantic = new List<ISemantic>();
			if (call.ArgumentCount > 0)
			{
				foreach (var arg in call.Arguments)
				{
					var callArgument = evaluationState != null
						? EvaluateValue(arg, evaluationState)
						: EvaluateValue(arg, ctxt);
					callArguments.Add(callArgument);
					callArgument_Semantic.Add(callArgument);
				}
			}

			ISymbolValue delegValue;

			// Deduce template parameters later on
			IEnumerable<AbstractType> baseExpression;
			TemplateInstanceExpression tix;

			GetRawCallOverloads(ctxt, call, out baseExpression, out tix);

			var argTypeFilteredOverloads = EvalMethodCall(baseExpression, tix, ctxt, call, callArgument_Semantic, out delegValue, returnBaseTypeOnly, evaluationState);

			if (delegValue != null)
				return delegValue;
			if (argTypeFilteredOverloads == null)
				return null;

			// Execute/Evaluate the variable contents etc.
			return TryDoCTFEOrGetValueRefs(argTypeFilteredOverloads, call.PostfixForeExpression, callArguments);
		}

		public static AbstractType EvalMethodCall(IEnumerable<AbstractType> baseExpression, TemplateInstanceExpression tix,
			ResolutionContext ctxt, 
			PostfixExpression_MethodCall call, List<ISemantic> callArguments, out ISymbolValue delegateValue,
			bool returnBaseTypeOnly, StatefulEvaluationContext ValueProvider = null)
		{
			delegateValue = null;

			bool returnInstantly;
			var methodOverloads = MethodOverloadCandidateSearchVisitor.SearchCandidates (baseExpression, ctxt, ValueProvider, call, 
			                                                      ref delegateValue, returnBaseTypeOnly, out returnInstantly);

			if (returnInstantly) {
				return methodOverloads.Count > 0 ? methodOverloads[0] : null;
			}

			if (methodOverloads.Count == 0)
				return null;

			var templateMatchedMethodOverloads = TryMatchTemplateArgumentsToOverloads (tix, ctxt, methodOverloads);

			return MethodOverloadsByParameterTypeComparisonFilter.FilterOverloads (call,
				templateMatchedMethodOverloads, ctxt,
				ValueProvider, returnBaseTypeOnly,
				callArguments, ref delegateValue);
		}

		static IEnumerable<AbstractType> TryMatchTemplateArgumentsToOverloads (TemplateInstanceExpression tix, ResolutionContext ctxt, IEnumerable<AbstractType> methodOverloads)
		{
			if (tix != null) {
				return TemplateInstanceHandler.DeduceParamsAndFilterOverloads(methodOverloads, tix, ctxt, true);
			}
			return methodOverloads;
		}

		void GetRawCallOverloads(ResolutionContext ctxt,PostfixExpression_MethodCall call, 
			out IEnumerable<AbstractType> baseExpression,
			out TemplateInstanceExpression tix)
		{
			tix = null;

			if (call.PostfixForeExpression is PostfixExpression_Access)
			{
				var pac = (PostfixExpression_Access)call.PostfixForeExpression;
				tix = pac.AccessExpression as TemplateInstanceExpression;

				var vs = EvalPostfixAccessExpression(this, ctxt, pac, null, false, false);

				baseExpression = AbstractType.Get(vs);
			}
			else
			{
				// Explicitly don't resolve the methods' return types - it'll be done after filtering to e.g. resolve template types to the deduced one
				var optBackup = ctxt.CurrentContext.ContextDependentOptions;
				ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.DontResolveBaseTypes;

				if (call.PostfixForeExpression is TokenExpression)
					baseExpression = ExpressionTypeEvaluation.GetResolvedConstructorOverloads((TokenExpression)call.PostfixForeExpression, ctxt);
				else
				{
					var fore = call.PostfixForeExpression;
					if (fore is TemplateInstanceExpression)
						tix = fore as TemplateInstanceExpression;

					baseExpression = ExpressionTypeEvaluation.GetUnfilteredMethodOverloads(fore, ctxt);
				}

				ctxt.CurrentContext.ContextDependentOptions = optBackup;
			}
		}

		/// <summary>
		/// Returns either all unfiltered and undeduced overloads of a member of a base type/value (like b from type a if the expression is a.b).
		/// if <param name="EvalAndFilterOverloads"></param> is false.
		/// If true, all overloads will be deduced, filtered and evaluated, so that (in most cases,) a one-item large array gets returned
		/// which stores the return value of the property function b that is executed without arguments.
		/// Also handles UFCS - so if filtering is wanted, the function becom
		/// </summary>
		public static List<R> EvalPostfixAccessExpression<R>(ExpressionVisitor<R> vis, ResolutionContext ctxt,PostfixExpression_Access acc,
			ISemantic resultBase = null, bool EvalAndFilterOverloads = true, bool ResolveImmediateBaseType = true, StatefulEvaluationContext ValueProvider = null)
			where R : class,ISemantic
		{
			if (acc == null)
				return null;

			var baseExpression = resultBase ?? acc.PostfixForeExpression?.Accept(vis);

			if (acc.AccessExpression is NewExpression)
			{
				/*
				 * This can be both a normal new-Expression as well as an anonymous class declaration!
				 */
				//TODO!
				return null;
			}

			if(baseExpression == null)
				return new List<R>();
			
			List<AbstractType> overloads;
			var optBackup = ctxt.CurrentContext.ContextDependentOptions;
			
			if (acc.AccessExpression is TemplateInstanceExpression tix)
			{
				if (!ResolveImmediateBaseType)
					ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.DontResolveBaseTypes;

				// Do not deduce and filter if superior expression is a method call since call arguments' types also count as template arguments!
				overloads = ExpressionTypeEvaluation.GetOverloads(tix, ctxt, AbstractType.Get(baseExpression), EvalAndFilterOverloads);

				if (!ResolveImmediateBaseType)
					ctxt.CurrentContext.ContextDependentOptions = optBackup;
			}

			else if (acc.AccessExpression is IdentifierExpression id)
			{
				if (EvalAndFilterOverloads)
				{
					var staticPropResult = StaticProperties.TryEvalPropertyValue(ctxt, baseExpression, id.IdHash);
					if (staticPropResult != null)
						return new List<R> { (R) staticPropResult };
				}

				if (!ResolveImmediateBaseType)
					ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.DontResolveBaseTypes;

				overloads = ExpressionTypeEvaluation.GetOverloads(id, ctxt, AbstractType.Get(baseExpression), EvalAndFilterOverloads);

				if (!ResolveImmediateBaseType)
					ctxt.CurrentContext.ContextDependentOptions = optBackup;
			}
			else
			{/*
				if (eval){
					EvalError(acc, "Invalid access expression");
					return null;
				}*/
				ctxt.LogError(acc, "Invalid post-dot expression");
				return null;
			}

			// If evaluation active and the access expression is stand-alone, return a single item only.
			if (!EvalAndFilterOverloads || ValueProvider == null)
				return overloads as List<R>;

			var evaluation = new Evaluation(ValueProvider);
			return new List<R>
			{
				(R) evaluation.TryDoCTFEOrGetValueRefs(
					AmbiguousType.Get(overloads),
					acc.AccessExpression,
					new List<ISymbolValue> {baseExpression as ISymbolValue})
			};
		}

		ISymbolValue EvalForeExpression(PostfixExpression ex)
		{
			var foreValue = ex.PostfixForeExpression?.Accept(this);

			if (readonlyEvaluation && foreValue is VariableValue variableValue)
				return EvaluateVariableValue(variableValue);

			return foreValue;
		}

		public ISymbolValue Visit(PostfixExpression_Access ex)
		{
			var r = EvalPostfixAccessExpression(this, ctxt, ex, null, true, ValueProvider:evaluationState);
			ctxt.CheckForSingleResult(r, ex);

			return r != null && r.Count > 0 ? r[0] : null;
		}

		public ISymbolValue Visit(PostfixExpression_Increment x)
		{
			var foreExpr = EvalForeExpression(x);

			if (readonlyEvaluation)
				EvalError(new NoConstException(x));
			// Must be implemented anyway regarding ctfe/ Op overloading
			return null;
		}

		public ISymbolValue Visit(PostfixExpression_Decrement x)
		{
			var foreExpr = EvalForeExpression(x);

			if (readonlyEvaluation)
				EvalError(new NoConstException(x));
			// Must be implemented anyway regarding ctfe
			return null;
		}

		public ISymbolValue Visit(PostfixExpression_ArrayAccess x)
		{
			var foreExpression = EvalForeExpression(x);

			if(x.Arguments != null)
				foreach (var arg in x.Arguments) {
					if (arg == null)
						continue;

					if (arg is PostfixExpression_ArrayAccess.SliceArgument)
						foreExpression = SliceArray (x, foreExpression, arg as PostfixExpression_ArrayAccess.SliceArgument);
					else
						foreExpression = AccessArrayAtIndex (x, foreExpression, arg);

					if (foreExpression == null)
						return null;
				}

			return foreExpression;
		}

		private int currentArrayLength = -1;

		ISymbolValue AccessArrayAtIndex(PostfixExpression_ArrayAccess x, ISymbolValue foreExpression, PostfixExpression_ArrayAccess.IndexArgument ix)
		{
			//TODO: Access pointer arrays(?)

			if (foreExpression is ArrayValue av) // ArrayValue must be checked first due to inheritance!
			{
				// Make $ operand available
				var arrLen_Backup = currentArrayLength;
				currentArrayLength = av.Length;

				var n = ix.Expression.Accept(this) as PrimitiveValue;

				currentArrayLength = arrLen_Backup;

				if (n == null)
				{
					EvalError(ix.Expression, "Returned no value");
					return null;
				}

				int i;
				try
				{
					i = Convert.ToInt32(n.Value);
				}
				catch
				{
					EvalError(ix.Expression, "Index expression must be of type int");
					return null;
				}

				if (i < 0 || i > av.Length)
				{
					EvalError(ix.Expression, "Index out of range - it must be between 0 and " + av.Length);
					return null;
				}

				if (av.IsString)
				{
					char c = av.StringValue[i];
					return new PrimitiveValue(c, (PrimitiveType)(av.RepresentedType as ArrayType).ValueType);
				}
				else return av.Elements[i];
			}
			else if (foreExpression is AssociativeArrayValue aa)
			{
				var key = ix.Expression.Accept(this) as PrimitiveValue;

				if (key == null)
				{
					EvalError(ix.Expression, "Returned no value");
					return null;
				}

				ISymbolValue val = null;

				foreach (var kv in aa.Elements)
					if (kv.Key.Equals(key))
						return kv.Value;

				EvalError(x, "Could not find key '" + val + "'");
				return null;
			}

			//TODO: myClassWithAliasThis[0] -- Valid!!

			EvalError(x.PostfixForeExpression, "Invalid index expression base value type", foreExpression);
			return null;
		}

		ISymbolValue SliceArray(IExpression x,ISymbolValue foreExpression, PostfixExpression_ArrayAccess.SliceArgument sl)
		{
			if (!(foreExpression is ArrayValue))
			{
				EvalError(x, "Must be an array");
				return null;
			}

			var ar = (ArrayValue)foreExpression;

			// If the [ ] form is used, the slice is of the entire array.
			if (sl.LowerBoundExpression == null && sl.UpperBoundExpression == null)
				//TODO: Clone it or append an item or so
				return foreExpression;

			// Make $ operand available
			var arrLen_Backup = currentArrayLength;
			var len = ar.Length;
			currentArrayLength = len;

			//TODO: Strip aliases and whatever things may break this
			var bound_lower = sl.LowerBoundExpression.Accept(this) as PrimitiveValue;
			var bound_upper = sl.UpperBoundExpression.Accept(this) as PrimitiveValue;

			currentArrayLength = arrLen_Backup;

			if (bound_lower == null || bound_upper == null)
			{
				EvalError(bound_lower == null ? sl.LowerBoundExpression : sl.UpperBoundExpression, "Must be of an integral type");
				return null;
			}

			int lower = -1, upper = -1;
			try
			{
				lower = Convert.ToInt32(bound_lower.Value);
				upper = Convert.ToInt32(bound_upper.Value);
			}
			catch
			{
				EvalError(lower != -1 ? sl.LowerBoundExpression : sl.UpperBoundExpression, "Boundary expression must base an integral type");
				return null;
			}

			if (lower < 0)
			{
				EvalError(sl.LowerBoundExpression, "Lower boundary must be greater than 0"); return new NullValue(ar.RepresentedType);
			}
			if (lower >= len && len > 0)
			{
				EvalError(sl.LowerBoundExpression, "Lower boundary must be smaller than " + len); return new NullValue(ar.RepresentedType);
			}
			if (upper < lower)
			{
				EvalError(sl.UpperBoundExpression, "Upper boundary must be greater than " + lower); return new NullValue(ar.RepresentedType);
			}
			else if (upper > len)
			{
				EvalError(sl.UpperBoundExpression, "Upper boundary must be smaller than " + len); return new NullValue(ar.RepresentedType);
			}

			if (ar.IsString)
				return new ArrayValue(ar.RepresentedType as ArrayType, ar.StringValue.Substring(lower, upper - lower));

			var rawArraySlice = new ISymbolValue[upper - lower];
			int j = 0;
			for (int i = lower; i < upper; i++)
				rawArraySlice[j++] = ar.Elements[i];

			return new ArrayValue(ar.RepresentedType as ArrayType, rawArraySlice);
		}
	}
}
