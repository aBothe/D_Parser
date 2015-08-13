using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using System.Collections.ObjectModel;

namespace D_Parser.Resolver.Templates
{
	public class DeducedTypeDictionary : Dictionary<TemplateParameter,TemplateParameterSymbol>, IEnumerable<TemplateParameterSymbol>	{

		public readonly TemplateParameter[] ExpectedParameters;

		public DeducedTypeDictionary() { }

		public DeducedTypeDictionary(TemplateParameter[] parameters)
		{
			ExpectedParameters = parameters;

			if(parameters != null)
				foreach (var tp in parameters)
					Add(tp, null);
		}

		public DeducedTypeDictionary(DNode owner)
			: this(owner.TemplateParameters)
		{}

		public DeducedTypeDictionary(DSymbol ms) : this(ms.ValidSymbol ? ms.Definition.TemplateParameters : null)
		{
			Add (ms.DeducedTypes);
			if (ms is TemplateParameterSymbol)
				Remove ((ms as TemplateParameterSymbol).Parameter);
		}

		public void Remove(IEnumerable<TemplateParameterSymbol> tpss)
		{
			if (tpss == null)
				return;

			foreach (var tps in tpss)
				Remove (tps.Parameter);
		}

		public void AddDefault(IEnumerable<TemplateParameter> templateParameters)
		{
			if (templateParameters == null)
				return;
			
			foreach (var tp in templateParameters)
				if (this [tp] == null)
					this [tp] = new TemplateParameterSymbol (tp, null);
		}

		public void Add(IEnumerable<TemplateParameterSymbol> tpss)
		{
			if (tpss == null)
				return;

			foreach (var tps in tpss)
				this [tps.Parameter] = tps;
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

		IEnumerator<TemplateParameterSymbol> IEnumerable<TemplateParameterSymbol>.GetEnumerator ()
		{
			return Values.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return Values.GetEnumerator ();
		}
	}
}
