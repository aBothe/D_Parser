using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Resolver;
using D_Parser.Dom;
using D_Parser.Evaluation.Exceptions;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Expressions;

namespace D_Parser.Evaluation
{
	public interface ISymbolValueProvider
	{
		ResolverContextStack ResolutionContext { get; }
		ISymbolValue this[string LocalName] { get; set; }
		ISymbolValue this[DVariable variable] { get; set; }

		bool ConstantOnly { get; set; }
		void LogError(ISyntaxRegion involvedSyntaxObject, string msg, bool isWarning=false);

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
		int CurrentArrayLength { get; set; }
	}

	/// <summary>
	/// This provider is used for constant values evaluation.
	/// 'Locals' aren't provided whereas requesting a variable's constant
	/// </summary>
	public class StandardValueProvider : ISymbolValueProvider
	{
		public ResolverContextStack ResolutionContext
		{
			get;
			private set;
		}

		public StandardValueProvider(ResolverContextStack ctxt)
		{
			ResolutionContext = ctxt;
		}



		public bool ConstantOnly
		{
			get { return true; }
			set { }
		}

		public void LogError(ISyntaxRegion involvedSyntaxObject, string msg, bool isWarning = false)
		{
			//TODO: Handle semantic errors that occur during analysis
		}


		public int CurrentArrayLength
		{
			get;
			set;
		}

		public ISymbolValue this[string LocalName]
		{
			get
			{
				var res = TypeDeclarationResolver.ResolveIdentifier(LocalName, ResolutionContext, null);

				if (res == null || res.Length == 0)
					return null;

				var r = res[0];

				if (r is MemberSymbol)
				{
					var mr = (MemberSymbol)r;

					if (mr.Definition is DVariable)
						return this[(DVariable)mr.Definition];
				}
				else if (r is UserDefinedType)
					return new TypeValue((AbstractType)r, new IdentifierExpression(LocalName, Parser.LiteralFormat.None));

				return null;
			}
			set
			{
				throw new NotImplementedException();
				// Shouldn't be supported since consts are theoretically immutable
			}
		}

		public ISymbolValue this[DVariable n]
		{
			get
			{
				if (n != null && n.IsConst)
				{
					// .. resolve it's pre-compile time value and make the returned value the given argument
					var val = ExpressionEvaluator.Evaluate(n.Initializer, this);

					// If it's null, then the initializer is null - which is equal to e.g. 0 or null !;

					if (val != null)
						return val;

					throw new EvaluationException(n.Initializer, "Initializer must be constant");
				}

				throw new EvaluationException(n.Initializer, "Variable must be constant.");
			}
			set
			{
				throw new NotImplementedException();
			}
		}
	}
}
