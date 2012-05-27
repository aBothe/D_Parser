using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Expressions;

namespace D_Parser.Resolver.Templates
{
	partial class TemplateParameterDeduction
	{
		public bool Handle(TemplateAliasParameter p, ResolveResult arg)
		{
			Set(p.Name, arg);

			return true;
		}
	}
}
