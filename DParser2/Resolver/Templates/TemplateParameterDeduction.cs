using D_Parser.Dom;

namespace D_Parser.Resolver.Templates
{
	public class TemplateParameterDeduction
	{
		readonly TemplateParameterDeductionVisitor deductionVisitor;

		/// <summary>
		/// If true and deducing a type parameter,
		/// the equality of the given and expected type is required instead of their simple convertibility.
		/// Used when evaluating IsExpressions.
		/// </summary>
		public bool EnforceTypeEqualityWhenDeducing
		{
			get => deductionVisitor.EnforceTypeEqualityWhenDeducing;
			set => deductionVisitor.EnforceTypeEqualityWhenDeducing = value;
		}

		[System.Diagnostics.DebuggerStepThrough]
		public TemplateParameterDeduction(DeducedTypeDictionary DeducedParameters, ResolutionContext ctxt)
		{
			deductionVisitor = new TemplateParameterDeductionVisitor(ctxt, DeducedParameters);
		}

		public bool Handle(TemplateParameter parameter, ISemantic argumentToAnalyze)
		{
			// Packages aren't allowed at all
			if (argumentToAnalyze is PackageSymbol)
				return false;

			// Module symbols can be used as alias only
			if (argumentToAnalyze is ModuleSymbol &&
				!(parameter is TemplateAliasParameter))
				return false;

			//TODO: Handle __FILE__ and __LINE__ correctly - so don't evaluate them at the template declaration but at the point of instantiation
			var ctxt = deductionVisitor.ctxt;

			/*
			 * Introduce previously deduced parameters into current resolution context
			 * to allow value parameter to be of e.g. type T whereas T is already set somewhere before 
			 */
			DeducedTypeDictionary _prefLocalsBackup = null;
			if (ctxt != null && ctxt.CurrentContext != null)
			{
				_prefLocalsBackup = ctxt.CurrentContext.DeducedTemplateParameters;

				var d = new DeducedTypeDictionary();
				foreach (var kv in deductionVisitor.TargetDictionary)
					if (kv.Value != null)
						d[kv.Key] = kv.Value;
				ctxt.CurrentContext.DeducedTemplateParameters = d;
			}

			bool res = parameter.Accept(deductionVisitor, argumentToAnalyze);

			if (ctxt != null && ctxt.CurrentContext != null)
				ctxt.CurrentContext.DeducedTemplateParameters = _prefLocalsBackup;

			return res;
		}
	}
}
