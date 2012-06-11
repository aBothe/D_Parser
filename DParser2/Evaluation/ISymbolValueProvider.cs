using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Resolver;
using D_Parser.Dom;

namespace D_Parser.Evaluation
{
	public interface ISymbolValueProvider
	{
		ResolverContextStack ResolutionContext { get; }
		bool IsSet(string name);
		ISymbolValue this[string LocalName] { get;set; }

		bool ConstantOnly { get; }
		void LogError(ISyntaxRegion involvedSyntaxObject, string msg, bool isWarning=false);
	}

	/// <summary>
	/// This provider is used for constant values evaluation.
	/// 'Locals' aren't provided whereas requesting a variable's constant
	/// </summary>
	public class StandardValueProvider : ISymbolValueProvider
	{
		public ResolverContextStack ResolutionContext
		{
			get;
			private set;
		}

		public bool IsSet(string name)
		{
			// Search along the resolution context to find locals/template parameters/(constant/enum) literals
			return false;
			//throw new NotImplementedException();
		}

		public ISymbolValue this[string LocalName]
		{
			get
			{
				return null;
				//throw new NotImplementedException();
			}
			set
			{
				// Shouldn't be supported since consts are theoretically immutable
			}
		}

		public StandardValueProvider(ResolverContextStack ctxt)
		{
			ResolutionContext = ctxt;
		}



		public bool ConstantOnly
		{
			get { return true; }
		}

		public void LogError(ISyntaxRegion involvedSyntaxObject, string msg, bool isWarning = false)
		{
			//TODO: Handle semantic errors that occur during analysis
		}
	}
}
