using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Resolver
{
	public class ResolverContext
	{
		public IBlockNode ScopedBlock;
		public IStatement ScopedStatement;

		public void IntroduceTemplateParameterTypes(TemplateIntermediateType tir)
		{
			if(tir!=null && tir.DeducedTypes != null)
				foreach (var dt in tir.DeducedTypes)
					DeducedTemplateParameters[dt.Key] = dt.Value;
		}

		public void RemoveParamTypesFromPreferredLocas(TemplateIntermediateType tir)
		{
			if (tir != null && tir.DeducedTypes != null)
				foreach (var dt in tir.DeducedTypes)
					DeducedTemplateParameters.Remove(dt.Key);
		}

		public Dictionary<string, ISemantic> DeducedTemplateParameters = new Dictionary<string,ISemantic>();

		//TODO: Cache expression results to increase static if() performance if multiple items are affected by them

		public ResolutionOptions ContextDependentOptions = 0;

		public void ApplyFrom(ResolverContext other)
		{
			if (other == null)
				return;

			ScopedBlock = other.ScopedBlock;
			ScopedStatement = other.ScopedStatement;
		}
	}

}
