//
// ConditionsFrame.cs
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
using System.Collections.Generic;
using D_Parser.Dom;

namespace D_Parser.Resolver.ASTScanner
{
	/// <summary>
	/// Used for storing declaration environment constraints (debug/version/static if) in the AbstractVisitor.
	/// Unused atm.
	/// </summary>
	class ConditionsFrame
	{
		public MutableConditionFlagSet LocalConditions = new MutableConditionFlagSet();
		public Stack<IMetaDeclarationBlock> MetaBlocks = new Stack<IMetaDeclarationBlock> ();
		public IEnumerator<StaticStatement> StaticStatementEnum;
		public IEnumerator<IMetaDeclaration> MetaBlockEnum;
		public IMetaDeclaration nextMetaDecl;
		public StaticStatement nextStatStmt;

		public ConditionsFrame(IEnumerable<StaticStatement> ssEnum, IEnumerable<IMetaDeclaration> metaBlocks)
		{
			this.StaticStatementEnum = (IEnumerator<StaticStatement>)ssEnum.GetEnumerator();
			this.MetaBlockEnum = (IEnumerator<IMetaDeclaration>)metaBlocks.GetEnumerator();

			// opt-in: Initially check whether there are any static statements or meta decls available
			if(StaticStatementEnum.MoveNext())
				nextStatStmt = StaticStatementEnum.Current;
			else
				StaticStatementEnum = null;

			if(MetaBlockEnum.MoveNext())
				nextMetaDecl = MetaBlockEnum.Current;
			else
				MetaBlockEnum = null;
		}

		public bool MatchesConditions(DNode n)
		{
			return true;
		}

		public ISyntaxRegion GetNextMetaBlockOrStatStmt(CodeLocation until)
		{
			if (nextStatStmt == null && StaticStatementEnum != null) {
				if (StaticStatementEnum.MoveNext ())
					nextStatStmt = StaticStatementEnum.Current;
				else
					StaticStatementEnum = null;
			}
			if (nextMetaDecl == null && MetaBlockEnum != null) {
				if (MetaBlockEnum.MoveNext ())
					nextMetaDecl = MetaBlockEnum.Current;
				else
					MetaBlockEnum = null;
			}

			ISyntaxRegion sr;

			if (nextStatStmt != null) {
				if (nextMetaDecl == null)
					sr = nextStatStmt;
				else
					sr = nextStatStmt.First (nextMetaDecl);
			} else if (nextMetaDecl != null)
				sr = nextMetaDecl;
			else
				return null;

			return sr.Location < until ? sr : null;
		}

		public void PopMetaBlockDeclaration(CodeLocation untilEnd)
		{
			while (MetaBlocks.Count != 0 &&	MetaBlocks.Peek ().EndLocation < untilEnd) {
				var mb = MetaBlocks.Pop ();
			}
		}
	}
}

