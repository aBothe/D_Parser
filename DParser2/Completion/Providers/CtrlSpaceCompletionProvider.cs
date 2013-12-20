using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver;
using D_Parser.Parser;

namespace D_Parser.Completion.Providers
{
	class CtrlSpaceCompletionProvider : AbstractCompletionProvider
	{
		public readonly IBlockNode curBlock;
		public readonly IStatement curStmt;
		public readonly MemberFilter visibleMembers;

		public CtrlSpaceCompletionProvider(ICompletionDataGenerator cdg, IBlockNode b, IStatement stmt, MemberFilter vis = MemberFilter.All)
			: base(cdg) { 
			this.curBlock = b;
			this.curStmt = stmt;
			visibleMembers = vis;
		}

		protected override void BuildCompletionDataInternal(IEditorData Editor, char enteredChar)
		{
			MemberCompletionEnumeration.EnumAllAvailableMembers(
					CompletionDataGenerator,
					curBlock,
					curStmt,
					Editor.CaretLocation,
					Editor.ParseCache,
					visibleMembers,
					new ConditionalCompilationFlags(Editor));

			//TODO: Split the keywords into such that are allowed within block statements and non-block statements
			// Insert typable keywords
			if (visibleMembers.HasFlag(MemberFilter.Keywords))
				foreach (var kv in DTokens.Keywords)
					CompletionDataGenerator.Add(kv.Key);

			else if (visibleMembers.HasFlag(MemberFilter.StructsAndUnions))
				foreach (var kv in DTokens.BasicTypes_Array)
					CompletionDataGenerator.Add(kv);
		}
	}
}
