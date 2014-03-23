using D_Parser.Dom;
using D_Parser.Resolver.ExpressionSemantics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace D_Parser.Resolver.ResolutionHooks
{
	public static class HookRegistry
	{
		static readonly Dictionary<string, IHook> DeductionHooks = new Dictionary<string, IHook>();

		/// <summary>
		/// For persisting the overall weakly referenced DNodes, store the containing AbstractType - and as soon as this type is getting free'd, its DNode will be either!
		/// </summary>
		static ConditionalWeakTable<AbstractType, INode> resultStore = new ConditionalWeakTable<AbstractType, INode>();

		public static void AddHook(IHook hook)
		{
			DeductionHooks[hook.HookedSymbol] = hook;
		}

		public static bool RemoveHook(string hookedSymbol)
		{
			return DeductionHooks.Remove(hookedSymbol);
		}

		static HookRegistry()
		{
			AddHook(new TupleHook());
			AddHook(new bitfields());
		}

		public static AbstractType TryDeduce(DSymbol t, IEnumerable<ISemantic> templateArguments, out bool supersedeOtherOverloads)
		{
			var def = t.Definition;
			IHook hook;
			if (def != null && DeductionHooks.TryGetValue(AbstractNode.GetNodePath(def, true), out hook))
			{
				supersedeOtherOverloads = hook.SupersedesMultipleOverloads;
				INode n = null;
				var res = hook.TryDeduce(t, templateArguments, ref n);
				if(n != null)
					resultStore.Add(res, n);
				return res;
			}

			supersedeOtherOverloads = true;
			return null;
		}
	}
}
