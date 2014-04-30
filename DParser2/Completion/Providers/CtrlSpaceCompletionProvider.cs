using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver;
using D_Parser.Parser;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.TypeResolution;
using System.Collections;

namespace D_Parser.Completion.Providers
{
	class CtrlSpaceCompletionProvider : AbstractCompletionProvider
	{
		public readonly IBlockNode curBlock;
		public readonly MemberFilter visibleMembers;
		public readonly byte[] specialKeywords;

		public CtrlSpaceCompletionProvider(ICompletionDataGenerator cdg, IBlockNode b, MemberFilter vis = MemberFilter.All ,params byte[] specialKeywords)
			: base(cdg) { 
			this.curBlock = b;
			visibleMembers = vis;
			this.specialKeywords = specialKeywords;
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

		static readonly byte[] statementKeywords = new[]{
			DTokens.If,
			DTokens.Else,
			DTokens.While,
			DTokens.Do,
			DTokens.For,
			DTokens.Foreach,
			DTokens.Foreach_Reverse,
			DTokens.Switch,
			DTokens.Case,
			DTokens.Default,
			DTokens.Final,
			DTokens.Continue,
			DTokens.Break,
			DTokens.Return,
			DTokens.Goto,
			DTokens.With,
			DTokens.Synchronized,
			DTokens.Try,
			DTokens.Catch,
			DTokens.Finally,
			DTokens.Scope,
			DTokens.Throw,
			DTokens.Asm,
			DTokens.Pragma,
			DTokens.Mixin,
			DTokens.Version,
			DTokens.Debug,
			DTokens.Assert,
			DTokens.Import,
		};

		static byte[] expressionKeywords = new[]{
			DTokens.Import,
			DTokens.Assert,
			DTokens.Is, DTokens.In,
			DTokens.Delete,
			DTokens.Cast,
			DTokens.New,
			DTokens.This,
			DTokens.Super,
			DTokens.Null,
			DTokens.True,
			DTokens.False,
			DTokens.Function,
			DTokens.Delegate,
			DTokens.Mixin,
			DTokens.Typeid,
			DTokens.__traits,
			DTokens.__DATE__,
			DTokens.__FILE__,
			DTokens.__LINE__,
			DTokens.__FUNCTION__,
			DTokens.__LOCAL_SIZE,
			DTokens.__MODULE__,
			DTokens.__PRETTY_FUNCTION__,
			DTokens.__TIMESTAMP__,
			DTokens.__TIME__,
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
			var bits = new BitArray (DTokens.MaxToken);
			CompletionDataGenerator.Add (DTokens.__EOF__);

			// Insert typable keywords
			if ((visibleMembers & MemberFilter.BlockKeywords) != 0) {
				for (byte tk = DTokens.MaxToken - 1; tk > 0; tk--)
					if (!bits [tk] && (DTokens.IsBasicType (tk) || 
						DTokens.IsClassLike (tk) || 
						DTokens.IsStorageClass (tk) || DTokens.IsParamModifier(tk) ||
						DTokens.IsVisibilityModifier(tk) ||
						tk == DTokens.Align || tk == DTokens.Pragma)) {
						CompletionDataGenerator.Add (tk);
						bits [tk] = true;
					}
			}

			if ((visibleMembers & MemberFilter.StatementBlockKeywords) != 0) {
				foreach (var kv in statementKeywords)
					if (!bits [kv]) {
						CompletionDataGenerator.Add (kv);
						bits [kv] = true;
					}
			}

			if ((visibleMembers & MemberFilter.ExpressionKeywords) != 0) {
				foreach (var kv in expressionKeywords)
					if (!bits [kv]) {
						CompletionDataGenerator.Add (kv);
						bits [kv] = true;
					}
			}

			if ((visibleMembers & MemberFilter.StructsAndUnions) != 0)
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
