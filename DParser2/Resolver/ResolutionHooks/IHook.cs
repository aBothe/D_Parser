using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver.ResolutionHooks
{
	public interface IHook
	{
		string HookedSymbol { get; }
		bool SupersedesMultipleOverloads { get; }
		AbstractType TryDeduce(DSymbol ds, IEnumerable<ISemantic> templateArguments);
	}
}
