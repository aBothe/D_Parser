using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Resolver;
using System.Threading;

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
		
		public ulong VersionNumber { get; set; }
		public string[] GlobalVersionIds { get; set; }
		public bool IsDebug {set;get;}
		public ulong DebugLevel { get; set; }
		public string[] GlobalDebugIds { get; set; }

        public CancellationToken CancelToken { get; set; }

		private ResolutionContext NormalContext;
		private ResolutionContext NoDeductionContext;
		private ResolutionContext RawContext;

		public EditorData()
		{
		}

		public ResolutionContext GetLooseResolutionContext(LooseResolution.NodeResolutionAttempt att)
		{
			if (att == LooseResolution.NodeResolutionAttempt.Normal)
			{
				NormalContext.PopAll();
				return NormalContext;
			}
			else if (att == LooseResolution.NodeResolutionAttempt.NoParameterOrTemplateDeduction)
			{
				NoDeductionContext.PopAll();
				return NoDeductionContext;
			}
			else if (att == LooseResolution.NodeResolutionAttempt.RawSymbolLookup)
			{
				RawContext.PopAll();
				return RawContext;
			}
			return null;
		}

		public void NewResolutionContexts()
		{
			NormalContext = ResolutionContext.Create(this, false);
			NoDeductionContext = ResolutionContext.Create(this, false);
			RawContext = ResolutionContext.Create(this, false);
		}

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

			CancelToken	= data.CancelToken;
		}
	}

	public interface IEditorData
	{
		string ModuleCode { get; }
		CodeLocation CaretLocation { get; }
		int CaretOffset { get; }
		DModule SyntaxTree { get; }

		ParseCacheView ParseCache { get; }

		ulong VersionNumber { get; }
		string[] GlobalVersionIds { get; }
		bool IsDebug{get;}
		ulong DebugLevel { get; }
		string[] GlobalDebugIds { get; }

		CancellationToken CancelToken { get; }

		ResolutionContext GetLooseResolutionContext(LooseResolution.NodeResolutionAttempt att);
	}
}
