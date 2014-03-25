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
using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Parser;

namespace D_Parser.Refactoring
{
	public class TypeReferenceFinder : AbstractResolutionVisitor
	{
		#region Properties
		readonly Dictionary<IBlockNode, Dictionary<int,byte>> TypeCache = new Dictionary<IBlockNode, Dictionary<int,byte>>();
		//DModule ast;
		Dictionary<int, Dictionary<ISyntaxRegion,byte>> Matches = new Dictionary<int, Dictionary<ISyntaxRegion,byte>>();
		#endregion

		#region Constructor / IO
		protected TypeReferenceFinder (ResolutionContext ctxt) : base(ctxt)
		{
		}

		public static Dictionary<int, Dictionary<ISyntaxRegion,byte>> Scan(DModule ast, ResolutionContext ctxt)
		{
			if (ast == null)
				return new Dictionary<int, Dictionary<ISyntaxRegion,byte>>();

			var typeRefFinder = new TypeReferenceFinder(ctxt);

			ContextFrame backupFrame = null;

			if(ctxt.ScopedBlock == ast)
				backupFrame = ctxt.Pop ();
			/*
			if (ctxt.CurrentContext == null)
			{
				ctxt.Push(backupFrame);
				backupFrame = null;
			}*/

			//typeRefFinder.ast = ast;
			// Enum all identifiers
			ast.Accept (typeRefFinder);

			if (backupFrame != null)
				ctxt.Push (backupFrame);

			// Crawl through all remaining expressions by evaluating their types and check if they're actual type references.
			/*typeRefFinder.queueCount = typeRefFinder.q.Count;
			typeRefFinder.ResolveAllIdentifiers();
			*/
			return typeRefFinder.Matches;
		}
		#endregion

		/// <summary>
		/// Used for caching available types.
		/// </summary>
		protected override void OnScopedBlockChanged (IBlockNode bn)
		{
			Dictionary<int,byte> dd = null;
			foreach (var n in ItemEnumeration.EnumScopedBlockChildren(ctxt, MemberFilter.Types | MemberFilter.Enums))
			{
				if (n.NameHash != 0) {
					if (dd == null && !TypeCache.TryGetValue (bn, out dd))
						TypeCache [bn] = dd = new Dictionary<int,byte> ();

					byte type = 0;

					if (n is DClassLike)
						type = (n as DClassLike).ClassType;
					else if (n is DEnum)
						type = DTokens.Enum;
					else if (n is TemplateParameter.Node)
						type = DTokens.Not; // Only needed for highlighting and thus just a convention question
					else if (n is DVariable)
						type = DTokens.Alias;

					dd[n.NameHash] = type;
				}
			}
		}

		public override void Visit (DClassLike n)
		{
			byte type;
			if (DoPrimaryIdCheck (n.NameHash, out type))
				AddResult (n, type);

			base.Visit (n);
		}

		public override void VisitTemplateParameter (TemplateParameter tp)
		{
			AddResult (tp, DTokens.Not);
		}

		public override void Visit (TemplateInstanceExpression x)
		{
			byte type;
			if (DoPrimaryIdCheck(x.TemplateIdHash, out type))
				AddResult(x, type);

			base.Visit (x);
		}

		public override void Visit (IdentifierDeclaration td)
		{
			byte type;
			if (DoPrimaryIdCheck(td.IdHash, out type))
				AddResult(td, type);

			base.Visit (td);
		}

		public override void Visit (IdentifierExpression x)
		{
			//TODO: If there is a type result, try to resolve x (or postfix-access expressions etc.) to find out whether it's overwritten by some local non-type
			byte type;
			if (DoPrimaryIdCheck(x.ValueStringHash, out type))
				AddResult(x, type);

			base.Visit (x);
		}

		public override void Visit (PostfixExpression_Access x)
		{
			// q.AddRange(DoPrimaryIdCheck(x));
			base.Visit (x);
		}
		
		void AddResult(INode n, byte type)
		{
			Dictionary<ISyntaxRegion,byte> l;
			if(!Matches.TryGetValue(n.NameLocation.Line, out l))
				Matches[n.NameLocation.Line] = l = new Dictionary<ISyntaxRegion,byte>();

			l[n] = type;
		}

		void AddResult(ISyntaxRegion sr, byte type)
		{
			Dictionary<ISyntaxRegion,byte> l;
			if(!Matches.TryGetValue(sr.Location.Line, out l))
				Matches[sr.Location.Line] = l = new Dictionary<ISyntaxRegion,byte>();

			l[sr] = type;
		}

		/// <summary>
		/// Returns true if a type called 'id' exists in the current scope
		/// </summary>
		bool DoPrimaryIdCheck(int id, out byte type)
		{
			if (id != 0) {
				Dictionary<int,byte> tc;
				var bn = ctxt.ScopedBlock;

				while (bn != null) {
					if (TypeCache.TryGetValue (bn, out tc) && tc.TryGetValue (id, out type))
						return true;
					else
						bn = bn.Parent as IBlockNode;
				}
			}
			type = 0;
			return false;
		}
	}
}

