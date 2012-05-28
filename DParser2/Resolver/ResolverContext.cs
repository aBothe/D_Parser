using D_Parser.Dom;
using D_Parser.Dom.Statements;
using System.Collections.Generic;

namespace D_Parser.Resolver
{
	public class ResolverContext
	{
		public IBlockNode ScopedBlock;
		public IStatement ScopedStatement;

		/// <summary>
		/// This dictionary stores symbols that are absolutely preferred in any scope.
		/// Used e.g. for template parameter deduction.
		/// </summary>
		public Dictionary<string, ResolveResult[]> PreferredLocals = new Dictionary<string, ResolveResult[]>();

		public ResolutionOptions Options = ResolutionOptions.Default;

		public void ApplyFrom(ResolverContext other)
		{
			if (other == null)
				return;

			//foreach (var kv in other.PreferredLocals)	PreferredLocals.Add(kv.Key, kv.Value);
			ScopedBlock = other.ScopedBlock;
			ScopedStatement = other.ScopedStatement;
			Options = other.Options;
		}
	}

}
