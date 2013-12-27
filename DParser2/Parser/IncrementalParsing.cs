//
// IncrementalParsing.cs
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
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Completion;
using D_Parser.Dom.Statements;

namespace D_Parser.Parser
{
	public static class IncrementalParsing
	{
		#region DMethod updating
		public static void UpdateBlockPartly(this BlockStatement bs, IEditorData ed, out bool isInsideNonCodeSegment)
		{
			UpdateBlockPartly (bs, ed.ModuleCode, ed.CaretOffset, ed.CaretLocation, out isInsideNonCodeSegment);
		}

		public static void UpdateBlockPartly(this BlockStatement bs, string code, int caretOffset, CodeLocation caretLocation, out bool isInsideNonCodeSegment)
		{
			isInsideNonCodeSegment = false;
			var finalParentMethod = bs.ParentNode as DMethod;
			var finalStmtsList = bs._Statements;

			var startLoc = bs.Location;
			int startStmtIndex;
			for(startStmtIndex = finalStmtsList.Count-1; startStmtIndex >= 0; startStmtIndex--) {
				var n = finalStmtsList [startStmtIndex];
				if (n.EndLocation.Line > 0 && n.EndLocation < caretLocation) {
					startLoc = --startStmtIndex == -1 ? 
						bs.Location : finalStmtsList [startStmtIndex].EndLocation;
					break;
				}
			}

			var startOff = startLoc.Line > 1 ? DocumentHelper.GetOffsetByRelativeLocation (code, caretLocation, caretOffset, startLoc) : 0;

			if (startOff >= caretOffset)
				return;

			var tempParentBlock = new DBlockNode ();
			var tempBlockStmt = new BlockStatement { ParentNode = tempParentBlock };

			try{
				using (var sv = new StringView (code, startOff, caretOffset - startOff))
				using (var p = DParser.Create(sv)) {
					p.Lexer.SetInitialLocation (startLoc);
					p.Step ();

					if(p.laKind == DTokens.OpenCurlyBrace)
						p.Step();

					while (!p.IsEOF) {

						if (p.laKind == DTokens.CloseCurlyBrace) {
							p.Step ();
							/*if (metaDecls.Count > 0)
								metaDecls.RemoveAt (metaDecls.Count - 1);*/
							continue;
						}

						tempBlockStmt.Add(p.Statement (true, false, tempParentBlock, tempBlockStmt));
					}

					tempBlockStmt.EndLocation = new CodeLocation(p.la.Column+1,p.la.Line);

					if(isInsideNonCodeSegment = p.Lexer.endedWhileBeingInNonCodeSequence)
						return;
				}
			}catch(Exception ex) {
				Console.WriteLine (ex.Message);
			}

			// Remove old statements from startLoc until caretLocation
			for (int i = startStmtIndex + 1; i < finalStmtsList.Count; i++) {
				var d = finalStmtsList [i];
				if (d.Location >= caretLocation)
					break;
				finalStmtsList.Remove (d);
			}

			// Insert new statements
			if (tempBlockStmt.EndLocation > bs.EndLocation)
				bs.EndLocation = tempBlockStmt.EndLocation;
			foreach (var stmt in tempBlockStmt._Statements)
				stmt.Parent = bs;
			finalStmtsList.InsertRange(startStmtIndex+1, tempBlockStmt._Statements);
			
			if (finalParentMethod != null) {
				var finalParentChildren = finalParentMethod.AdditionalChildren;
				// Remove old parent block children
				int startDeclIndex;
				for(startDeclIndex = finalParentChildren.Count-1; startDeclIndex >= 0; startDeclIndex--) {
					var n = finalParentChildren [startDeclIndex];
					if (n == null) {
						finalParentChildren.RemoveAt (startDeclIndex);
						continue;
					}
					if (n.Location < startLoc)
						break;
					if (n.Location < caretLocation)
						finalParentChildren.RemoveAt(startDeclIndex);
				}

				// Insert new special declarations
				foreach (var decl in tempParentBlock)
					decl.Parent = finalParentMethod;
				finalParentChildren.InsertRange(startDeclIndex+1, tempParentBlock);

				finalParentMethod.UpdateChildrenArray ();
				if (bs.EndLocation > finalParentMethod.EndLocation)
					finalParentMethod.EndLocation = bs.EndLocation;
			}

			//TODO: Handle DBlockNode parents?
		}
		#endregion

