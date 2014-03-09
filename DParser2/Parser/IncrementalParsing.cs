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
using System.Collections.Generic;

namespace D_Parser.Parser
{
	public static class IncrementalParsing
	{
		#region DMethod updating
		public static IBlockNode UpdateBlockPartly(this BlockStatement bs, IEditorData ed, out bool isInsideNonCodeSegment)
		{
			return UpdateBlockPartly (bs, ed.ModuleCode, ed.CaretOffset, ed.CaretLocation, out isInsideNonCodeSegment);
		}

		public static IBlockNode UpdateBlockPartly(this BlockStatement bs, string code, int caretOffset, CodeLocation caretLocation, out bool isInsideNonCodeSegment)
		{
			isInsideNonCodeSegment = false;
			var finalParentMethod = bs.ParentNode as DMethod;
			var finalStmtsList = bs._Statements;

			var startLoc = bs.Location;
			int startStmtIndex;
			for(startStmtIndex = finalStmtsList.Count-1; startStmtIndex >= 0; startStmtIndex--) {
				var n = finalStmtsList [startStmtIndex];
				if (n.EndLocation.Line > 0 && n.EndLocation.Line < caretLocation.Line) {
					startLoc = --startStmtIndex == -1 ? 
						bs.Location : finalStmtsList [startStmtIndex].EndLocation;
					break;
				}
			}

			var startOff = startLoc.Line > 1 ? DocumentHelper.GetOffsetByRelativeLocation (code, caretLocation, caretOffset, startLoc) : 0;

			if (startOff >= caretOffset)
				return null;

			var tempParentBlock = new DMethod();//new DBlockNode();
			var tempBlockStmt = new BlockStatement { ParentNode = tempParentBlock };
			tempParentBlock.Body = tempBlockStmt;
			tempBlockStmt.Location = startLoc;
			tempParentBlock.Location = startLoc;

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

					var stmt = p.Statement (true, false, tempParentBlock, tempBlockStmt);
					if (stmt != null)
						tempBlockStmt.Add(stmt);
					else
						p.Step();
				}

				tempBlockStmt.EndLocation = new CodeLocation(p.la.Column+1,p.la.Line);
				tempParentBlock.EndLocation = tempBlockStmt.EndLocation;

				if(isInsideNonCodeSegment = p.Lexer.endedWhileBeingInNonCodeSequence)
					return null;
			}

			DoubleDeclarationSilencer.RemoveDoubles(finalParentMethod, tempParentBlock);

			tempParentBlock.Parent = finalParentMethod;
			//tempParentBlock.Add(new PseudoStaticStmt { Block = tempBlockStmt, ParentNode = tempParentBlock, Location = tempBlockStmt.Location, EndLocation = tempBlockStmt.EndLocation });

			return tempParentBlock;
		}
		/*
		class PseudoStaticStmt : AbstractStatement,StaticStatement
		{
			public BlockStatement Block;

			public DAttribute[] Attributes
			{
				get	{ return null; }
				set	{}
			}

			public override R Accept<R>(StatementVisitor<R> vis)
			{
				return Block.Accept(vis);
			}

			public override void Accept(StatementVisitor vis)
			{
				Block.Accept(vis);
			}

			public override string ToCode()
			{
				return null;
			}
		}*/
		#endregion

		#region DBlockNode updating
		public static DBlockNode UpdateBlockPartly(this DBlockNode bn, IEditorData ed, out bool isInsideNonCodeSegment)
		{
			return UpdateBlockPartly (bn, ed.ModuleCode, ed.CaretOffset, ed.CaretLocation, out isInsideNonCodeSegment);
		}

		public static DBlockNode UpdateBlockPartly(this DBlockNode bn, string code,
			int caretOffset, CodeLocation caretLocation, out bool isInsideNonCodeSegment)
		{
			isInsideNonCodeSegment = false;

			// Get the end location of the declaration that appears before the caret.
			var startLoc = bn.BlockStartLocation;
			int startDeclIndex;
			for(startDeclIndex = bn.Children.Count-1; startDeclIndex >= 0; startDeclIndex--) {
				var n = bn.Children [startDeclIndex];
				if (n.EndLocation.Line > 0 && n.EndLocation.Line < caretLocation.Line) {
					startLoc = --startDeclIndex == -1 ? 
						bn.BlockStartLocation : bn.Children [startDeclIndex].EndLocation;
					break;
				}
			}

			var startOff = startLoc.Line > 1 ? DocumentHelper.GetOffsetByRelativeLocation (code, caretLocation, caretOffset, startLoc) : 0;

			// Immediately break to waste no time if there's nothing to parse
			if (startOff >= caretOffset)
				return null;

			// Get meta block stack so they can be registered while parsing 
			//var metaDecls = bn.GetMetaBlockStack (startLoc, true, false);

			// Parse region from start until caret for maximum efficiency
			var tempBlock = bn is DEnum ? new DEnum() : new DBlockNode();
			tempBlock.BlockStartLocation = startLoc;
			
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

					// Update the actual tempBlock as well as methods/other blocks' end location that just appeared while parsing the code incrementally,
					// so they are transparent to SearchBlockAt
					var block = tempBlock as IBlockNode;
					while(block != null && 
						(block.EndLocation.Line < 1 || block.EndLocation == p.la.Location)){
						block.EndLocation = new CodeLocation(p.la.Column+1,p.la.Line);
						if(block.Children.Count == 0)
							break;
						block = block.Children[block.Count-1] as IBlockNode;
					}

					if(isInsideNonCodeSegment = p.Lexer.endedWhileBeingInNonCodeSequence)
						return null;

					tempBlock.EndLocation = new CodeLocation(p.la.Column + 1, p.la.Line);
				}
			}catch(Exception ex) {
				Console.WriteLine (ex.Message);
			}

			DoubleDeclarationSilencer.RemoveDoubles(bn, tempBlock);

			tempBlock.Parent = bn;

			return tempBlock;
		}
		#endregion

		class DoubleDeclarationSilencer : DefaultDepthFirstVisitor
		{
			List<INode> originalDeclarations = new List<INode>();
			bool secondRun = false;

			public static void RemoveDoubles(IBlockNode originalAst, IBlockNode blockToClean)
			{
				var rem = new DoubleDeclarationSilencer();
				originalAst.Accept(rem);
				rem.secondRun = true;
				blockToClean.Accept(rem);
			}

			public override void VisitDNode(DNode n)
			{
				if(!secondRun)
				{
					originalDeclarations.Add(n);
					return;
				}

				foreach(var decl in originalDeclarations)
				{
					if (decl.NameHash == n.NameHash &&
						decl.GetType() == n.GetType() &&
						TryCompareNodeEquality(decl as DNode, n))
					{
						n.NameHash = 0; // Don't even risk referencing issues and just hide parsed stuff
						return;
					}
				}
			}

			static bool TryCompareNodeEquality(DNode orig, DNode copy)
			{
				var dm1 = orig as DMethod;
				var dm2 = copy as DMethod;
				if(dm1 != null && dm2 != null)
				{
					if(dm1.Parameters.Count == dm2.Parameters.Count)
						return true;
					// TODO: Don't check parameter details for now
					return false;
				}

				return true;
			}

			public override void Visit(ExpressionStatement s) { }
			public override void VisitChildren(Dom.Expressions.ContainerExpression x) { }
		}
	}
}

