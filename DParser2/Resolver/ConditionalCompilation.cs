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

			public ConditionSet(ConditionalCompilationFlags gFLags, ConditionalCompilationFlags lFlags = null)
			{
				// Make a default global environment for test resolutions etc.
				GlobalFlags = gFLags ?? new ConditionalCompilationFlags(null,0,false);
				LocalFlags = lFlags;
			}

			public bool IsMatching(IEnumerable<DAttribute> conditions)
			{
				if(conditions!=null)
					foreach (var c in conditions)
						if (c is DeclarationCondition){
							if(c is NegatedDeclarationCondition)
							{
								if (!(GlobalFlags.IsMatching((DeclarationCondition)c) && LocalFlags.IsMatching((DeclarationCondition)c)))
									return false;
							}
							else if(!(GlobalFlags.IsMatching((DeclarationCondition)c) || LocalFlags.IsMatching((DeclarationCondition)c)))
								return false;
						}
				return true;
			}
		}

		public static void EnumConditions(ConditionSet cs,IStatement stmt, IBlockNode block, CodeLocation caret)
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
				if (block is DModule)
					GetDoneVersionDebugSpecs(cs, l, (DModule)block);
				else if (block is DNode)
				{
					foreach (var attr in ((DNode)block).Attributes)
						if (attr is DeclarationCondition)
							l.Add(((DeclarationCondition)attr));
				}
				
				block = block.Parent as IBlockNode;
			}
		}

		static void GetDoneVersionDebugSpecs(ConditionSet cs, MutableConditionFlagSet l, DModule m)
		{
			if (m.StaticStatements == null || m.StaticStatements.Count == 0)
				return;

			foreach(var ss in m.StaticStatements)
			{
				if(ss is VersionSpecification)
				{
					var vs = (VersionSpecification)ss;

					if (vs.Conditions != null && !cs.IsMatching(vs.Conditions))
						continue;

					if(vs.SpecifiedId==null)
 						l.AddVersionCondition(vs.SpecifiedNumber);
					else
						l.AddVersionCondition(vs.SpecifiedId);
				}
				else if(ss is DebugSpecification)
				{
					var ds = (DebugSpecification)ss;

					if (ds.Conditions != null && !cs.IsMatching(ds.Conditions))
						continue;

					if (ds.SpecifiedId == null)
						l.AddDebugCondition(ds.SpecifiedDebugLevel);
					else
						l.AddDebugCondition(ds.SpecifiedId);
				}
			}
		}
	}
}
