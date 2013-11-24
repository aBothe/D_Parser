//
// TypeReferenceFinder.cs
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
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using System.Threading;
using D_Parser.Resolver.ASTScanner;

namespace D_Parser.Refactoring
{
	public class TypeReferenceFinder : AbstractResolutionVisitor
	{
		#region Properties
		readonly Dictionary<IBlockNode, HashSet<int>> TypeCache = new Dictionary<IBlockNode, HashSet<int>>();
		//DModule ast;
		TypeReferencesResult result = new TypeReferencesResult();
		#endregion

		#region Constructor / IO
		protected TypeReferenceFinder (ResolutionContext ctxt) : base(ctxt)
		{
		}

		public static TypeReferencesResult Scan(DModule ast, ResolutionContext ctxt)
		{
			if (ast == null)
				return new TypeReferencesResult();

			var typeRefFinder = new TypeReferenceFinder(ctxt);

			ContextFrame backupFrame = null;

			if(ctxt.ScopedBlock == ast)
				backupFrame = ctxt.Pop ();

			if (ctxt.CurrentContext == null)
			{
				ctxt.Push(backupFrame);
				backupFrame = null;
			}

			//typeRefFinder.ast = ast;
			// Enum all identifiers
			ast.Accept (typeRefFinder);

			if (backupFrame != null)
				ctxt.Push (backupFrame);

			// Crawl through all remaining expressions by evaluating their types and check if they're actual type references.
			/*typeRefFinder.queueCount = typeRefFinder.q.Count;
			typeRefFinder.ResolveAllIdentifiers();
			*/
			return typeRefFinder.result;
		}
		#endregion

		/// <summary>
		/// Used for caching available types.
		/// </summary>
		protected override void OnScopedBlockChanged (IBlockNode bn)
		{
			HashSet<int> dd = null;
			foreach (var n in ItemEnumeration.EnumScopedBlockChildren(ctxt, MemberFilter.Types))
			{
				if (n.NameHash != 0) {
					if (dd == null && !TypeCache.TryGetValue (bn, out dd))
						TypeCache [bn] = dd = new HashSet<int> ();
					dd.Add (n.NameHash);
				}
			}
		}

		public override void Visit (DClassLike n)
		{
			if (DoPrimaryIdCheck (n.NameHash))
				AddResult (n);

			base.VisitDNode (n);
		}

		public override void Visit (TemplateInstanceExpression x)
		{
			if (DoPrimaryIdCheck(x.TemplateIdHash))
				AddResult(x);

			base.Visit (x);
		}

		public override void Visit (IdentifierDeclaration td)
		{
			if (DoPrimaryIdCheck(td.IdHash))
				AddResult(td);

			base.Visit (td);
		}

		public override void Visit (IdentifierExpression x)
		{
			/*
			 * if (DoPrimaryIdCheck(x.ValueStringHash))
					q.Add(o);
			 */
			base.Visit (x);
		}

		public override void Visit (PostfixExpression_Access x)
		{
			// q.AddRange(DoPrimaryIdCheck(x));
			base.Visit (x);
		}
		
		void AddResult(INode n)
		{
			List<ISyntaxRegion> l;
			if(!result.Matches.TryGetValue(n.NameLocation.Line, out l))
				result.Matches[n.NameLocation.Line] = l = new List<ISyntaxRegion>();

			l.Add(n);
		}

		void AddResult(ISyntaxRegion sr)
		{
			List<ISyntaxRegion> l;
			if(!result.Matches.TryGetValue(sr.Location.Line, out l))
				result.Matches[sr.Location.Line] = l = new List<ISyntaxRegion>();

			l.Add(sr);
		}

		/// <summary>
		/// Returns true if a type called 'id' exists in the current scope
		/// </summary>
		bool DoPrimaryIdCheck(int id)
		{
			if (id != 0) {
				HashSet<int> tc;
				var bn = ctxt.ScopedBlock;

				while (bn != null) {
					if (TypeCache.TryGetValue (bn, out tc) && tc.Contains (id))
						return true;
					else
						bn = bn.Parent as IBlockNode;
				}
			}
			return false;
		}
		/*
		List<IExpression> DoPrimaryIdCheck(PostfixExpression_Access acc)
		{
			var r = new List<IExpression>();
			while(acc != null){
				if (DoPrimaryIdCheck(ExtractId(acc)))
					r.Add(acc);

				// Scan down the access expression for other, deeper expressions
				if (acc.PostfixForeExpression is PostfixExpression_Access)
					acc = (PostfixExpression_Access)acc.PostfixForeExpression;
				else
				{
					if (DoPrimaryIdCheck(ExtractId(acc.PostfixForeExpression)))
						r.Add(acc.PostfixForeExpression);
					break;
				}
			}
			return r;
		}

		public static int ExtractId(ISyntaxRegion o)
		{
			if (o is IdentifierDeclaration)
				return ((IdentifierDeclaration)o).IdHash;
			else if (o is IdentifierExpression && ((IdentifierExpression)o).IsIdentifier)
				return ((IdentifierExpression)o).ValueStringHash;
			else if (o is PostfixExpression_Access)
				return ExtractId(((PostfixExpression_Access)o).AccessExpression);
			else if (o is TemplateInstanceExpression)
				return ((TemplateInstanceExpression)o).TemplateIdHash;
			else if (o is NewExpression)
				return ExtractId(((NewExpression)o).Type);
			return 0;
		}*/
	}
}

