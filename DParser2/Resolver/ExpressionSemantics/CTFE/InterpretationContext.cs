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
				throw new CtfeException("Variable "+variable.ToString()+" not set yet!");
			}
			set
			{
				if (variable == null)
					throw new CtfeException("Can't set non-existent variable");
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
