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
{//TODO: Flexible lambda completion
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
			bool inNonCode;

			var sr = FindCurrentCaretContext(editor, ref _b, out _s, out inNonCode);

			if (inNonCode)
				return false;

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

		static bool IsCompletionAllowed(IEditorData Editor, char enteredChar)
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
			}

			return true;
		}

		public static ISyntaxRegion FindCurrentCaretContext(IEditorData editor, 
			ref IBlockNode currentScope, 
			out IStatement currentStatement,
			out bool isInsideNonCodeSegment)
		{
			isInsideNonCodeSegment = false;
			currentStatement = null;

			if(currentScope == null)
				currentScope = DResolver.SearchBlockAt (editor.SyntaxTree, editor.CaretLocation, out currentStatement);

			if (currentScope == null)
				return null;

			BlockStatement blockStmt;
			// Always skip lambdas as they're too quirky for accurate scope calculation // ISSUE: May be other anon symbols too?
			var dm = currentScope as DMethod;
			if (dm != null && (dm.SpecialType & DMethod.MethodType.Lambda) != 0)
				currentScope = dm.Parent as IBlockNode;

			if (currentScope is DMethod &&
			    (blockStmt = (currentScope as DMethod).GetSubBlockAt (editor.CaretLocation)) != null) {
				blockStmt.UpdateBlockPartly (editor, out isInsideNonCodeSegment);
				currentScope = DResolver.SearchBlockAt (currentScope, editor.CaretLocation, out currentStatement);
			}else {
				while (currentScope is DMethod)
					currentScope = currentScope.Parent as IBlockNode;
				if (currentScope == null)
					return null;

				(currentScope as DBlockNode).UpdateBlockPartly (editor, out isInsideNonCodeSegment);
				currentStatement = DResolver.GetStatementAt (currentScope as DBlockNode, editor.CaretLocation);
			}
			return currentScope;
		}
	}
}

