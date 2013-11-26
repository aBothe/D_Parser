using D_Parser.Dom;
using D_Parser.Misc;

namespace D_Parser.Completion
{
	/// <summary>
	/// Generic interface between a high level editor object and the low level completion engine
	/// </summary>
	public class EditorData:IEditorData
	{
		public virtual string ModuleCode { get; set; }
		public virtual CodeLocation CaretLocation { get; set; }
		public virtual int CaretOffset { get; set; }
		public virtual DModule SyntaxTree { get; set; }

		public virtual ParseCacheView ParseCache { get; set; }
		
		public int VersionNumber { get; set; }
		public string[] GlobalVersionIds { get; set; }
		public bool IsDebug {set;get;}
		public int DebugLevel { get; set; }
		public string[] GlobalDebugIds { get; set; }
		
		public virtual void ApplyFrom(IEditorData data)
		{
			ModuleCode = data.ModuleCode;
			CaretLocation = data.CaretLocation;
			CaretOffset = data.CaretOffset;
			SyntaxTree = data.SyntaxTree;
			ParseCache = data.ParseCache;

			VersionNumber = data.VersionNumber;
			GlobalVersionIds = data.GlobalVersionIds;
			IsDebug = data.IsDebug;
			DebugLevel = data.DebugLevel;
			GlobalDebugIds = data.GlobalDebugIds;
		}
	}

	public interface IEditorData
	{
		string ModuleCode { get; }
		CodeLocation CaretLocation { get; }
		int CaretOffset { get; }
		DModule SyntaxTree { get; }

		ParseCacheView ParseCache { get; }

		int VersionNumber { get; }
		string[] GlobalVersionIds { get; }
		bool IsDebug{get;}
		int DebugLevel { get; }
		string[] GlobalDebugIds { get; }
	}
}
