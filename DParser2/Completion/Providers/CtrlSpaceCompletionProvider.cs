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
		public object parsedBlock;
		public IBlockNode curBlock;
		public IStatement curStmt;
		public ParserTrackerVariables trackVars;

		public CtrlSpaceCompletionProvider(ICompletionDataGenerator cdg) : base(cdg) { }

		protected override void BuildCompletionDataInternal(IEditorData Editor, char enteredChar)
		{
			var visibleMembers = MemberFilter.All;

			if(!GetVisibleMemberFilter(Editor, enteredChar, ref visibleMembers, ref curStmt))
				return;

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

		private bool GetVisibleMemberFilter(IEditorData Editor, char enteredChar, ref MemberFilter visibleMembers, ref IStatement curStmt)
		{
			if (trackVars == null)
			{
				// --> Happens if no actual declaration syntax given --> Show types/keywords anyway
				visibleMembers = MemberFilter.Types | MemberFilter.Keywords | MemberFilter.TypeParameters;
			}
			else
			{
				var n = trackVars.LastParsedObject as INode;
				var dv = n as DVariable;
				if (trackVars.ExpectingNodeName) {
					if (dv != null && dv.IsAlias && dv.Type == null) {
						// Show completion because no aliased type has been entered yet
					} else if (n != null && n.NameHash==0 && enteredChar != '\0')
						return false;
				}

				else if (trackVars.LastParsedObject is TokenExpression &&
					DTokens.BasicTypes[(trackVars.LastParsedObject as TokenExpression).Token] &&
					DTokens.IsIdentifierChar(enteredChar))
					return false;

				if (trackVars.LastParsedObject is Modifier)
				{
					var attr = trackVars.LastParsedObject as Modifier;

					if (attr.IsStorageClass && attr.Token != DTokens.Abstract)
						return false;
				}

				else if (trackVars.IsParsingBaseClassList)
				{
					var dc = parsedBlock as DClassLike;
					if (dc != null && dc.ClassType == DTokens.Interface)
						visibleMembers = MemberFilter.Interfaces | MemberFilter.Templates;
					else
						visibleMembers = MemberFilter.Classes | MemberFilter.Interfaces | MemberFilter.Templates;
					return true;
				}
				
				if (trackVars.IsParsingAssignExpression)
				{
					visibleMembers = MemberFilter.All;
					return true;
				}

				if ((trackVars.LastParsedObject is NewExpression && trackVars.IsParsingInitializer) ||
					trackVars.LastParsedObject is TemplateInstanceExpression && ((TemplateInstanceExpression)trackVars.LastParsedObject).Arguments == null)
					visibleMembers = MemberFilter.Types;
				else if (enteredChar == ' ')
					return false;
				// In class bodies, do not show variables
				else if (!(parsedBlock is BlockStatement || trackVars.IsParsingInitializer))
				{
					bool showVariables = false;
					var dbn = parsedBlock as DBlockNode;
					if (dbn != null && dbn.StaticStatements != null && dbn.StaticStatements.Count > 0)
					{
						var ss = dbn.StaticStatements[dbn.StaticStatements.Count - 1];
						if (Editor.CaretLocation > ss.Location && Editor.CaretLocation <= ss.EndLocation)
						{
							showVariables = true;
						}
					}

					if (!showVariables)
						visibleMembers = MemberFilter.All ^ MemberFilter.Variables;
				}

				// Hide completion if having typed a '0.' literal
				else if (trackVars.LastParsedObject is IdentifierExpression &&
					   (trackVars.LastParsedObject as IdentifierExpression).Format == LiteralFormat.Scalar)
					return false;

				/*
				 * Handle module-scoped things:
				 * When typing a dot without anything following, trigger completion and show types, methods and vars that are located in the module & import scope
				 */
				else if (trackVars.LastParsedObject is TokenExpression &&
					((TokenExpression)trackVars.LastParsedObject).Token == DTokens.Dot)
				{
					visibleMembers = MemberFilter.Methods | MemberFilter.Types | MemberFilter.Variables | MemberFilter.TypeParameters;
					curBlock = Editor.SyntaxTree;
					curStmt = null;
				}

				// In a method, parse from the method's start until the actual caret position to get an updated insight
				if (visibleMembers.HasFlag(MemberFilter.Variables) &&
					curBlock is DMethod &&
					parsedBlock is BlockStatement)
				{}
				else
					curStmt = null;
			}
			return true;
		}

		public static ISyntaxRegion FindCurrentCaretContext(IEditorData editor, 
			out ParserTrackerVariables trackerVariables, 
			ref IBlockNode currentScope, 
			out IStatement currentStatement)
		{
			if(currentScope == null)
				currentScope = DResolver.SearchBlockAt (editor.SyntaxTree, editor.CaretLocation, out currentStatement);

			if (currentScope == null) {
				trackerVariables = null;
				currentStatement = null;
				return null;
			}

			bool ParseDecl = false;

			int blockStart = 0;
			var blockStartLocation = currentScope != null ? currentScope.BlockStartLocation : editor.CaretLocation;

			if (currentScope is DMethod)
			{
				var block = (currentScope as DMethod).GetSubBlockAt(editor.CaretLocation);

				if (block != null)
					blockStart = DocumentHelper.GetOffsetByRelativeLocation (editor.ModuleCode, editor.CaretLocation, editor.CaretOffset, blockStartLocation = block.Location);
				else {
					currentScope = currentScope.Parent as IBlockNode;
					return FindCurrentCaretContext (editor, out trackerVariables, ref currentScope, out currentStatement);
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
					trackerVariables = psr.TrackerVariables;
					return ret;
				}

			trackerVariables = null;
			currentStatement = null;
			return null;
		}
	}
}
