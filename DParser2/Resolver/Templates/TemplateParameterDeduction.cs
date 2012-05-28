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
		Dictionary<string, ResolveResult[]> TargetDictionary;
		
		/// <summary>
		/// Needed for resolving default types
		/// </summary>
		ResolverContextStack ctxt;

		public TemplateParameterDeduction(Dictionary<string, ResolveResult[]> DeducedParameters, ResolverContextStack ctxt)
		{
			this.ctxt = ctxt;
			this.TargetDictionary = DeducedParameters;
		}
		#endregion

		public bool Handle(ITemplateParameter parameter, ResolveResult argumentToAnalyze)
		{
			if (parameter is TemplateAliasParameter)
				return HandleWithAlreadyDeductedParamIntroduction(parameter, argumentToAnalyze);
			else if (parameter is TemplateThisParameter)
				return Handle((TemplateThisParameter)parameter, argumentToAnalyze);
			else if(parameter is TemplateTypeParameter)
				return Handle((TemplateTypeParameter)parameter,argumentToAnalyze);
			else if(parameter is TemplateValueParameter)
				return HandleWithAlreadyDeductedParamIntroduction(parameter,argumentToAnalyze);
			return false;
		}

		public bool Handle(TemplateThisParameter p, ResolveResult arg)
		{
			// Only special handling required for method calls
			return Handle(p.FollowParameter,arg);
		}

		public bool Handle(TemplateTupleParameter p, IEnumerable<ResolveResult[]> arguments)
		{
			if (arguments == null)
				return false;

			var args= arguments.ToArray();

			if (args.Length < 2)
				return false;

			Set(p.Name, new TypeTupleResult { 
				TupleParameter=p,
				TupleItems=args 
			});

			return true;
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
			ResolveResult[] rl=null;
			if (!TargetDictionary.TryGetValue(parameterName, out rl) || rl == null)
			{
				TargetDictionary[parameterName] = new[] { r };
				return true;
			}
			else
			{
				if (rl.Length == 1 && ResultComparer.IsEqual(rl[0], r))
						return true;

				var newArr = new ResolveResult[rl.Length + 1];
				rl.CopyTo(newArr, 0);
				newArr[rl.Length] = r;

				TargetDictionary[parameterName] = newArr;
				return false;
			}
		}
	}
}
