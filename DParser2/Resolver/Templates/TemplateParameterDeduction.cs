using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Statements;

namespace D_Parser.Resolver.Templates
{
	public partial class TemplateParameterDeduction
	{
		#region Properties / ctor
		/// <summary>
		/// The dictionary which stores all deduced results + their names
		/// </summary>
		Dictionary<string, List<ResolveResult>> TargetDictionary;
		
		/// <summary>
		/// Needed for resolving default types
		/// </summary>
		ResolverContextStack ctxt;

		public TemplateParameterDeduction(Dictionary<string, List<ResolveResult>> DeducedParameters, ResolverContextStack ctxt)
		{
			this.ctxt = ctxt;
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

		/// <summary>
		/// Returns true if <param name="parameterName">parameterName</param> is expected somewhere in the template parameter list.
		/// </summary>
		bool Contains(string parameterName)
		{
			foreach (var kv in TargetDictionary)
				if (kv.Key == parameterName)
					return true;
			return false;
		}

		/// <summary>
		/// Returns false if the item has already been set before and if the already set item is not equal to 'r'.
		/// Inserts 'r' into the target dictionary and returns true otherwise.
		/// </summary>
		bool Set(string parameterName, ResolveResult r)
		{
			List<ResolveResult> rl=null;
			if (!TargetDictionary.TryGetValue(parameterName, out rl) || rl == null)
				rl = TargetDictionary[parameterName] = new List<ResolveResult>();
			else
			{
				if (rl.Count == 1)
				{
					if (ResultComparer.IsEqual(rl[0], r))
						return true;
					else
					{
						rl.Add(r);
						return false;
					}
				}
				else if (rl.Count > 1)
				{
					rl.Add(r);
					return false;
				}
			}
			
			rl.Add(r);
			return true;
		}
	}
}
