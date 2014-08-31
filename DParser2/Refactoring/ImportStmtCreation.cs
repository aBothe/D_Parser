//
// ImportStmtCreation.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2014 Alexander Bothe
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
using D_Parser.Dom.Statements;
using System.IO;
using D_Parser.Parser;
using D_Parser.Completion;

namespace D_Parser.Refactoring
{
	public static class ImportStmtCreation
	{
		/// <summary>
		/// Finds the last import statement and returns its end location (the position after the semicolon).
		/// If no import but module statement was found, the end location of this module statement will be returned.
		/// </summary>
		public static CodeLocation FindLastImportStatementEndLocation(DModule m, string moduleCode = null)
		{
			IStatement lastStmt = null;

			foreach (var s in m.StaticStatements)
				if (s is ImportStatement)
					lastStmt = s;
				else if (lastStmt != null)
					break;

			if (lastStmt != null)
				return lastStmt.EndLocation;

			if (m.OptionalModuleStatement != null)
				return m.OptionalModuleStatement.EndLocation;

			if (moduleCode != null)
				using(var sr = new StringReader(moduleCode))
				using (var lx = new Lexer(sr) { OnlyEnlistDDocComments = false })
				{
					lx.NextToken();

					if (lx.Comments.Count != 0)
						return lx.Comments[lx.Comments.Count - 1].EndPosition;
				}

			return new CodeLocation(1, 1);
		}

		public static void GenerateImportStatementForNode(INode n, IEditorData ed, ITextDocument doc)
		{
			var off = doc.LocationToOffset(FindLastImportStatementEndLocation(ed.SyntaxTree, ed.ModuleCode).Line + 1, 0);
			doc.Insert(off, "import " + (n.NodeRoot as DModule).ModuleName + ";" + doc.EolMarker);
		}
	}
}

