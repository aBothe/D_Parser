using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Resolver
{
	public class ConditionalCompilation
	{
		public struct ConditionSet
		{
			public readonly ConditionalCompilationFlags GlobalFlags;
			public readonly List<DeclarationCondition> ScopeConditions;

			public ConditionSet(ConditionalCompilationFlags gFLags)
			{
				GlobalFlags = gFLags;
				ScopeConditions = new List<DeclarationCondition>();
			}
		}

		public static void EnumConditions(ConditionSet cs,IStatement stmt, IBlockNode block, CodeLocation caret)
		{
			var l = cs.ScopeConditions;
			
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
					GetDoneVersionDebugSpecs(cs, (DModule)block);
				else if (block is DNode)
				{
					foreach (var attr in ((DNode)block).Attributes)
						if (attr is DeclarationCondition)
							l.Add(((DeclarationCondition)attr));
				}
				
				block = block.Parent as IBlockNode;
			}
		}

		static void GetDoneVersionDebugSpecs(ConditionSet cs, DModule m)
		{
			if (m.StaticStatements == null || m.StaticStatements.Count == 0)
				return;

			foreach(var ss in m.StaticStatements)
			{
				if(ss is VersionSpecification)
				{
					var vs = (VersionSpecification)ss;

					if (vs.Conditions != null && !IsMatchingVersionAndDebugConditions(cs, vs.Conditions))
						continue;

					cs.ScopeConditions.Add(vs.SpecifiedId==null ? 
						new VersionCondition(vs.SpecifiedNumber) :
						new VersionCondition(vs.SpecifiedId));
				}
				else if(ss is DebugSpecification)
				{
					var ds = (DebugSpecification)ss;

					if (ds.Conditions != null && !IsMatchingVersionAndDebugConditions(cs, ds.Conditions))
						continue;

					cs.ScopeConditions.Add(ds.SpecifiedId == null ?
						new DebugCondition(ds.SpecifiedDebugLevel) :
						new DebugCondition(ds.SpecifiedId));
				}
			}
		}

		public static bool IsMatchingVersionAndDebugConditions(ConditionSet environment,
			IEnumerable<DAttribute> conditionsToCheck)
		{
			var expectedDebugConds = new List<DebugCondition>();
			var expectedVersionConds = new List<VersionCondition>();

			foreach (var c in environment.ScopeConditions)
				if (c is DebugCondition)
					expectedDebugConds.Add((DebugCondition)c);
				else if (c is VersionCondition)
					expectedVersionConds.Add((VersionCondition)c);

			bool HasDebugConditions = expectedDebugConds.Count != 0 || environment.GlobalFlags.IsDebug;
			
			foreach(var c in conditionsToCheck)
			{
				if (!IsMatching(environment, expectedDebugConds, expectedVersionConds, HasDebugConditions, c))
					return false;
			}
			return true;
		}

		private static bool IsMatching(ConditionSet environment, 
			List<DebugCondition> expectedDebugConds, List<VersionCondition> expectedVersionConds, 
			bool HasDebugConditions, DAttribute c)
		{
			bool t = false;
			if(c is NegatedDeclarationCondition)
			{
				return !IsMatching(environment, expectedDebugConds, expectedVersionConds, HasDebugConditions,
					((NegatedDeclarationCondition)c).FirstCondition);
			}
			else if (c is DebugCondition)
			{
				// If there is a debug condition but no debug flag set anywhere, return immediately
				if (!HasDebugConditions)
					return false;

				var ds = (DebugCondition)c;

				if (ds.DebugId != null)
				{
					if (!environment.GlobalFlags.IsDebugIdSet(ds.DebugId))
					{
						foreach (var dc in expectedDebugConds)
							if (t = dc.DebugId == ds.DebugId)
								break;

						if (!t)
							return false;
					}
				}
				else if (ds.DebugLevel != 0)
				{
					if (!environment.GlobalFlags.IsDebugLevel(ds.DebugLevel))
					{
						foreach (var dc in expectedDebugConds)
							if (t = dc.DebugLevel <= ds.DebugLevel) // TODO really <= or >= ? 
								break;

						if (!t)
							return false;
					}
				}
			}
			else if (c is VersionCondition)
			{
				var vs = (VersionCondition)c;

				if (vs.VersionId != null)
				{
					if (!environment.GlobalFlags.IsVersionSupported(vs.VersionId))
					{
						foreach (var vc in expectedVersionConds)
							if (t = vc.VersionId == vs.VersionId)
								break;

						if (!t)
							return false;
					}
				}
				else if (vs.VersionNumber != 0)
				{
					if (!environment.GlobalFlags.IsVersionSupported(vs.VersionNumber))
					{
						foreach (var vc in expectedVersionConds)
							if (t = vc.VersionNumber <= vs.VersionNumber) // TODO really <= or >= ? 
								break;

						if (!t)
							return false;
					}
				}
			}

			// Default all static if's to true..
			return true;
		}
	}
}
