//
// CompletionProviderVisitor.cs
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
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using System.Collections.Generic;
using D_Parser.Completion.Providers;
using D_Parser.Resolver.ASTScanner;

namespace D_Parser.Completion
{
	public class CompletionProviderVisitor : DefaultDepthFirstVisitor
	{
		#region Properties
		bool halt; 
		public IBlockNode scopedBlock;
		public IStatement scopedStatement;

		bool explicitlyNoCompletion;

		bool handlesBaseClasses;
		DClassLike handledClass;

		public AbstractCompletionProvider GeneratedProvider { 
			get{ 
				return prv ?? (explicitlyNoCompletion ? null : 
					new CtrlSpaceCompletionProvider(cdgen) { curBlock = scopedBlock, curStmt = scopedStatement }); 
			}
		}
		AbstractCompletionProvider prv;
		readonly ICompletionDataGenerator cdgen;
		#endregion

		public CompletionProviderVisitor(ICompletionDataGenerator cdg, char enteredChar = '\0')
		{
			this.cdgen = cdg;
			explicitlyNoCompletion = char.IsWhiteSpace (enteredChar);
		}

		#region Nodes
		public override void VisitDNode (DNode n)
		{
			if (n.NameHash == DTokens.IncompleteIdHash) {
				explicitlyNoCompletion = true;
				halt = true;
			}
			else
				base.VisitDNode (n);
		}

		public override void Visit (DClassLike n)
		{
			if (!halt) {
				handlesBaseClasses = true;
				handledClass = n;
				foreach (var bc in n.BaseClasses)
					bc.Accept (this);
				handlesBaseClasses = false;

				if (!halt)
					VisitBlock (n);
			}
		}

		public override void VisitChildren (IBlockNode block)
		{
			var b = scopedBlock;
			var s = scopedStatement;
			scopedStatement = null;
			scopedBlock = block;
			base.VisitChildren (block);
			scopedBlock = b;
			scopedStatement = s;
		}
		#endregion

		#region TypeDeclarations
		public override void Visit (DTokenDeclaration td)
		{
			if (td.Token == DTokens.Incomplete) {
				if (handlesBaseClasses) {
					MemberFilter vis;
					if (handledClass.ClassType == DTokens.Interface)
						vis = MemberFilter.Interfaces | MemberFilter.Templates;
					else
						vis = MemberFilter.Classes | MemberFilter.Interfaces | MemberFilter.Templates;

					prv = new CtrlSpaceCompletionProvider (cdgen) { curBlock = handledClass, visibleMembers = vis };
				} else
					prv = new MemberCompletionProvider (cdgen, td.InnerDeclaration, scopedBlock, scopedStatement);

				halt = true;
			} else
				base.Visit (td);
		}

		public override void Visit (IdentifierDeclaration td)
		{
			if (td.IdHash == DTokens.IncompleteIdHash) {
				halt = true;
				if(td.InnerDeclaration != null)
					prv = new MemberCompletionProvider (cdgen, td.InnerDeclaration, scopedBlock, scopedStatement);
			}
			else
				base.Visit (td);
		}

		static bool IsIncompleteDeclaration(ITypeDeclaration x)
		{
			if(x is DTokenDeclaration)
				return (x as DTokenDeclaration).Token == DTokens.Incomplete;
			if(x is IdentifierDeclaration)
				return (x as IdentifierDeclaration).IdHash == DTokens.IncompleteIdHash;
			return false;
		}
		#endregion

		#region Attributes
		public override void VisitAttribute (Modifier a)
		{
			if (a.ContentHash == DTokens.IncompleteIdHash) {
				prv = new AttributeCompletionProvider (cdgen) { Attribute = a };
				halt = true;
			}
			else
				base.VisitAttribute (a);
		}

		public override void Visit (ScopeGuardStatement s)
		{
			if (s.GuardedScope == DTokens.IncompleteId) {
				prv = new ScopeAttributeCompletionProvider (cdgen);
				halt = true;
			}
			else
				base.Visit (s);
		}

