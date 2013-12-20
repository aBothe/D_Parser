using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ASTScanner;
using System.IO;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Completion
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

		public static ISyntaxRegion FindCurrentCaretContext(IEditorData editor, ref IBlockNode currentScope, out IStatement currentStatement)
		{
			if(currentScope == null)
				currentScope = DResolver.SearchBlockAt (editor.SyntaxTree, editor.CaretLocation, out currentStatement);

			if (currentScope == null) {
				currentStatement = null;
				return null;
			}

			bool ParseDecl = false;

			int blockStart = 0;
			var blockStartLocation = currentScope.BlockStartLocation;

			if (currentScope is DMethod)
			{
				var block = (currentScope as DMethod).GetSubBlockAt(editor.CaretLocation);

				if (block != null)
					blockStart = DocumentHelper.GetOffsetByRelativeLocation (editor.ModuleCode, editor.CaretLocation, editor.CaretOffset, blockStartLocation = block.Location);
				else {
					currentScope = currentScope.Parent as IBlockNode;
					return FindCurrentCaretContext (editor, ref currentScope, out currentStatement);
				}
			}
			else if (currentScope != null)
			{
				if (currentScope.BlockStartLocation.IsEmpty || (editor.CaretLocation < currentScope.BlockStartLocation && editor.CaretLocation > currentScope.Location))
				{
					ParseDecl = true;
					blockStart = DocumentHelper.GetOffsetByRelativeLocation(editor.ModuleCode, editor.CaretLocation, editor.CaretOffset, blockStartLocation = currentScope.Location);
				}
				else
					blockStart = DocumentHelper.GetOffsetByRelativeLocation(editor.ModuleCode, editor.CaretLocation, editor.CaretOffset, currentScope.BlockStartLocation);
			}

			if (blockStart >= 0 && editor.CaretOffset - blockStart > 0)
				using (var sr = new Misc.StringView(editor.ModuleCode, blockStart, editor.CaretOffset - blockStart))
				{
					var psr = DParser.Create(sr);

					/*					 Deadly important! For correct resolution behaviour, 
					 * it is required to set the parser virtually to the blockStart position, 
					 * so that everything using the returned object is always related to 
					 * the original code file, not our code extraction!
					 */
					psr.Lexer.SetInitialLocation(blockStartLocation);

					ISyntaxRegion ret = null;

					if (currentScope == null)
						ret = psr.Parse();
					else if (currentScope is DMethod)
					{
						psr.Step();
						var dm = currentScope as DMethod;
						dm.Clear();

						if ((dm.SpecialType & DMethod.MethodType.Lambda) != 0 &&
							psr.Lexer.LookAhead.Kind != DTokens.OpenCurlyBrace) {
							psr.LambdaSingleStatementBody (dm);
							ret = dm.Body;
						} else {
							var methodRegion = DTokens.Body;

							if (dm.In != null && blockStartLocation == dm.In.Location)
								methodRegion = DTokens.In;

							if (dm.Out != null && blockStartLocation == dm.Out.Location)
								methodRegion = DTokens.Out;

							var newBlock = psr.BlockStatement (currentScope);
							ret = newBlock;

							switch (methodRegion) {
								case DTokens.Body:
									newBlock.EndLocation = dm.Body.EndLocation;
									dm.Body = newBlock;
									break;
								case DTokens.In:
									newBlock.EndLocation = dm.In.EndLocation;
									dm.In = newBlock;
									break;
								case DTokens.Out:
									newBlock.EndLocation = dm.Out.EndLocation;
									dm.Out = newBlock;
									break;
							}
						}
					}
					else if (currentScope is DModule)
						ret = psr.Root();
					else
					{
						psr.Step();
						if (ParseDecl)
						{
							var ret2 = psr.Declaration(currentScope);

							if (ret2 != null && ret2.Length > 0)
								ret = ret2[0];
						}
						else if (currentScope is DClassLike)
						{
							var t = new DClassLike((currentScope as DClassLike).ClassType);
							t.AssignFrom(currentScope);
							t.Clear();
							psr.ClassBody(t);
							ret = t;
						}
						else if (currentScope is DEnum)
						{
							var t = new DEnum();
							t.AssignFrom(currentScope);
							t.Clear();
							psr.EnumBody(t);
							ret = t;
						}
					}

					currentScope = DResolver.SearchBlockAt (currentScope, 
						psr.Lexer.CurrentToken != null ? psr.Lexer.CurrentToken.EndLocation : editor.CaretLocation, 
						out currentStatement);
					return ret;
				}

			currentStatement = null;
			return null;
		}
	}
}
