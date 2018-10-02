using System.Collections.Generic;
using D_Parser.Dom;
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
		ConditionalCompilation.ConditionSet declarationCondititons;
		
		public IBlockNode ScopedBlock{get{ return scopedBlock; }}
		#endregion
		
		public ContextFrame(ResolutionContext ctxt)
		{
			this.ctxt = ctxt;
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
			declarationCondititons = null;
		}

		public void Set(IBlockNode b)
		{
			scopedBlock = b;
			Caret = CodeLocation.Empty;
			declarationCondititons = null;
		}
		
		public void Set(IBlockNode b, CodeLocation caret)
		{
			scopedBlock = b;
			Caret = caret;
			declarationCondititons = null;
		}

		public ConditionalCompilation.ConditionSet DeclarationConditions
		{
			get
			{
				if(declarationCondititons == null)
				{
					declarationCondititons = new ConditionalCompilation.ConditionSet(ctxt.CompilationEnvironment);
					ConditionalCompilation.EnumConditions(declarationCondititons, scopedBlock, ctxt, Caret);
				}
				return declarationCondititons;
			}
		}

		/// <summary>
		/// Returns true if a node fully matches its environment concerning static if() declaration constraints and version/debug() restrictions.
		/// </summary>
		public bool MatchesDeclarationEnvironment(IEnumerable<DAttribute> conditions)
		{
			return DeclarationConditions.IsMatching(conditions,ctxt);
		}
		
		public bool MatchesDeclarationEnvironment(DeclarationCondition dc)
		{
			return DeclarationConditions.IsMatching(dc,ctxt);
		}

		public override string ToString()
		{
			return scopedBlock.ToString() + " // " + Caret.ToString();
		}
	}
}
