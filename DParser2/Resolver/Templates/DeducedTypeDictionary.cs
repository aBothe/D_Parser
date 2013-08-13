using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Resolver.ExpressionSemantics;
using System.Collections.ObjectModel;

namespace D_Parser.Resolver.Templates
{
	public class DeducedTypeDictionary : Dictionary<int,TemplateParameterSymbol>	{

		public readonly TemplateParameter[] ExpectedParameters;

		public DeducedTypeDictionary() { }

		public DeducedTypeDictionary(TemplateParameter[] parameters)
		{
			ExpectedParameters = parameters;
			if (parameters != null)
			{
				foreach (var tpar in parameters)
					this.Add(tpar.NameHash,null);
			}
		}

		public DeducedTypeDictionary(DNode owner)
			: this(owner.TemplateParameters)
		{}

		public DeducedTypeDictionary(DSymbol ms)
		{
			ExpectedParameters = ms.Definition.TemplateParameters;

			if (ms.DeducedTypes != null)
				foreach (var i in ms.DeducedTypes)
					this.Add(i.NameHash,i);
		}
		/*
		public DeducedTypeDictionary(IEnumerable<TemplateParameterSymbol> l, DNode parameterOwner)
		{
			ParameterOwner = parameterOwner;
			if (parameterOwner != null)
				ExpectedParameters = parameterOwner.TemplateParameters;

			if (l != null)
				foreach (var i in l)
					this[i.Name] = i;
		}*/

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

		public new TemplateParameterSymbol this[int nameHash]
		{
			get{
				TemplateParameterSymbol tps;
				base.TryGetValue (nameHash, out tps);
				return tps;
			}
			set{
				base.Remove (nameHash);
				base.Add (nameHash, value);
			}
		}
	}
}
