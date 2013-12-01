using System;

namespace D_Parser.Resolver
{
	[Flags]
	public enum ResolutionOptions
	{
		DontResolveAliases=1,
		/// <summary>
		/// If passed, base classes will not be resolved in any way.
		/// </summary>
		DontResolveBaseClasses= 2,
		/// <summary>
		/// If passed, variable/method return types will not be evaluated. 
		/// </summary>
		DontResolveBaseTypes = 4,

		/// <summary>
		/// If set, the resolver won't filter out members by template parameter deduction.
		/// </summary>
		NoTemplateParameterDeduction= 8,

		ReturnMethodReferencesOnly = 16,
		
		IgnoreAllProtectionAttributes = 32,
		IgnoreDeclarationConditions = 64,

		Default = 0
	}
}
