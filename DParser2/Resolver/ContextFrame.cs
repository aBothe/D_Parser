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
		IStatement scopedStmt;
		readonly ConditionalCompilation.ConditionSet declarationCondititons;
		
		public IBlockNode ScopedBlock{get{ return scopedBlock; }}
		public IStatement ScopedStatement{get{return scopedStmt;}}
		#endregion
		
		public ContextFrame(ResolutionContext ctxt, IBlockNode b, IStatement stmt = null)
		{
			this.ctxt = ctxt;
			declarationCondititons = new ConditionalCompilation.ConditionSet(ctxt.CompilationEnvironment);
			
			Set(b,stmt);
		}
		
		public void IntroduceTemplateParameterTypes(DSymbol tir)
		{
			if(tir!=null && tir.DeducedTypes != null)
				foreach (var dt in tir.DeducedTypes)
					DeducedTemplateParameters[dt.NameHash] = dt;
		}

		public void RemoveParamTypesFromPreferredLocals(DSymbol tir)
		{
			if (tir != null && tir.DeducedTypes != null)
				foreach (var dt in tir.DeducedTypes)
					DeducedTemplateParameters.Remove(dt.NameHash);
		}
		
		public void Set(IStatement stmt)
		{
			Set(scopedBlock, stmt);
		}
		
		public void Set(IBlockNode b, IStatement stmt = null)
		{
			scopedBlock = b;
			scopedStmt = stmt;
			
			var c = CodeLocation.Empty; //TODO: Take the caret position if we're in the currently edited module and the scoped block is the module root(?)
			if(stmt == null)
			{
				if(b!=null)
					c = b.BlockStartLocation;
			}
			else
				c = stmt.Location;
			
			ConditionalCompilation.EnumConditions(declarationCondititons, stmt, b, ctxt, c); 
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
			return scopedBlock.ToString() + " // " + (scopedStmt == null ? "" : scopedStmt.ToString());
		}
	}
}
