using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Resolver.ExpressionSemantics;
using System.Collections.ObjectModel;

namespace D_Parser.Resolver.Templates
{
	public class DeducedTypeDictionary : Dictionary<string,TemplateParameterSymbol>	{

		/// <summary>
		/// Used for final template parameter symbol creation.
		/// Might be specified for better code completion because parameters can be identified with the owner node now.
		/// </summary>
		public readonly DNode ParameterOwner;
		public readonly ITemplateParameter[] ExpectedParameters;

		public DeducedTypeDictionary() { }

		public DeducedTypeDictionary(ITemplateParameter[] parameters)
		{
			ExpectedParameters = parameters;
			if (parameters != null)
			{
				foreach (var tpar in parameters)
				{
					this[tpar.Name] = null;
				}
			}
		}

		public DeducedTypeDictionary(DNode owner)
		{
			ParameterOwner = owner;
			if (owner.TemplateParameters != null)
			{
				ExpectedParameters = owner.TemplateParameters;
				foreach (var tpar in owner.TemplateParameters)
					this[tpar.Name] = null;
			}
		}
		public DeducedTypeDictionary(DSymbol ms)
		{
			ParameterOwner = ms.Definition;
			ExpectedParameters = ParameterOwner.TemplateParameters;

			if (ms.DeducedTypes != null)
				foreach (var i in ms.DeducedTypes)
					this[i.Name] = i;
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
	}
}
