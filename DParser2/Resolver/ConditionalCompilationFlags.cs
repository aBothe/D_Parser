using System.Collections.Generic;

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
		#endregion

		public bool IsVersionSupported(string versionId)
		{ return setVersions.Contains(versionId); }
		public bool IsVersionSupported(int versionNumber)
		{ return versionNumber >= this.versionNumber || setVersions.Contains(versionNumber.ToString()); }

		public bool IsDebugIdSet(string id)
		{ return setDebugVersions.Contains(id); }
		public bool IsDebugLevel(int lvl)
		{ return lvl >= debugLevel; }
		public bool IsDebug
		{ get { return debugFlagOverride || debugLevel != 0 || setDebugVersions.Count != 0; } }

		protected ConditionalCompilationFlags()	{}

		public ConditionalCompilationFlags(IEnumerable<string> definedVersionIdentifiers, int versionNumber,
			bool debug,IEnumerable<string> definedDebugIdentifiers=null, int debugLevel=0)
		{
			if(definedVersionIdentifiers!=null)
				setVersions.AddRange(definedVersionIdentifiers);
			this.versionNumber = versionNumber;

			if (definedDebugIdentifiers != null)
				setDebugVersions.AddRange(definedDebugIdentifiers);
			this.debugLevel = debugLevel;
			this.debugFlagOverride = debug;
		}
	}
}
