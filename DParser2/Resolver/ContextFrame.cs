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
		readonly ConditionalCompilation.ConditionSet declarationCondititons;
		
		public IBlockNode ScopedBlock{get{ return scopedBlock; }}
		#endregion
		
		public ContextFrame(ResolutionContext ctxt, IBlockNode b, CodeLocation caret)
		{
			this.ctxt = ctxt;
			declarationCondititons = new ConditionalCompilation.ConditionSet(ctxt.CompilationEnvironment);

			ctxt.Push(this);

			Set(b, caret);
		}
		
		public void IntroduceTemplateParameterTypes(DSymbol tir)
		{
			if(tir!=null && tir.DeducedTypes != null)
				foreach (var dt in tir.DeducedTypes)
					if(dt != null)
						DeducedTemplateParameters[dt.Parameter] = dt;
		}

		public void RemoveParamTypesFromPreferredLocals(DSymbol tir)
		{
			if (tir != null && tir.DeducedTypes != null)
				foreach (var dt in tir.DeducedTypes)
					DeducedTemplateParameters.Remove(dt.Parameter);
		}

		public void Set(CodeLocation caret)
		{
			Caret = caret;
			ConditionalCompilation.EnumConditions(declarationCondititons, scopedBlock, ctxt, caret); 
		}

		public void Set(IBlockNode b)
		{
			scopedBlock = b;
			Caret = CodeLocation.Empty;

			ConditionalCompilation.EnumConditions(declarationCondititons, b, ctxt, CodeLocation.Empty);
		}
		
		public void Set(IBlockNode b, CodeLocation caret)
		{
			scopedBlock = b;
			Caret = caret;
			
			ConditionalCompilation.EnumConditions(declarationCondititons, b, ctxt, caret); 
		}
		
		/// <summary>
		/// Returns true if a node fully matches its environment concerning static if() declaration constraints and version/debug() restrictions.
		/// </summary>
		public bool MatchesDeclarationEnvironment(IEnumerable<DAttribute> conditions)
		{
			return declarationCondititons.IsMatching(conditions,ctxt);
		}
		
		public bool MatchesDeclarationEnvironment(DeclarationCondition dc)
		{
			return declarationCondititons.IsMatching(dc,ctxt);
		}

		public override string ToString()
		{
			return scopedBlock.ToString() + " // " + Caret.ToString();
		}
	}
}
