using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;

namespace D_Parser.Resolver.Templates
{
	public class TemplateParameterDeduction
	{
		#region Properties / ctor
		/// <summary>
		/// The dictionary which stores all deduced results + their names
		/// </summary>
		Dictionary<string, ResolveResult[]> TargetDictionary;

		public TemplateParameterDeduction(Dictionary<string, ResolveResult[]> DeducedParameters)
		{
			this.TargetDictionary = DeducedParameters;
		}
		#endregion

		public bool Handle(ITemplateParameter parameter, ResolveResult argumentToAnalyze)
		{
			if (parameter is TemplateAliasParameter)
				return Handle((TemplateAliasParameter)parameter, argumentToAnalyze);
			else if (parameter is TemplateThisParameter)
				return Handle((TemplateThisParameter)parameter, argumentToAnalyze);
			else if(parameter is TemplateTypeParameter)
				return Handle((TemplateTypeParameter)parameter,argumentToAnalyze);
			else if(parameter is TemplateValueParameter)
				return Handle((TemplateValueParameter)parameter,argumentToAnalyze);
			return false;
		}

		public bool Handle(TemplateAliasParameter p, ResolveResult arg)
		{
			return false;
		}

		public bool Handle(TemplateThisParameter p, ResolveResult arg)
		{
			return false;
		}

		public bool Handle(TemplateTupleParameter p, IEnumerable<ResolveResult[]> arguments)
		{
			return false;
		}

		public bool Handle(TemplateValueParameter p, ResolveResult arg)
		{
			return false;
		}

		public bool Handle(TemplateTypeParameter p, ResolveResult arg)
		{
			return false;
		}
	}
}
