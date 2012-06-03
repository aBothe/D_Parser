using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Resolver
{
	public class ResolverContext
	{
		public IBlockNode ScopedBlock;
		public IStatement ScopedStatement;

		public void IntroduceTemplateParameterTypes(TemplateInstanceResult tir)
		{
			foreach (var dt in tir.DeducedTypes)
				DeducedTemplateParameters[dt.Key] = dt.Value;
		}

		public void RemoveParamTypesFromPreferredLocas(TemplateInstanceResult tir)
		{
			foreach (var dt in tir.DeducedTypes)
				DeducedTemplateParameters.Remove(dt.Key);
		}

		public Dictionary<string, ResolveResult[]> DeducedTemplateParameters = new Dictionary<string,ResolveResult[]>();

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
