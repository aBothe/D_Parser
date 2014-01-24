using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using System.Collections.ObjectModel;

namespace D_Parser.Resolver.Templates
{
	public class DeducedTypeDictionary : Dictionary<TemplateParameter,TemplateParameterSymbol>	{

		public readonly TemplateParameter[] ExpectedParameters;

		public DeducedTypeDictionary() { }

		public DeducedTypeDictionary(TemplateParameter[] parameters)
		{
			ExpectedParameters = parameters;
			if (parameters != null)
			{
				foreach (var tpar in parameters)
					this.Add(tpar,null);
			}
		}

		public DeducedTypeDictionary(DNode owner)
			: this(owner.TemplateParameters)
		{}

		public DeducedTypeDictionary(DSymbol ms) : this(ms.ValidSymbol ? ms.Definition.TemplateParameters : null)
		{
			if (ms.DeducedTypes != null)
				foreach (var i in ms.DeducedTypes)
					this [i.Parameter] = i;
		}

		public ReadOnlyCollection<TemplateParameterSymbol> ToReadonly()
		{
			return new ReadOnlyCollection<TemplateParameterSymbol>(this.Values.ToList());
		}

		public bool AllParamatersSatisfied
		{
			get
			{
				foreach (var kv in this)
					if (kv.Value == null)
						return false;

				return true;
			}
		}

		public override string ToString ()
		{
			var sb = new StringBuilder ("DeducedTypeDict: ");

			foreach (var kv in this)
				sb.Append (kv.Key.Name).Append (": ").Append(kv.Value != null ? kv.Value.ToString() : "-").Append(',');

			return sb.ToString ();
		}
	}
}
