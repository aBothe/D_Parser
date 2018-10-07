using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver.ExpressionSemantics.CTFE
{
	class InterpretationContext : StandardValueProvider
	{
		#region Properties
		// Storage for already resolved symbols or such

		readonly Dictionary<DVariable, ISymbolValue> Locals = new Dictionary<DVariable, ISymbolValue>();
		#endregion

		public InterpretationContext(AbstractSymbolValueProvider baseValueProvider) : base(baseValueProvider.ResolutionContext)
		{
			var ic = baseValueProvider as InterpretationContext;
			if (ic != null)
			{
				// Copy over already resolved types and parent template symbols etc.
			}
		}

		public override ISymbolValue this[DVariable variable]
		{
			get
			{
				ISymbolValue v;
				if (Locals.TryGetValue(variable, out v))
					return v;

				// Assign a default value to the variable
				var variableBaseType =
					TypeResolution.DSymbolBaseTypeResolver.ResolveDVariableBaseType(variable, ResolutionContext, true);
				if (variableBaseType != null)
				{
					if (variableBaseType is PrimitiveType type)
						v= new PrimitiveValue(0M, type);
					else
						v = new NullValue(variableBaseType);
				}
				else
					v = new NullValue();

				this[variable] = v;

				return v;
			}
			set
			{
				if (variable == null)
					throw new CtfeException("variable must not be null");
				Locals[variable] = value;
			}
		}

		public override bool Readonly { get; } = false;
	}
}
