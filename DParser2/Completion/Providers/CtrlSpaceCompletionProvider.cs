using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver;
using D_Parser.Parser;
using D_Parser.Dom.Expressions;

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

		private sealed class LabelVisitor : DefaultDepthFirstVisitor
		{
			public ICompletionDataGenerator gen;

			public LabelVisitor(ICompletionDataGenerator gen)
			{
				this.gen = gen;
			}

			public override void Visit (LabeledStatement s)
			{
				if(s.IdentifierHash != 0)
					gen.AddTextItem (s.Identifier, "Jump label");
			}
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
			{
				foreach (var kv in DTokens.Keywords)
					CompletionDataGenerator.Add(kv.Key);
			}
			else if (visibleMembers.HasFlag(MemberFilter.StructsAndUnions))
			{
				foreach (var kv in DTokens.BasicTypes_Array)
					CompletionDataGenerator.Add(kv);
			}

			if (visibleMembers.HasFlag(MemberFilter.Labels))
			{
				IStatement stmt = curStmt;
				do
				{
					stmt = stmt.Parent;
					if (stmt is SwitchStatement) 
					{
						CompletionDataGenerator.Add (DTokens.Case);
						CompletionDataGenerator.Add (DTokens.Default);
						break;
					}
				} while(stmt != null && stmt.Parent != null);

				if(stmt != null)
					stmt.Accept (new LabelVisitor (CompletionDataGenerator));
			}

			if (visibleMembers.HasFlag(MemberFilter.x86Registers))
			{
				foreach (var kv in AsmRegisterExpression.x86RegisterTable)
					CompletionDataGenerator.AddTextItem(kv.Key, kv.Value);
			}

			if (visibleMembers.HasFlag(MemberFilter.x64Registers))
			{
				foreach (var kv in AsmRegisterExpression.x64RegisterTable)
					CompletionDataGenerator.AddTextItem(kv.Key, kv.Value);
			}
		}
	}
}
