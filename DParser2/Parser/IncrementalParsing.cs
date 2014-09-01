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
		abstract class IncrementalParsingBase<T> where T : ISyntaxRegion
		{
			public IBlockNode ParseIncrementally(T sr, IEditorData ed, out bool isInsideNonCodeSegment)
			{
				return ParseIncrementally(sr, ed.ModuleCode, ed.CaretOffset, ed.CaretLocation, out isInsideNonCodeSegment);
			}

			public IBlockNode ParseIncrementally(T sr, string code, int caretOffset, CodeLocation caretLocation, out bool isInsideNonCodeSegment)
			{
				isInsideNonCodeSegment = false;

				// Get the end location of the declaration that appears before the caret.
				var startLoc = GetBlockStartLocation(sr);
				int startStmtIndex;
				for (startStmtIndex = ChildCount(sr) - 1; startStmtIndex >= 0; startStmtIndex--)
				{
					var n = ChildAt(sr,startStmtIndex);
					if (n.EndLocation.Line > 0 && n.EndLocation.Line < caretLocation.Line)
					{
						if(startStmtIndex > 0)
							startLoc = ChildAt(sr,--startStmtIndex).EndLocation;
						break;
					}
				}

				var startOff = startLoc.Line > 1 ? DocumentHelper.GetOffsetByRelativeLocation(code, caretLocation, caretOffset, startLoc) : 0;

				// Immediately break to waste no time if there's nothing to parse
				if (startOff >= caretOffset)
					return null;

				var t = PrepareParsing(sr,startLoc);

				using (var sv = new StringView(code, startOff, caretOffset - startOff))
				using (var p = DParser.Create(sv))
				{
					p.Lexer.SetInitialLocation(startLoc);
					p.Step();

					if (p.laKind == DTokens.OpenCurlyBrace)
						p.Step();

					while (!p.IsEOF && Parse(t, p)) ;

					if (isInsideNonCodeSegment = p.Lexer.endedWhileBeingInNonCodeSequence)
						return null;

					return FinishParsing(t, p);
				}
			}

			protected abstract T PrepareParsing(T sr,CodeLocation startLoc);
			protected abstract bool Parse(T tempBlock, DParser p);
			protected abstract IBlockNode FinishParsing(T tempBlock, DParser p);

			protected abstract int ChildCount(T sr);
			protected abstract ISyntaxRegion ChildAt(T sr, int i);

			protected virtual CodeLocation GetBlockStartLocation(T sr)
			{
				return sr.Location;
			}
		}

		#region DMethod updating
		class BlockStmtIncrParsing : IncrementalParsingBase<BlockStatement>
		{
			DMethod tempParentBlock;
			DMethod finalParentMethod;

			protected override BlockStatement PrepareParsing(BlockStatement bs,CodeLocation startLoc)
			{
				finalParentMethod = bs.ParentNode as DMethod;

				tempParentBlock = new DMethod();//new DBlockNode();
				tempParentBlock.Attributes = finalParentMethod.Attributes; // assign given attributes to the temporary block for allowing the completion to check whether static or non-static items may be shown
				var tempBlockStmt = new BlockStatement { ParentNode = tempParentBlock };
				tempParentBlock.Body = tempBlockStmt;
				tempBlockStmt.Location = startLoc;
				tempParentBlock.Location = startLoc;

				return tempBlockStmt; 
			}

			protected override bool Parse(BlockStatement tempBlockStmt, DParser p)
			{
				if (p.laKind == DTokens.CloseCurlyBrace)
				{
					p.Step();
					/*if (metaDecls.Count > 0)
						metaDecls.RemoveAt (metaDecls.Count - 1);*/
					return true;
				}

				var stmt = p.Statement(true, false, tempParentBlock, tempBlockStmt);
				if (stmt != null)
					tempBlockStmt.Add(stmt);
				else
					p.Step();
				return true;
			}

			protected override IBlockNode FinishParsing(BlockStatement tempBlockStmt, DParser p)
			{
				tempBlockStmt.EndLocation = new CodeLocation(p.la.Column + 1, p.la.Line);
				tempParentBlock.EndLocation = tempBlockStmt.EndLocation;

				DoubleDeclarationSilencer.RemoveDoubles(finalParentMethod, tempParentBlock);

				tempParentBlock.Parent = finalParentMethod;

				return tempParentBlock;
			}

			protected override int ChildCount(BlockStatement sr)
			{
				return sr._Statements.Count;
			}

			protected override ISyntaxRegion ChildAt(BlockStatement sr, int i)
			{
				return sr._Statements[i];
			}
		}

		public static IBlockNode UpdateBlockPartly(this BlockStatement bs, IEditorData ed, out bool isInsideNonCodeSegment)
		{
			return new BlockStmtIncrParsing().ParseIncrementally(bs, ed, out isInsideNonCodeSegment);
		}

		public static IBlockNode UpdateBlockPartly(this BlockStatement bs, string code, int caretOffset, CodeLocation caretLocation, out bool isInsideNonCodeSegment)
		{
			return new BlockStmtIncrParsing().ParseIncrementally(bs, code, caretOffset, caretLocation, out isInsideNonCodeSegment);
		}
		#endregion

		#region DBlockNode updating
		class IncrBlockNodeParsing : IncrementalParsingBase<DBlockNode>
		{
			DBlockNode finalParentBlock;

			protected override DBlockNode PrepareParsing(DBlockNode bn, CodeLocation startLoc)
			{
				finalParentBlock = bn;
				var tempBlock = bn is DEnum ? new DEnum() : new DBlockNode();
				tempBlock.Attributes = bn.Attributes;
				tempBlock.BlockStartLocation = startLoc;
				return tempBlock;
			}

			protected override bool Parse(DBlockNode tempBlock, DParser p)
			{
				if (p.laKind == DTokens.CloseCurlyBrace)
				{
					p.Step();
					/*if (metaDecls.Count > 0)
						metaDecls.RemoveAt (metaDecls.Count - 1);*/
					return true;
				}

				// Enum bodies
				if (tempBlock is DEnum)
				{
					if (p.laKind == DTokens.Comma)
							p.Step();
					var laBackup = p.la;
					p.EnumValue(tempBlock as DEnum);
					if (p.la == laBackup)
						return false;
				}
				else // Normal class/module bodies
				{
					if (p.laKind == DTokens.Module)
					{
						tempBlock.Add(p.ModuleDeclaration());
						if (p.IsEOF)
							return false;
					}

					p.DeclDef(tempBlock);
				}

				return true;
			}

			protected override IBlockNode FinishParsing(DBlockNode tempBlock, DParser p)
			{
				// Update the actual tempBlock as well as methods/other blocks' end location that just appeared while parsing the code incrementally,
				// so they are transparent to SearchBlockAt
				var n = tempBlock as INode;
				while (n != null &&
					(n.EndLocation.Line < 1 || n.EndLocation == p.la.Location))
				{
					n.EndLocation = new CodeLocation(p.la.Column + 1, p.la.Line);
					var bn = n as IBlockNode;
					if (bn == null || bn.Children.Count == 0)
						break;
					n = bn.Children[bn.Count - 1];
				}

				tempBlock.EndLocation = new CodeLocation(p.la.Column + 1, p.la.Line);

				DoubleDeclarationSilencer.RemoveDoubles(finalParentBlock, tempBlock);

				tempBlock.Parent = finalParentBlock;

				return tempBlock;
			}

			protected override int ChildCount(DBlockNode sr)
			{
				return sr.Count;
			}

			protected override ISyntaxRegion ChildAt(DBlockNode sr, int i)
			{
				return sr.Children[i];
			}

			protected override CodeLocation GetBlockStartLocation(DBlockNode sr)
			{
				return sr.BlockStartLocation;
			}
		}

		public static IBlockNode UpdateBlockPartly(this DBlockNode bn, IEditorData ed, out bool isInsideNonCodeSegment)
		{
			return new IncrBlockNodeParsing().ParseIncrementally (bn, ed, out isInsideNonCodeSegment);
		}

		public static IBlockNode UpdateBlockPartly(this DBlockNode bn, string code, int caretOffset, CodeLocation caretLocation, out bool isInsideNonCodeSegment)
		{
			return new IncrBlockNodeParsing().ParseIncrementally(bn, code, caretOffset, caretLocation, out isInsideNonCodeSegment);
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
				if (n.NameHash == 0 || n.NameHash == DTokens.IncompleteIdHash)
					return;

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
						n.NameHash = -1; // Don't even risk referencing issues and just hide parsed stuff
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

