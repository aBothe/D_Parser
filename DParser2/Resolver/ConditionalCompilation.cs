using System;
using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Resolver
{
	public class ConditionalCompilation
	{
		public class ConditionSet
		{
			public readonly ConditionalCompilationFlags GlobalFlags;
			public ConditionalCompilationFlags LocalFlags;
			List<DeclarationCondition> conditionsBeingChecked = new List<DeclarationCondition>();

			public ConditionSet(ConditionalCompilationFlags gFLags, ConditionalCompilationFlags lFlags = null)
			{
				// Make a default global environment for test resolutions etc.
				GlobalFlags = gFLags ?? new ConditionalCompilationFlags(null,0,false);
				LocalFlags = lFlags;
			}

			public bool IsMatching(IEnumerable<DAttribute> conditions, ResolutionContext ctxt)
			{
				if(conditions!=null)
					foreach (var c in conditions)
						if (c is DeclarationCondition)
							if(!IsMatching((DeclarationCondition)c,ctxt))
							   return false;
				return true;
			}
			
			public bool IsMatching(DeclarationCondition dc, ResolutionContext ctxt)
			{
				var r = true;

				if(dc is NegatedDeclarationCondition)
				{
					var ng = (NegatedDeclarationCondition)dc;
					if(ng.FirstCondition is StaticIfCondition){
						if(!conditionsBeingChecked.Contains(ng.FirstCondition))
							conditionsBeingChecked.Add(ng.FirstCondition);
						else
							return false;
						
						r = !GlobalFlags.IsMatching((StaticIfCondition)ng.FirstCondition, ctxt);
						
						conditionsBeingChecked.Remove(ng.FirstCondition);
					}
					else
						r = (GlobalFlags.IsMatching(dc,ctxt) && LocalFlags.IsMatching(dc,ctxt));
				}
				else {
					if(dc is StaticIfCondition){
						if(!conditionsBeingChecked.Contains(dc))
							conditionsBeingChecked.Add(dc);
						else
							return false;
						
						r = GlobalFlags.IsMatching((StaticIfCondition)dc, ctxt);
						
						conditionsBeingChecked.Remove(dc);
					}
					else
						r = (GlobalFlags.IsMatching(dc,ctxt) || LocalFlags.IsMatching(dc,ctxt));
				}
				return r;
			}
		}

		public static void EnumConditions(ConditionSet cs,IStatement stmt, IBlockNode block, ResolutionContext ctxt, CodeLocation caret)
		{
			var l = new MutableConditionFlagSet();
			cs.LocalFlags = l;
			
			// If the current scope is a dedicated block.. (so NOT in a method but probably in an initializer or other static statement)
			if(block is DBlockNode)
			{
				// If so, get all (scoping) declaration conditions in the current block 
				// and add them to the condition list
				var mblocks = ((DBlockNode)block).GetMetaBlockStack(caret, false, true);

				if(mblocks!=null && mblocks.Length!=0)
					foreach(var mb in mblocks)
					{
						var amd = mb as AttributeMetaDeclaration;
						if(amd!=null && amd.AttributeOrCondition!=null && amd.AttributeOrCondition.Length!=0)
							foreach(var attr in amd.AttributeOrCondition)
								if(attr is DeclarationCondition)
									l.Add((DeclarationCondition)attr);
					}
			}

			// Scan up the current statement when e.g. inside a method body
			while (stmt != null)
			{
				if (stmt is StatementCondition)
					l.Add(((StatementCondition)stmt).Condition);
				stmt = stmt.Parent;
			}

			// Go up the block hierarchy and add all conditions that belong to the respective nodes
			while (block != null)
			{
				var dn = block as DNode;
				if (dn!=null)
				{
					if(dn is DBlockNode)
						GetDoneVersionDebugSpecs(cs, l, dn as DBlockNode, ctxt);
					if(dn.Attributes!=null)
						foreach (var attr in dn.Attributes)
							if (attr is DeclarationCondition)
								l.Add(((DeclarationCondition)attr));
				}
				
				block = block.Parent as IBlockNode;
			}
		}

		static void GetDoneVersionDebugSpecs(ConditionSet cs, MutableConditionFlagSet l, DBlockNode m, ResolutionContext ctxt)
		{
			if (m.StaticStatements == null || m.StaticStatements.Count == 0)
				return;

			foreach(var ss in m.StaticStatements)
			{
				if(ss is VersionSpecification)
				{
					var vs = (VersionSpecification)ss;
					
					if(!_checkForMatchinSpecConditions(m,cs,ss,ctxt))
						continue;
					
					if(vs.SpecifiedId==null)
 						l.AddVersionCondition(vs.SpecifiedNumber);
					else
						l.AddVersionCondition(vs.SpecifiedId);
				}
				else if(ss is DebugSpecification)
				{
					var ds = (DebugSpecification)ss;

					if(!_checkForMatchinSpecConditions(m,cs,ss, ctxt))
						continue;
					
					if (ds.SpecifiedId == null)
						l.AddDebugCondition(ds.SpecifiedDebugLevel);
					else
						l.AddDebugCondition(ds.SpecifiedId);
				}
			}
		}
		
		static bool _checkForMatchinSpecConditions(DBlockNode m,ConditionSet cs,StaticStatement ss, ResolutionContext ctxt)
		{
			return ss.Attributes == null || cs.IsMatching(ss.Attributes,ctxt);
		}
	}
}
