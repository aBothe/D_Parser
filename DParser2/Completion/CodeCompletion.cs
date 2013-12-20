//
// CompletionService.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2013 Alexander Bothe
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using D_Parser.Completion;
using D_Parser.Parser;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Completion
{
	public static class CodeCompletion
	{
		/// <summary>
		/// Generates the completion data.
		/// </summary>
		/// <param name="checkForCompletionAllowed">Set to <c>false</c> if you already ensured that completion can occur in the current editing context.</param>
		public static bool GenerateCompletionData(IEditorData editor, 
			ICompletionDataGenerator completionDataGen, char triggerChar, bool alreadyCheckedCompletionContext = false)
		{
			if(!alreadyCheckedCompletionContext && !IsCompletionAllowed(editor, triggerChar))
				return false;

			IBlockNode _b = null;
			IStatement _s;
			var sr = FindCurrentCaretContext(editor, ref _b, out _s);

			if (editor.CaretLocation > _b.EndLocation) {
				_b = editor.SyntaxTree;
				_s = null;
			}

			var complVis = new CompletionProviderVisitor (completionDataGen, triggerChar) { scopedBlock = _b, scopedStatement = _s };
			if (sr is INode)
				(sr as INode).Accept (complVis);
			else if (sr is IStatement)
				(sr as IStatement).Accept (complVis);
			else if (sr is IExpression)
				(sr as IExpression).Accept (complVis);

			if (complVis.GeneratedProvider == null)
				return false;

			complVis.GeneratedProvider.BuildCompletionData(editor, triggerChar);

			return true;
		}

		public static bool IsCompletionAllowed(IEditorData Editor, char enteredChar)
		{
			if (enteredChar == '(')
				return false;

			if (Editor.CaretOffset > 0)
			{
				if (Editor.CaretLocation.Line == 1 && Editor.ModuleCode.Length > 0 && Editor.ModuleCode[0] == '#')
					return false;

				if (enteredChar == '.' || enteredChar == '_')
				{
					// Don't complete on a double/multi-dot
					if (Editor.CaretOffset > 1 && Editor.ModuleCode[Editor.CaretOffset - 2] == enteredChar) 
						// ISSUE: When a dot was typed, off-1 is the dot position, 
						// if a letter was typed, off-1 is the char before the typed letter..
						return false;
				}
				// If typing a begun identifier, return immediately
				else if ((DTokens.IsIdentifierChar(enteredChar) || enteredChar == '\0') &&
					DTokens.IsIdentifierChar(Editor.ModuleCode[Editor.CaretOffset - 1]))
					return false;

				return !CaretContextAnalyzer.IsInCommentAreaOrString(Editor.ModuleCode, Editor.CaretOffset);
			}

			return true;
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

