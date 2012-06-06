using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Resolver;

namespace D_Parser.Evaluation
{
	interface IVariableStorage
	{
		ResolverContextStack ResolutionContext { get; }
		bool IsSet(string name);
		object Get(string name);
		void Set(string name, object value);
	}
}
