using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using D_Parser.Dom;
using D_Parser.Misc.Mangling;
using D_Parser.Resolver;

namespace D_Parser.Misc
{
	public class NameMangling
	{
		public AbstractType Demangle(string mangledString, ResolutionContext ctxt, out ITypeDeclaration qualifier)
		{
			return Demangler.Demangle(mangledString, ctxt, out qualifier);
		}

		public static string Mangle(AbstractType typeToMangle)
		{
			return string.Empty;
		}
	}
}
