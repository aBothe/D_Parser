using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.Templates;

namespace D_Parser.Resolver
{
	public class ContextFrame
	{
		#region Properties
		ResolutionContext ctxt;
		public DeducedTypeDictionary DeducedTemplateParameters = new DeducedTypeDictionary();
		public ResolutionOptions ContextDependentOptions = 0;
		IBlockNode scopedBlock;
		public CodeLocation Caret { get; protected set; }
		public readonly ConditionalCompilation.ConditionSet DeclarationCondititons;
		
		public IBlockNode ScopedBlock{get{ return scopedBlock; }}
		#endregion
		
		public ContextFrame(ResolutionContext ctxt)
		{
			this.ctxt = ctxt;
			DeclarationCondititons = new ConditionalCompilation.ConditionSet(ctxt.CompilationEnvironment);
		}
		
		public void IntroduceTemplateParameterTypes(DSymbol tir)
		{
			if (tir != null)
				DeducedTemplateParameters.Add (tir.DeducedTypes);
		}

		public void RemoveParamTypesFromPreferredLocals(DSymbol tir)
		{
			if (tir != null)
				DeducedTemplateParameters.Remove (tir.DeducedTypes);
		}

		public void Set(CodeLocation caret)
		{
			Caret = caret;
			ConditionalCompilation.EnumConditions(DeclarationCondititons, scopedBlock, ctxt, caret); 
		}

		public void Set(IBlockNode b)
		{
			scopedBlock = b;
			Caret = CodeLocation.Empty;

			ConditionalCompilation.EnumConditions(DeclarationCondititons, b, ctxt, CodeLocation.Empty);
		}
		
		public void Set(IBlockNode b, CodeLocation caret)
		{
			scopedBlock = b;
			Caret = caret;
			
			ConditionalCompilation.EnumConditions(DeclarationCondititons, b, ctxt, caret); 
		}
		
		/// <summary>
		/// Returns true if a node fully matches its environment concerning static if() declaration constraints and version/debug() restrictions.
		/// </summary>
		public bool MatchesDeclarationEnvironment(IEnumerable<DAttribute> conditions)
		{
			return DeclarationCondititons.IsMatching(conditions,ctxt);
		}
		
		public bool MatchesDeclarationEnvironment(DeclarationCondition dc)
		{
			return DeclarationCondititons.IsMatching(dc,ctxt);
		}

		public override string ToString()
		{
			return scopedBlock.ToString() + " // " + Caret.ToString();
		}
	}
}
