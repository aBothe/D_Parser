using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;

namespace D_Parser.Resolver.Templates
{
	public partial class TemplateParameterDeduction
	{
		#region Properties / ctor
		/// <summary>
		/// The dictionary which stores all deduced results + their names
		/// </summary>
		DeducedTypeDictionary TargetDictionary;

		/// <summary>
		/// If true and deducing a type parameter,
		/// the equality of the given and expected type is required instead of their simple convertibility.
		/// Used when evaluating IsExpressions.
		/// </summary>
		public bool EnforceTypeEqualityWhenDeducing
		{
			get;
			set;
		}

		/// <summary>
		/// Needed for resolving default types
		/// </summary>
		ResolutionContext ctxt;

		public TemplateParameterDeduction(DeducedTypeDictionary DeducedParameters, ResolutionContext ctxt)
		{
			this.ctxt = ctxt;
			this.TargetDictionary = DeducedParameters;
		}
		#endregion

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

			/*
			 * Introduce previously deduced parameters into current resolution context
			 * to allow value parameter to be of e.g. type T whereas T is already set somewhere before 
			 */
			DeducedTypeDictionary _prefLocalsBackup = null;
			if (ctxt != null && ctxt.CurrentContext != null)
			{
				_prefLocalsBackup = ctxt.CurrentContext.DeducedTemplateParameters;

				var d = new DeducedTypeDictionary();
				foreach (var kv in TargetDictionary)
					if (kv.Value != null)
						d[kv.Key] = kv.Value;
				ctxt.CurrentContext.DeducedTemplateParameters = d;
			}

			bool res = false;

			if (parameter is TemplateAliasParameter)
				res = Handle((TemplateAliasParameter)parameter, argumentToAnalyze);
			else if (parameter is TemplateThisParameter)
				res = Handle((TemplateThisParameter)parameter, argumentToAnalyze);
			else if (parameter is TemplateTypeParameter)
				res = Handle((TemplateTypeParameter)parameter, argumentToAnalyze);
			else if (parameter is TemplateValueParameter)
				res = Handle((TemplateValueParameter)parameter, argumentToAnalyze);
			else if (parameter is TemplateTupleParameter)
				res = Handle((TemplateTupleParameter)parameter, new[] { argumentToAnalyze });

			if (ctxt != null && ctxt.CurrentContext != null)
				ctxt.CurrentContext.DeducedTemplateParameters = _prefLocalsBackup;

			return res;
		}

		bool Handle(TemplateThisParameter p, ISemantic arg)
		{
			// Only special handling required for method calls
			return Handle(p.FollowParameter,arg);
		}

		public bool Handle(TemplateTupleParameter p, IEnumerable<ISemantic> arguments)
		{
			var l = new List<ISemantic>();

			if(arguments != null)
				foreach (var arg in arguments)
					if(arg is DTuple) // If a type tuple was given already, add its items instead of the tuple itself
					{
						var tt = arg as DTuple;
						if(tt.Items != null)
							l.AddRange(tt.Items);
					}
					else
						l.Add(arg);				

			return Set(p, new DTuple(p, l.Count == 0 ? null : l), 0);
		}

		/// <summary>
		/// Returns true if <param name="parameterName">parameterName</param> is expected somewhere in the template parameter list.
		/// </summary>
		bool Contains(int parameterNameHash)
		{
			return TargetDictionary.ContainsKey (parameterNameHash);
		}

		/// <summary>
		/// Returns false if the item has already been set before and if the already set item is not equal to 'r'.
		/// Inserts 'r' into the target dictionary and returns true otherwise.
		/// </summary>
		bool Set(TemplateParameter p, ISemantic r, int nameHash)
		{
			if (p == null) {
				if (nameHash != 0 && TargetDictionary.ExpectedParameters != null) {
					foreach (var tpar in TargetDictionary.ExpectedParameters)
						if (tpar.NameHash == nameHash) {
							p = tpar;
							break;
						}
				}
			}

			if (p == null) {
				ctxt.LogError (null, "no fitting template parameter found!");
				return false;
			}

			if (nameHash == 0)
				nameHash = p.NameHash;

			// void call(T)(T t) {}
			// call(myA) -- T is *not* myA but A, so only assign myA's type to T. 
			if (p is TemplateTypeParameter)
			{
				var newR = Resolver.TypeResolution.DResolver.StripMemberSymbols(AbstractType.Get(r));
				if (newR != null)
					r = newR;
			}

			TemplateParameterSymbol rl;
			if (!TargetDictionary.TryGetValue(nameHash, out rl) || rl == null)
			{
				TargetDictionary[nameHash] = new TemplateParameterSymbol(p, r);
				return true;
			}
			else
			{
				if (ResultComparer.IsEqual(rl.Base, r))
				{
					return true;
				}
				else
				{
					// Error: Ambiguous assignment
				}

				TargetDictionary[nameHash] = new TemplateParameterSymbol(p, r);

				return false;
			}
		}
	}
}
