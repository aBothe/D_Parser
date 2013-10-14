using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.Templates
{
	/// <summary>
	/// http://dlang.org/template.html
	/// see 'Implicit Template Properties'
	/// </summary>
	internal class ImplicitTemplateProperties
	{
		/// <summary>
		/// Returns true if dc contains exclusively children that are named like dc.
		/// </summary>
		public static bool ContainsEquallyNamedChildrenOnly(DClassLike dc)
		{
			//Edit: The actual dmd implementation differs from the spec immensely:
			// It's only required that there are items called like dc, nothing else.
			return dc != null && (dc[dc.Name] != null || dc.StaticStatements.Count != 0); //HACK: There might be mixins that build up items called exactly like the parent template..
		}

		public static bool TryGetImplicitProperty(TemplateType template, ResolutionContext ctxt, out AbstractType[] matchingChild)
		{
			// Check if there are only children that are named as the parent template.
			// That's the requirement for the special treatment.
			matchingChild = null;
			if (!ContainsEquallyNamedChildrenOnly(template.Definition))
				return false;

			// Prepare a new context
			bool pop = !ctxt.ScopedBlockIsInNodeHierarchy(template.Definition);
			if (pop)
				ctxt.PushNewScope(template.Definition);

			// Introduce the deduced params to the current resolution context
			ctxt.CurrentContext.IntroduceTemplateParameterTypes(template);

			// Get actual overloads
			matchingChild = TypeDeclarationResolver.ResolveFurtherTypeIdentifier( template.NameHash, new[]{ template }, ctxt);
			
			// Undo context-related changes
			if (pop)
				ctxt.Pop();
			else
				ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(template);

			return matchingChild != null && matchingChild.Length == 1 && matchingChild[0] != null;
		}
	}
}
