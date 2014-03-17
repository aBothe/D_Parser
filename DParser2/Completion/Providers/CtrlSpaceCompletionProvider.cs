using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver;
using D_Parser.Parser;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Completion.Providers
{
	class CtrlSpaceCompletionProvider : AbstractCompletionProvider
	{
		public readonly IBlockNode curBlock;
		public readonly MemberFilter visibleMembers;

		public CtrlSpaceCompletionProvider(ICompletionDataGenerator cdg, IBlockNode b, MemberFilter vis = MemberFilter.All)
			: base(cdg) { 
			this.curBlock = b;
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

		static readonly string[] PropertyAttributeCompletionItems = new[] 
		{
			"disable",
			"property",
			"safe",
			"system",
			"trusted"
		};

		protected override void BuildCompletionDataInternal(IEditorData Editor, char enteredChar)
		{
			MemberCompletionEnumeration.EnumAllAvailableMembers(
					CompletionDataGenerator,
					curBlock,
					Editor.CaretLocation,
					Editor.ParseCache,
					visibleMembers,
					new ConditionalCompilationFlags(Editor));

			//TODO: Split the keywords into such that are allowed within block statements and non-block statements
			// Insert typable keywords
			if ((visibleMembers & MemberFilter.Keywords) != 0)
			{
				foreach (var kv in DTokens.Keywords)
					CompletionDataGenerator.Add(kv.Key);
			}
			else if ((visibleMembers & MemberFilter.StructsAndUnions) != 0)
			{
				foreach (var kv in DTokens.BasicTypes_Array)
					CompletionDataGenerator.Add(kv);
			}

			if ((visibleMembers & MemberFilter.Labels) != 0)
			{
				var stmt = DResolver.SearchStatementDeeplyAt(curBlock, Editor.CaretLocation);
				bool addedSwitchKWs = false;
				while(stmt != null && stmt.Parent != null)
				{
					stmt = stmt.Parent;
					if (!addedSwitchKWs && stmt is SwitchStatement) 
					{
						addedSwitchKWs = true;
						CompletionDataGenerator.Add (DTokens.Case);
						CompletionDataGenerator.Add (DTokens.Default);
					}
				}

				if(stmt != null)
					stmt.Accept (new LabelVisitor (CompletionDataGenerator));
			}

			if ((visibleMembers & MemberFilter.x86Registers) != 0)
			{
				foreach (var kv in AsmRegisterExpression.x86RegisterTable)
					CompletionDataGenerator.AddTextItem(kv.Key, kv.Value);
			}

			if ((visibleMembers & MemberFilter.x64Registers) != 0)
			{
				foreach (var kv in AsmRegisterExpression.x64RegisterTable)
					CompletionDataGenerator.AddTextItem(kv.Key, kv.Value);
			}

			if ((visibleMembers & (MemberFilter.x86Registers | MemberFilter.x64Registers)) != 0) {
				CompletionDataGenerator.Add (DTokens.__LOCAL_SIZE);

				CompletionDataGenerator.AddTextItem("offsetof","");
				CompletionDataGenerator.AddTextItem("seg","The seg means load the segment number that the symbol is in. This is not relevant for flat model code. Instead, do a move from the relevant segment register.");

				// Provide AsmTypePrefixes
				CompletionDataGenerator.AddTextItem("near","");
				CompletionDataGenerator.AddTextItem("far","");
				CompletionDataGenerator.AddTextItem("word","");
				CompletionDataGenerator.AddTextItem("dword","");
				CompletionDataGenerator.AddTextItem("qword","");
				CompletionDataGenerator.AddTextItem("ptr","");
			}

			if ((visibleMembers & MemberFilter.BuiltInPropertyAttributes) != 0)
			{
				foreach (var propAttr in PropertyAttributeCompletionItems)
					CompletionDataGenerator.AddPropertyAttribute(propAttr);
			}
		}
	}
}
