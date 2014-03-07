using D_Parser.Dom;
using D_Parser.Resolver.ExpressionSemantics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver.ResolutionHooks
{
	static class HookRegistry
	{
		static readonly Dictionary<string, IHook> DeductionHooks = new Dictionary<string, IHook>();
		static void AddHook(IHook hook)
		{
			DeductionHooks[hook.HookedSymbol] = hook;
		}

		static HookRegistry()
		{
			AddHook(new TupleHook());
		}

		public static AbstractType TryDeduce(DSymbol t, IEnumerable<ISemantic> templateArguments, out bool supersedeOtherOverloads)
		{
			var def = t.Definition;
			IHook hook;
			if (def != null && DeductionHooks.TryGetValue(AbstractNode.GetNodePath(def, true), out hook))
			{
				supersedeOtherOverloads = hook.SupersedesMultipleOverloads;
				return hook.TryDeduce(t, templateArguments);
			}

			supersedeOtherOverloads = true;
			return null;
		}

		public static ISymbolValue TryEvaluate(DSymbol t, IEnumerable<ISemantic> templateArgs)
		{
			return null;
		}
	}
}
