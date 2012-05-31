using System;

namespace D_Parser.Resolver
{
	[Flags]
	public enum ResolutionOptions
	{
		ResolveAliases=1,
		ResolveBaseClasses=2,

		/// <summary>
		/// Stops resolution if first match has been found
		/// </summary>
		StopAfterFirstMatch=4,

		/// <summary>
		/// Stops resolution at the end of a match's block if first match has been found.
		/// This will still resolve possible overloads but stops after leaving the overloads' scope.
		/// </summary>
		StopAfterFirstOverloads = StopAfterFirstMatch + 8,

		/// <summary>
		/// If set, the resolver won't filter out members by template parameter deduction.
		/// </summary>
		NoTemplateParameterDeduction= 16,

		Default = ResolveAliases | ResolveBaseClasses
	}
}
