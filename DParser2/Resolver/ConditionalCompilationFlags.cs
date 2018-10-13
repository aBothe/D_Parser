using System.Collections.Generic;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Dom.Statements;

namespace D_Parser.Resolver
{
	/// <summary>
	/// Stores [debug] version information about the compiled programs.
	/// These information are valid module-independently and depend on 
	/// 1) Compiler vendor-specific defintions and/or
	/// 2) Flags that are set in the compiler's command line.
	/// Further info: http://dlang.org/version.html
	/// </summary>
	public class ConditionalCompilationFlags
	{
		#region Properties
		protected List<string> setVersions = new List<string>();
		public IEnumerable<string> Versions {get{ return setVersions;}}
		protected ulong versionNumber = 0;
		public ulong VersionNumber{get{return versionNumber;}}

		protected bool debugFlagOverride = false;
		protected List<string> setDebugVersions = new List<string>();
		public IEnumerable<string> DebugVersions {get{ return setDebugVersions;}}
		protected ulong debugLevel = 0;
		public ulong DebugLevel {get{ return debugLevel; }}


		public bool IsVersionSupported(string versionId)
		{ return setVersions.Contains(versionId); }
		public bool IsVersionSupported(ulong versionNumber)
		{ return versionNumber >= this.versionNumber || setVersions.Contains(versionNumber.ToString()); }

		public bool IsDebugIdSet(string id)
		{ return setDebugVersions.Contains(id); }
		public bool IsDebugLevel(ulong lvl)
		{ return lvl >= debugLevel; }
		public bool IsDebug
		{ get { return debugFlagOverride || debugLevel != 0 || setDebugVersions.Count != 0; } }
		#endregion

		protected ConditionalCompilationFlags()	{}

		public ConditionalCompilationFlags(IEditorData ed)
			: this(ed.GlobalVersionIds, ed.VersionNumber, ed.IsDebug, ed.GlobalDebugIds, ed.DebugLevel) { }

		public ConditionalCompilationFlags(IEnumerable<string> definedVersionIdentifiers, ulong versionNumber,
			bool debug,IEnumerable<string> definedDebugIdentifiers=null, ulong debugLevel=0)
		{
			if (definedVersionIdentifiers != null)
				setVersions.AddRange(definedVersionIdentifiers);
			this.versionNumber = versionNumber;

			if (definedDebugIdentifiers != null)
				setDebugVersions.AddRange(definedDebugIdentifiers);
			this.debugLevel = debugLevel;
			this.debugFlagOverride = debug;
		}

		public bool IsMatching(DeclarationCondition cond, ResolutionContext ctxt)
		{
			if(cond is VersionCondition)
			{
				var vc = (VersionCondition)cond;
				return vc.VersionIdHash == 0 ?
					vc.VersionNumber >= versionNumber :
					setVersions.Contains(vc.VersionId);
			}
			else if(cond is DebugCondition)
			{
				var dc = (DebugCondition)cond;
				if (dc.HasNoExplicitSpecification)
					return IsDebug;
				return dc.DebugIdHash == 0 ?
					debugLevel >= dc.DebugLevel :
					setDebugVersions.Contains(dc.DebugId);
			}
			else if(cond is StaticIfCondition)
				return IsMatching((StaticIfCondition)cond,ctxt);
			else if(cond is NegatedDeclarationCondition)
			{
				cond = ((NegatedDeclarationCondition)cond).FirstCondition;
				//TODO: Ensure that there's no double negation
				if(cond is VersionCondition)
				{
					var vc = (VersionCondition)cond;
					return vc.VersionIdHash == 0 ?
						vc.VersionNumber < versionNumber :
						(!setVersions.Contains(vc.VersionId) || setVersions.Contains("!"+vc.VersionId));
				}
				else if(cond is DebugCondition)
				{
					var dc = (DebugCondition)cond;
					if (dc.HasNoExplicitSpecification)
						return !IsDebug;
					return dc.DebugIdHash == 0 ?
						debugLevel < dc.DebugLevel :
						(!setDebugVersions.Contains(dc.DebugId) || setDebugVersions.Contains("!" + dc.DebugId));
				}
				else if(cond is StaticIfCondition)
					return !IsMatching((StaticIfCondition)cond,ctxt);
			}

			// True on default -- static if's etc. will be filtered later on
			return true;
		}

		List<StaticIfCondition> conditionsBeingChecked = new List<StaticIfCondition>();

		public bool IsMatching(StaticIfCondition sc, ResolutionContext ctxt)
		{
			if (conditionsBeingChecked.Contains(sc) || ctxt == null)
				return false;

			conditionsBeingChecked.Add(sc);
			ISymbolValue v = null;

			/*if (System.Threading.Interlocked.Increment(ref stk) > 5)
			{
			}*/
			try
			{
				v = Evaluation.EvaluateValue(sc.Expression, ctxt);

				if (v is VariableValue)
					v = Evaluation.EvaluateValue(((VariableValue)v).Variable.Initializer, ctxt);
			}
			finally
			{
				conditionsBeingChecked.Remove(sc);
			}
			//System.Threading.Interlocked.Decrement(ref stk);
			return !Evaluation.IsFalsy(v); //TODO: Just because the expression evaluation isn't working properly currently, let it return true to have it e.g. in the completion list
		}
	}

	public class MutableConditionFlagSet : ConditionalCompilationFlags
	{
		public void Add(DeclarationCondition cond)
		{
			if(cond is VersionCondition)
			{
				var vc = (VersionCondition)cond;
				if (vc.VersionId == null)
					AddVersionCondition(vc.VersionNumber);
				else
					AddVersionCondition(vc.VersionId);
			}
			else if(cond is DebugCondition)
			{
				var dc = (DebugCondition)cond;
				if (dc.DebugId == null)
					AddDebugCondition(dc.DebugLevel);
				else
					AddDebugCondition(dc.DebugId);
			}
			else if(cond is NegatedDeclarationCondition)
			{
				cond = ((NegatedDeclarationCondition)cond).FirstCondition;

				if (cond is VersionCondition)
				{
					var vc = (VersionCondition)cond;
					if (vc.VersionId != null)
						/*AddVersionCondition(vc.VersionNumber); -- TODO How are "negated" version numbers handled?
					else*/
						AddVersionCondition("!"+vc.VersionId);
				}
				else if (cond is DebugCondition)
				{
					var dc = (DebugCondition)cond;
					if (dc.DebugId != null)
						/*AddDebugCondition(dc.DebugLevel);
					else*/
						AddDebugCondition("!"+dc.DebugId);
				}
			}
		}

		public void AddVersionCondition(VersionSpecification vs)
		{
			if (vs.SpecifiedId == null)
				AddVersionCondition(vs.SpecifiedNumber);
			else
				AddVersionCondition(vs.SpecifiedId);
		}

		public void AddVersionCondition(string id)
		{
			if (!setVersions.Contains(id))
				setVersions.Add(id);
		}

		public void AddVersionCondition(ulong v)
		{
			if (v > versionNumber)
				versionNumber = v;
		}

		public void AddDebugCondition(DebugSpecification ds)
		{
			if (ds.SpecifiedId == null)
				AddDebugCondition(ds.SpecifiedDebugLevel);
			else
				AddDebugCondition(ds.SpecifiedId);
		}

		public void AddDebugCondition(string id)
		{
			if (!setDebugVersions.Contains(id))
				setDebugVersions.Add(id);
		}

		public void AddDebugCondition(ulong lvl)
		{
			//if (lvl > debugLevel) -- 
			debugLevel = lvl;
		}
	}
}
