//
// AbstractResolutionVisitor.cs
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
using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Resolver.ASTScanner
{
	public class AbstractResolutionVisitor: DefaultDepthFirstVisitor
	{
		protected readonly ResolutionContext ctxt;

		public AbstractResolutionVisitor (ResolutionContext ctxt)
		{
			this.ctxt = ctxt;
		}

		protected virtual void OnScopedBlockChanged(IBlockNode bn)
		{

		}

		#region Scoping visit overloads
		public override void VisitAbstractStmt (AbstractStatement stmt)
		{
			using(ctxt.Push(stmt.ParentNode, stmt))
				base.VisitAbstractStmt (stmt);
		}

		public override void VisitChildren (StatementContainingStatement stmt)
		{
			using (ctxt.Push(stmt.ParentNode, stmt)) 
				base.VisitSubStatements(stmt);
		}

		public override void VisitBlock (DBlockNode bn)
		{
			var back = ctxt.ScopedBlock;
			using(ctxt.Push(bn)) {
				 if (ctxt.ScopedBlock != back)
					OnScopedBlockChanged (bn);
				 base.VisitBlock(bn);
			}
		}

		// Only for parsing the base class identifiers!
		public override void Visit (DClassLike dc)
		{
			var back = ctxt.ScopedBlock;
			using(ctxt.Push(dc)) {
				if(back != ctxt.ScopedBlock)
					OnScopedBlockChanged (dc);
				base.Visit(dc);
			}
		}

		public override void Visit (DMethod dm)
		{
			var back = ctxt.ScopedBlock;
			using (ctxt.Push(dm)) {
				if (back != ctxt.ScopedBlock)
					OnScopedBlockChanged(dm);
				base.Visit(dm);
			}
		}
		#endregion
	}
}

