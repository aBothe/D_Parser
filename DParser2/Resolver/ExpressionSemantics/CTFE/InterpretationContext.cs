using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver.ExpressionSemantics.CTFE
{
	class InterpretationContext : AbstractSymbolValueProvider
	{
		#region Properties
		// Storage for already resolved symbols or such

		readonly Dictionary<DVariable, ISymbolValue> Locals = new Dictionary<DVariable, ISymbolValue>();
		#endregion

		#region Constructor
		public InterpretationContext(AbstractSymbolValueProvider baseValueProvider) : base(baseValueProvider.ResolutionContext)
		{
			var ic = baseValueProvider as InterpretationContext;
			if (ic != null)
			{
				// Copy over already resolved types and parent template symbols etc.
			}
		}
		#endregion

		public override ISymbolValue this[DVariable variable]
		{
			get
			{
				ISymbolValue v;
				if (Locals.TryGetValue(variable, out v))
					return v;

				// Assign a default value to the variable
				var t = TypeResolution.TypeDeclarationResolver.HandleNodeMatch(variable, base.ResolutionContext) as MemberSymbol;
				if (t != null)
				{
					if (t.Base is PrimitiveType)
						v= new PrimitiveValue(0M, t.Base as PrimitiveType);
					else
						v = new NullValue(t.Base);
				}
				else
					v = new NullValue();

				Locals[variable] = v;

				return v;
			}
			set
			{
				if (variable == null)
					throw new CtfeException("variable must not be null");
				Locals[variable] = value;
			}
		}

		public override DVariable GetLocal(string LocalName, IdentifierExpression id = null)
		{
			foreach (var kv in Locals)
				if (kv.Key.Name == LocalName)
					return kv.Key;
			return null;
		}

		public override bool ConstantOnly{get{ return false; }set{}}
	}
}