		public override void VisitAttribute (PragmaAttribute a)
		{
			if (a.Arguments != null && 
				a.Arguments.Length>0 &&
				IsIncompleteExpression (a.Arguments[a.Arguments.Length-1])) {
				prv = new AttributeCompletionProvider (cdgen) { Attribute=a };
				halt = true;
			}
			else
				base.VisitAttribute (a);
		}

		public override void VisitAttribute (UserDeclarationAttribute a)
		{
			if (a.AttributeExpression != null && 
				a.AttributeExpression.Length>0 &&
				IsIncompleteExpression (a.AttributeExpression[0])) {
				prv = new PropertyAttributeCompletionProvider (cdgen);
				halt = true;
			}
			else
				base.VisitAttribute (a);
		}
		#endregion

		#region Statements
		public override void VisitAbstractStmt (AbstractStatement stmt)
		{
			scopedStatement = stmt;
			base.VisitAbstractStmt (stmt);
		}

		public override void Visit (ModuleStatement s)
		{
			if (IsIncompleteDeclaration (s.ModuleName)) {
				scopedStatement = s;
				prv = new ModuleStatementCompletionProvider (cdgen);
				halt = true;
			}
			else
				base.Visit (s);
		}

		public override void VisitImport (ImportStatement.Import i)
		{
			if (IsIncompleteDeclaration (i.ModuleIdentifier)) {
				prv = new ImportStatementCompletionProvider (cdgen, i);
				halt = true;
			}
			else
				base.VisitImport (i);
		}

		ImportStatement.ImportBindings curBindings;
		public override void VisitImport (ImportStatement.ImportBinding i)
		{
			if (!halt) {
				if (IsIncompleteDeclaration (i.Symbol)) {
					prv = new ImportStatementCompletionProvider (cdgen, curBindings);
					halt = true;
				} else
					base.VisitImport (i);
			}
		}

		public override void VisitImport (ImportStatement.ImportBindings i)
		{
			curBindings = i;
			base.VisitImport (i);
			curBindings = null;
		}

		public override void Visit (ForeachStatement s)
		{
			var decls = s.Declarations;
			var lastDecl = decls [decls.Length - 1];
			if (lastDecl.NameHash == DTokens.IncompleteIdHash) {
				scopedStatement = s;
				halt = true;
				explicitlyNoCompletion = lastDecl.Type != null;
			}
			else
				base.Visit (s);
		}
		#endregion

		#region Expressions
		public override void VisitChildren (ContainerExpression x)
		{
			if(!halt)
				base.VisitChildren (x);
		}

		public override void Visit (TokenExpression e)
		{
			if (e.Token == DTokens.Incomplete) {
				halt = true;
			}
		}

		static bool IsIncompleteExpression(IExpression x)
		{
			return x is TokenExpression && (x as TokenExpression).Token == DTokens.Incomplete;
		}

		public override void Visit (PostfixExpression_Access x)
		{
			if (IsIncompleteExpression(x.AccessExpression)) {
				halt = true;
				if (x.PostfixForeExpression is DTokenDeclaration && (x.PostfixForeExpression as DTokenDeclaration).Token == DTokens.Dot) {
					// Handle module-scoped things:
					// When typing a dot without anything following, trigger completion and show types, methods and vars that are located in the module & import scope
					prv = new CtrlSpaceCompletionProvider (cdgen) { 
						curBlock = scopedBlock, 
						visibleMembers = MemberFilter.Methods | MemberFilter.Types | MemberFilter.Variables | MemberFilter.TypeParameters 
					};
				}
				else
					prv = new MemberCompletionProvider (cdgen, x.PostfixForeExpression, scopedBlock, scopedStatement);
			}
			else
				base.Visit (x);
		}

		public override void Visit (TraitsExpression x)
		{
			if(x.Keyword == DTokens.IncompleteId)
			{
				prv = new TraitsExpressionCompletionProvider(cdgen);
				halt = true;
			}
			else
				base.Visit (x);
		}

		public override void Visit (NewExpression x)
		{
			if (IsIncompleteDeclaration (x.Type)) {
				halt = true;
				prv = new CtrlSpaceCompletionProvider (cdgen) { visibleMembers = MemberFilter.Types, curBlock = scopedBlock, curStmt = scopedStatement };
			}
			else
				base.Visit (x);
		}
		#endregion
	}
}

