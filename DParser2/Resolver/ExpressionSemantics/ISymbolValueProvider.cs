using System;
using System.Collections.Generic;
using D_Parser.Resolver;
using D_Parser.Dom;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public abstract class AbstractSymbolValueProvider
	{
		public readonly ResolutionContext ResolutionContext;
		public AbstractSymbolValueProvider(ResolutionContext ctxt)
		{
			this.ResolutionContext = ctxt;
		}

		public ISymbolValue this[IdentifierExpression id]
		{
			get
			{
				return this[GetLocal(id)];
			}
			set
			{
				this[GetLocal(id)] = value;
			}
		}

		public ISymbolValue this[string LocalName]
		{
			get
			{
				return this[GetLocal(LocalName)];
			}
			set
			{
				this[GetLocal(LocalName)] = value;
			}
		}

		public abstract ISymbolValue this[DVariable variable] { get; set; }

		public DVariable GetLocal(IdentifierExpression id)
		{
			return GetLocal(id.StringValue, id);
		}

		/// <summary>
		/// Searches a local/parameter variable and returns the node
		/// </summary>
		public abstract DVariable GetLocal(string LocalName, IdentifierExpression id=null);

		public abstract bool ConstantOnly { get; set; }
		public void LogError(ISyntaxRegion involvedSyntaxObject, string msg, params ISemantic[] temporaryResults)
		{
			ResolutionContext.LogError (new EvaluationError (involvedSyntaxObject, msg, temporaryResults));
		}

		/*
		 * TODO:
		 * -- Execution stack and model
		 * -- Virtual memory allocation
		 *		(e.g. class instance will contain a dictionary with class properties etc.)
		 *		-- when executing a class' member method, the instance will be passed as 'this' reference etc.
		 */

		/// <summary>
		/// Used for $ operands inside index/slice expressions.
		/// </summary>
		public int CurrentArrayLength;
	}

	/// <summary>
	/// This provider is used for constant values evaluation.
	/// 'Locals' aren't provided whereas requesting a variable's constant
	/// </summary>
	public class StandardValueProvider : AbstractSymbolValueProvider
	{
		public StandardValueProvider(ResolutionContext ctxt) : base(ctxt)	{	}

		public override bool ConstantOnly
		{
			get => true;
			set { }
		}
		
		readonly List<DVariable> varsBeingResolved = new List<DVariable>();

		public override ISymbolValue this[DVariable n]
		{
			get
			{
				if (n == null)
					return new ErrorValue(new EvaluationException("There must be a valid variable node given in order to retrieve its value"));

				if(varsBeingResolved.Contains(n)){
					return new ErrorValue(new EvaluationException("Cannot reference itself"));
				}
				varsBeingResolved.Add(n);
				try
				{
					return EvaluateConstVariablesValue(n);
				}
				finally{
					varsBeingResolved.Remove(n);
				}
			}
			set => throw new NotImplementedException();
		}

		public override DVariable GetLocal(string localName, IdentifierExpression id=null)
		{
			var res = ExpressionTypeEvaluation.GetOverloads(id ?? new IdentifierExpression(localName), ResolutionContext, null, false);

			if (res == null || res.Count == 0)
				return null;

			var r = res[0];

			if (r is MemberSymbol mr && mr.Definition is DVariable variable)
				return variable;

			LogError(id ?? new IdentifierExpression(localName), localName + " must represent a local variable or a parameter");
			return null;
		}

		ISymbolValue EvaluateConstVariablesValue(DVariable variable)
		{
			if (!variable.IsConst)
				return new ErrorValue(new EvaluationException(variable + " must have a constant initializer"));

			if (variable is DEnumValue enumValueVariable && enumValueVariable.Initializer == null)
				return EvaluateNonInitializedEnumValue(enumValueVariable);

			return Evaluation.EvaluateValue(variable.Initializer, this);
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
			return Evaluation.EvaluateValue(incrementExpression, this);
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
	}
}