		#region DBlockNode updating
		public static void UpdateBlockPartly(this DBlockNode bn, IEditorData ed, out bool isInsideNonCodeSegment)
		{
			UpdateBlockPartly (bn, ed.ModuleCode, ed.CaretOffset, ed.CaretLocation, out isInsideNonCodeSegment);
		}

		public static void UpdateBlockPartly(this DBlockNode bn, string code,
			int caretOffset, CodeLocation caretLocation, out bool isInsideNonCodeSegment)
		{
			isInsideNonCodeSegment = false;

			// Get the end location of the declaration that appears before the caret.
			var startLoc = bn.BlockStartLocation;
			int startDeclIndex;
			for(startDeclIndex = bn.Children.Count-1; startDeclIndex >= 0; startDeclIndex--) {
				var n = bn.Children [startDeclIndex];
				if (n.EndLocation.Line > 0 && n.EndLocation < caretLocation) {
					startLoc = --startDeclIndex == -1 ? 
						bn.BlockStartLocation : bn.Children [startDeclIndex].EndLocation;
					break;
				}
			}

			var startOff = startLoc.Line > 1 ? DocumentHelper.GetOffsetByRelativeLocation (code, caretLocation, caretOffset, startLoc) : 0;

			// Immediately break to waste no time if there's nothing to parse
			if (startOff >= caretOffset)
				return;

			// Get meta block stack so they can be registered while parsing 
			//var metaDecls = bn.GetMetaBlockStack (startLoc, true, false);

			// Parse region from start until caret for maximum efficiency
			var tempBlock = bn is DEnum ? new DEnum() : new DBlockNode();
			try{
				using (var sv = new StringView (code, startOff, caretOffset - startOff))
				using (var p = DParser.Create(sv)) {
					p.Lexer.SetInitialLocation (startLoc);
					p.Step ();

					if(p.laKind == DTokens.OpenCurlyBrace)
						p.Step();

					// Enum bodies
					if(bn is DEnum)
					{
						do
						{
							if(p.laKind == DTokens.Comma)
								p.Step();
							var laBackup = p.la;
							p.EnumValue(tempBlock as DEnum);
							if(p.la == laBackup)
								break;
						}
						while(!p.IsEOF);
					}
					else // Normal class/module bodies
					{
						if(p.laKind == DTokens.Module && bn is DModule)
							tempBlock.Add(p.ModuleDeclaration());

						while (!p.IsEOF) {
							// 
							if (p.laKind == DTokens.CloseCurlyBrace) {
								p.Step ();
								/*if (metaDecls.Count > 0)
									metaDecls.RemoveAt (metaDecls.Count - 1);*/
								continue;
							}

							p.DeclDef (tempBlock);
						}
					}

					tempBlock.EndLocation = new CodeLocation(p.la.Column+1,p.la.Line);

					if(isInsideNonCodeSegment = p.Lexer.endedWhileBeingInNonCodeSequence)
						return;
				}
			}catch(Exception ex) {
				Console.WriteLine (ex.Message);
			}

			// Remove old static stmts, declarations and meta blocks from bn
			/*bn.MetaBlocks;*/
			int startStatStmtIndex;
			for (startStatStmtIndex = bn.StaticStatements.Count - 1; startStatStmtIndex >= 0; startStatStmtIndex--) {
				var ss = bn.StaticStatements [startStatStmtIndex];
				if (ss.Location >= startLoc && ss.Location <= caretLocation)
					bn.StaticStatements.RemoveAt (startStatStmtIndex);
				else if(ss.EndLocation < startLoc)
					break;
			}

			for (int i = startDeclIndex + 1; i < bn.Count; i++) {
				var d = bn.Children [i];
				if (d.Location >= caretLocation)
					break;
				bn.Children.Remove (d);
			}

			// Insert new static stmts, declarations and meta blocks(?) into bn
			if (tempBlock.EndLocation > bn.EndLocation)
				bn.EndLocation = tempBlock.EndLocation;
			foreach (var n in tempBlock.Children) {
				if(n != null)
					bn.Children.Insert (n, ++startDeclIndex);
			}

			bn.StaticStatements.InsertRange(startStatStmtIndex+1, tempBlock.StaticStatements);
		}
		#endregion
	}
}

