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
using System.Threading;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Completion
{//TODO: Flexible lambda completion
	public static class CodeCompletion
	{
		/// <summary>
		/// Generates the completion data.
		/// </summary>
		/// <param name="alreadyCheckedCompletionContext">Set to <c>false</c> if you already ensured that completion can occur in the current editing context.</param>
		public static bool GenerateCompletionData(IEditorData editor, 
			ICompletionDataGenerator completionDataGen, char triggerChar, bool alreadyCheckedCompletionContext = false)
		{
			if(!alreadyCheckedCompletionContext && !IsCompletionAllowed(editor, triggerChar))
				return false;

			IBlockNode _b = null;
			bool inNonCode;

			var sr = FindCurrentCaretContext(editor, ref _b, out inNonCode);

			if (inNonCode)
				return false;

			if (_b == null || editor.CaretLocation > _b.EndLocation) {
				_b = editor.SyntaxTree;
			}

			var complVis = new CompletionProviderVisitor (completionDataGen, editor, triggerChar) { scopedBlock = _b };
			if (sr is INode node)
				node.Accept (complVis);
			else if (sr is IStatement statement)
				statement.Accept (complVis);
			else if (sr is IExpression expression)
				expression.Accept (complVis);

			if (complVis.GeneratedProvider == null)
				return false;

			complVis.GeneratedProvider.BuildCompletionData(editor, triggerChar);

			return true;
		}

		static bool IsCompletionAllowed(IEditorData Editor, char enteredChar)
		{/*'debug(', 'version(', 'pragma(' etc. shall trigger, but not 'foo('
			if (enteredChar == '(')
				return false;
			*/
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
			}

			return true;
		}

		public static ISyntaxRegion FindCurrentCaretContext(IEditorData editor, 
			ref IBlockNode currentScope,
			out bool isInsideNonCodeSegment)
		{
			isInsideNonCodeSegment = false;

			if(currentScope == null)
				currentScope = ASTSearchHelper.SearchBlockAt (editor.SyntaxTree, editor.CaretLocation);

			if (currentScope == null)
				return null;

			// class asd : |
			if (currentScope.BlockStartLocation > editor.CaretLocation)
				currentScope = currentScope.Parent as IBlockNode;

			BlockStatement blockStmt;
			// Always skip lambdas as they're too quirky for accurate scope calculation // ISSUE: May be other anon symbols too?
			if (currentScope is DMethod dm && (dm.SpecialType & DMethod.MethodType.Lambda) != 0)
				currentScope = dm.Parent as IBlockNode;

			if (currentScope is DMethod &&
			    (blockStmt = (currentScope as DMethod).GetSubBlockAt (editor.CaretLocation)) != null) {
				var tempBlock = blockStmt.UpdateBlockPartly (editor, out isInsideNonCodeSegment);
				if (tempBlock == null)
					return null;
				currentScope = ASTSearchHelper.SearchBlockAt (tempBlock, editor.CaretLocation);
			}else {
				while (currentScope is DMethod)
					currentScope = currentScope.Parent as IBlockNode;
				if (currentScope == null)
					return null;

				var tempBlock = (currentScope as DBlockNode).UpdateBlockPartly (editor, out isInsideNonCodeSegment);
				currentScope = ASTSearchHelper.SearchBlockAt (tempBlock, editor.CaretLocation);
			}
			return currentScope;
		}

		public static void DoTimeoutableCompletionTask(ICompletionDataGenerator cdgen, ResolutionContext ctxt, Action ac, CancellationToken token)
		{
			if (token == CancellationToken.None)
			{
				DoTimeoutableCompletionTask(cdgen, ctxt, ac);
				return;
			}

			if (token.IsCancellationRequested)
				return;

			ctxt.CancellationToken = token;

			ac();

			if (token.IsCancellationRequested && cdgen != null)
				cdgen.NotifyTimeout ();
		}

		/// <param name="cdgen">Can be null.</param>
		/// <param name="ctxt"></param>
		/// <param name="ac"></param>
		/// <param name="timeout">if int.MinValue, this value will be made the default CompletionTimeout.</param>
		public static void DoTimeoutableCompletionTask(ICompletionDataGenerator cdgen,ResolutionContext ctxt, Action ac, int timeout = int.MinValue)
		{
            if (ctxt.CancellationToken != CancellationToken.None)
            {
                // inherit existing cancellation token
                ac();
                return;
            }
			var cts = new CancellationTokenSource ();
			ctxt.CancellationToken = cts.Token;

			if (timeout == int.MinValue)
				timeout = ctxt.CompletionOptions.CompletionTimeout;

			#if NET40
			if(timeout > 0)
				Task.Factory.StartNew(()=>{Thread.Sleep(timeout); cts.Cancel();});
			#else
			if (timeout > 0)
				cts.CancelAfter (timeout);
			#endif

			ac ();

			if (cts.IsCancellationRequested)
				cdgen?.NotifyTimeout ();
		}
	}
}

