using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;

namespace D_Parser.Resolver
{
	public class ConditionalCompilation
	{
		public static bool IsMatching(IDeclarationCondition cond, ResolutionContext ctxt)
		{
			return true;
		}
	}
}
