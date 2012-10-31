using System.Collections;
using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.Templates;

namespace D_Parser.Resolver
{
	public class ContextFrame
	{
		public IBlockNode ScopedBlock;
		public IStatement ScopedStatement;

		public void IntroduceTemplateParameterTypes(DSymbol tir)
		{
			if(tir!=null && tir.DeducedTypes != null)
				foreach (var dt in tir.DeducedTypes)
					DeducedTemplateParameters[dt.Key] = dt.Value;
		}

		public void RemoveParamTypesFromPreferredLocals(DSymbol tir)
		{
			if (tir != null && tir.DeducedTypes != null)
				foreach (var dt in tir.DeducedTypes)
					DeducedTemplateParameters.Remove(dt.Key);
		}

		public DeducedTypeDictionary DeducedTemplateParameters = new DeducedTypeDictionary();

		//TODO: Cache expression results to increase static if() performance if multiple items are affected by them
		public List<DeclarationCondition> CurrentDeclarationConditions
		{
			get {
				return ConditionalCompilation.EnumConditions(ScopedStatement, ScopedBlock);
			}
		}

		public ResolutionOptions ContextDependentOptions = 0;

		public override string ToString()
		{
			return ScopedBlock.ToString() + " // " + (ScopedStatement == null ? "" : ScopedStatement.ToString());
		}
	}

}
