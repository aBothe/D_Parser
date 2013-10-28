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
using System;
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

		protected virtual void OnScopedStatementChanged(IStatement stmt)
		{

		}

		#region Scoping visit overloads
		public override void VisitAbstractStmt (AbstractStatement stmt)
		{
			var back = ctxt.ScopedStatement;
			if (back != stmt) {
				ctxt.CurrentContext.Set (stmt);
				OnScopedStatementChanged (stmt);
			}
			base.VisitAbstractStmt (stmt);
			if (back != stmt)
				ctxt.CurrentContext.Set (back);
		}

		public override void VisitChildren (StatementContainingStatement stmt)
		{
			var back = ctxt.ScopedStatement;
			if (back != stmt) {
				ctxt.PushNewScope (ctxt.ScopedBlock, stmt);
				OnScopedStatementChanged (stmt);
			}
			base.VisitSubStatements (stmt);
			if (back != stmt)
				ctxt.Pop ();
		}

		public override void VisitBlock (DBlockNode bn)
		{
			var back = ctxt.ScopedBlock;
			if (bn != back) {
				ctxt.PushNewScope (bn);
				OnScopedBlockChanged (bn);
			}
			base.VisitBlock (bn);
			if(bn != back)
				ctxt.Pop ();
		}

		// Only for parsing the base class identifiers!
		public override void Visit (DClassLike dc)
		{
			var back = ctxt.ScopedBlock;
			if (back != dc) {
				ctxt.PushNewScope (dc);
				OnScopedBlockChanged (dc);
			}
			base.Visit (dc); 
			if(back != dc)
				ctxt.Pop ();
		}

		public override void Visit (DMethod dm)
		{
			var back = ctxt.ScopedBlock;
			if (back != dm) {
				ctxt.PushNewScope (dm);
				OnScopedBlockChanged (dm);
			}
			base.Visit (dm);
			if(back != dm)
				ctxt.Pop ();
		}
		#endregion
	}
}

