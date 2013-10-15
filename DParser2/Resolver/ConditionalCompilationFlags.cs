using System.Collections.Generic;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Resolver.ExpressionSemantics;

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
		protected int versionNumber = 0;

		protected bool debugFlagOverride = false;
		protected List<string> setDebugVersions = new List<string>();
		protected int debugLevel = 0;


		protected bool IsVersionSupported(string versionId)
		{ return setVersions.Contains(versionId); }
		protected bool IsVersionSupported(int versionNumber)
		{ return versionNumber >= this.versionNumber || setVersions.Contains(versionNumber.ToString()); }

		protected bool IsDebugIdSet(string id)
		{ return setDebugVersions.Contains(id); }
		protected bool IsDebugLevel(int lvl)
		{ return lvl >= debugLevel; }
		protected bool IsDebug
		{ get { return debugFlagOverride || debugLevel != 0 || setDebugVersions.Count != 0; } }
		#endregion

		protected ConditionalCompilationFlags()	{}

		public ConditionalCompilationFlags(IEditorData ed)
			: this(ed.GlobalVersionIds, ed.VersionNumber, ed.IsDebug, ed.GlobalDebugIds, ed.DebugLevel) { }

		public ConditionalCompilationFlags(IEnumerable<string> definedVersionIdentifiers, int versionNumber,
			bool debug,IEnumerable<string> definedDebugIdentifiers=null, int debugLevel=0)
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
				return vc.VersionId == null ?
					vc.VersionNumber >= versionNumber :
					setVersions.Contains(vc.VersionId);
			}
			else if(cond is DebugCondition)
			{
				var dc = (DebugCondition)cond;
				if (dc.HasNoExplicitSpecification)
					return IsDebug;
				return dc.DebugId == null ?
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
					return vc.VersionId == null ?
						vc.VersionNumber < versionNumber :
						(!setVersions.Contains(vc.VersionId) || setVersions.Contains("!"+vc.VersionId));
				}
				else if(cond is DebugCondition)
				{
					var dc = (DebugCondition)cond;
					if (dc.HasNoExplicitSpecification)
						return !IsDebug;
					return dc.DebugId == null ?
						debugLevel < dc.DebugLevel :
						(!setDebugVersions.Contains(dc.DebugId) || setDebugVersions.Contains("!" + dc.DebugId));
				}
				else if(cond is StaticIfCondition)
					return !IsMatching((StaticIfCondition)cond,ctxt);
			}

			// True on default -- static if's etc. will be filtered later on
			return true;
		}
		
		public bool IsMatching(StaticIfCondition sc, ResolutionContext ctxt)
		{
			ISymbolValue v;
			try{
				v = Evaluation.EvaluateValue(sc.Expression, ctxt);
				if(v is VariableValue)
					v = Evaluation.EvaluateValue(((VariableValue)v).Variable.Initializer, ctxt);
			}
			catch
			{
				return false; //TODO: Remove the try / Notify the user on an exception case -- when the evaluation is considered stable only!!
			}
			return !Evaluation.IsFalseZeroOrNull(v); //TODO: Just because the expression evaluation isn't working properly currently, let it return true to have it e.g. in the completion list
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

		public void AddVersionCondition(string id)
		{
			if (!setVersions.Contains(id))
				setVersions.Add(id);
		}

		public void AddVersionCondition(int v)
		{
			if (v > versionNumber)
				versionNumber = v;
		}

		public void AddDebugCondition(string id)
		{
			if (!setDebugVersions.Contains(id))
				setDebugVersions.Add(id);
		}

		public void AddDebugCondition(int lvl)
		{
			if (lvl > debugLevel)
				debugLevel = lvl;
		}
	}
}
