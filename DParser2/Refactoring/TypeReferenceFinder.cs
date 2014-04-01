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
using D_Parser.Dom.Statements;
using System;

namespace D_Parser.Refactoring
{
	public class TypeReferenceFinder : AbstractResolutionVisitor
	{
		#region Properties
		HashSet<DeclarationCondition> handledConditions = new HashSet<DeclarationCondition>();
		readonly List<ISyntaxRegion> invalidConditionalCodeRegions;
		readonly Dictionary<IBlockNode, Dictionary<int,byte>> TypeCache = new Dictionary<IBlockNode, Dictionary<int,byte>>();
		//DModule ast;
		Dictionary<int, Dictionary<ISyntaxRegion,byte>> Matches = new Dictionary<int, Dictionary<ISyntaxRegion,byte>>();
		#endregion

		#region Constructor / IO
		protected TypeReferenceFinder (ResolutionContext ctxt, List<ISyntaxRegion> i) : base(ctxt)
		{
			this.invalidConditionalCodeRegions = i;
		}

		public static Dictionary<int, Dictionary<ISyntaxRegion,byte>> Scan(DModule ast, ResolutionContext ctxt, List<ISyntaxRegion> invalidConditionalCodeRegions = null)
		{
			if (ast == null)
				return new Dictionary<int, Dictionary<ISyntaxRegion,byte>>();

			var typeRefFinder = new TypeReferenceFinder(ctxt, invalidConditionalCodeRegions);
			
			// Enum all identifiers
			ast.Accept (typeRefFinder);

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
			if (ctxt.CancelOperation)
				return;
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

		public override void VisitDNode(DNode n)
		{
			if (CheckNode(n))
				base.VisitDNode(n);
		}

		public override void VisitBlock(DBlockNode block)
		{
			// First do meta block evaluation due to conditional compilation checks
			var en = block.StaticStatements.GetEnumerator ();
			var metaBlockEnumGotElements = en.MoveNext ();
			using (PushConditionEnumBlock (block)) {
				if (block.MetaBlocks.Count != 0)
					foreach (var mb in block.MetaBlocks) {
						if (metaBlockEnumGotElements)
							metaBlockEnumGotElements = ContinueEnumStaticStatements (en, mb.Location);
						mb.Accept (this);
					}

				if (metaBlockEnumGotElements)
					ContinueEnumStaticStatements (en, block.EndLocation);

				VisitChildren(block);
			}
		}

		public override void VisitChildren (StatementContainingStatement stmt)
		{
			using(PushConditionEnumBlock (stmt))
				base.VisitChildren (stmt);
		}

		class CustomConditionFlagSet : MutableConditionFlagSet
		{
			public INode Block;
		}

		class ConditionStackPopper : IDisposable
		{
			public TypeReferenceFinder f;
			public void Dispose ()
			{
				f.conditionStack.Pop ();
			}
		}

		Stack<CustomConditionFlagSet> conditionStack = new Stack<CustomConditionFlagSet>();

		IDisposable PushConditionEnumBlock(IBlockNode bn)
		{
			if (conditionStack.Count == 0 || conditionStack.Peek ().Block != bn) {
				conditionStack.Push (new CustomConditionFlagSet{ Block = bn });
				return new ConditionStackPopper{ f = this };
			}
			return null;
		}

		IDisposable PushConditionEnumBlock(IStatement s)
		{
			INode n;
			if (s == null || (n=s.ParentNode) == null)
				return null;

			if (conditionStack.Count == 0 || conditionStack.Peek ().Block != n) {
				conditionStack.Push (new CustomConditionFlagSet{ Block = n });
				return new ConditionStackPopper{ f = this };
			}
			return null;
		}

		bool ContinueEnumStaticStatements(IEnumerator<IStatement> en, CodeLocation until)
		{
			IStatement cur;
			while ((cur = en.Current).Location < until) {
				cur.Accept (this);

				if (!en.MoveNext ())
					return false;
			}

			return true;
		}

		public override void Visit (VersionSpecification s)
		{
			if (CheckCondition (s.Attributes) >= 0)
				conditionStack.Peek ().AddVersionCondition (s);
		}

		public override void Visit (DebugSpecification s)
		{
			if (CheckCondition (s.Attributes) >= 0)
				conditionStack.Peek ().AddDebugCondition (s);
		}

		public override void Visit (DClassLike n)
		{
			if (!CheckNode(n))
				return;

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

		public override void Visit(StatementCondition s)
		{
			if (invalidConditionalCodeRegions != null)
			{
				switch(CheckCondition(s.Condition))
				{
					case 1:
						if(s.ElseStatement != null)
							invalidConditionalCodeRegions.Add(s.ElseStatement);
						if (s.ScopedStatement != null)
							s.ScopedStatement.Accept(this);
						return;
					case -1:
						if(s.ScopedStatement != null)
							invalidConditionalCodeRegions.Add(s.ScopedStatement);
						if (s.ElseStatement != null)
							s.ElseStatement.Accept(this);
						return;
				}
			}
			
			base.Visit(s);
		}

		public override void VisitAttributeMetaDeclarationBlock(AttributeMetaDeclarationBlock a)
		{
			switch (CheckCondition(a.AttributeOrCondition))
			{
				case 1:
					if (a.OptionalElseBlock != null)
						invalidConditionalCodeRegions.Add(a.OptionalElseBlock);
					break;
				case -1:
					invalidConditionalCodeRegions.Add(a);
					break;
			}
			
			base.VisitAttributeMetaDeclarationBlock(a);
		}

		bool CheckNode(DNode n)
		{
			switch (CheckCondition(n.Attributes))
			{
				case -1:
					invalidConditionalCodeRegions.Add(n);
					return false;
			}

			return true;
		}

		int CheckCondition(IEnumerable<DAttribute> attributes)
		{
			// All Attributes must apply to have the block compiling!
			if (attributes == null || invalidConditionalCodeRegions == null)
				return 0;
			int r = 0;

			foreach (var attr in attributes)
				if (attr is DeclarationCondition && (r = CheckCondition(attr as DeclarationCondition)) < 0)
					break;

			return r;
		}

		/// <returns>-1 if c is not matching, 1 if matching, 0 if unknown (won't invalidate else-case)</returns>
		int CheckCondition(DeclarationCondition c)
		{
			if (c == null || handledConditions.Contains(c) || 
				c is StaticIfCondition || 
				(c is NegatedDeclarationCondition && (c as NegatedDeclarationCondition).FirstCondition is NegatedDeclarationCondition))
				return 0;

			handledConditions.Add(c);
			bool ret = false;

			var backupStack = new Stack<CustomConditionFlagSet> ();
			INode n = null;
			CustomConditionFlagSet cc;
			while (conditionStack.Count != 0 && (n == null || conditionStack.Peek ().Block == n.Parent)) {
				cc = conditionStack.Pop ();
				n = cc.Block;
				backupStack.Push (cc);

				if (!(ret = cc.IsMatching (c, null)))
					break;
			}

			while (backupStack.Count != 0)
				conditionStack.Push (backupStack.Pop ());

			return ret || ctxt.CompilationEnvironment.IsMatching(c, null) ? 1 : -1;
		}
	}
}

