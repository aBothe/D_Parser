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
using D_Parser.Completion;
using System.Threading;

namespace D_Parser.Refactoring
{
	using TypeReferenceDictionary = Dictionary<int, LazyTypeReferenceKind>;

	public enum TypeReferenceKind : byte
	{
		Unknown,

		Interface,
		Enum,
		EnumValue,
		Template,
		Class,
		Struct,
		Union,
		TemplateTypeParameter,

		Constant,
		LocalVariable,
		ParameterVariable,
		TLSVariable,
		SharedVariable,
		GSharedVariable,
		MemberVariable,
		Variable,

		Alias,
		Module,
		Package = Module, // TODO
		Function,
		Method,
		BasicType,
	}

	struct LazyTypeReferenceKind
	{
		private TypeReferenceKind kind;
		private TypeReferenceFinder refFinder;
		private DVariable node;

		public LazyTypeReferenceKind(TypeReferenceKind k, TypeReferenceFinder finder, DVariable n)
		{
			kind = k;
			refFinder = finder;
			node = n;
		}

		public TypeReferenceKind get()
		{
			if (refFinder != null && node != null)
			{
				TypeReferenceKind k = refFinder.ResolveNodeType(node);
				refFinder = null;
				node = null;
				if (k != TypeReferenceKind.Unknown)
					kind = k;
			}
			return kind;
		}

		public static implicit operator LazyTypeReferenceKind(TypeReferenceKind k)
		{
			return new LazyTypeReferenceKind(k, null, null);
		}
	}

	public class TypeReferenceFinder : AbstractResolutionVisitor
	{
		#region Properties
		Dictionary<DeclarationCondition,int> handledConditions = new Dictionary<DeclarationCondition,int>();
		readonly List<ISyntaxRegion> invalidConditionalCodeRegions;
		readonly Dictionary<IBlockNode, TypeReferenceDictionary> TypeCache = new Dictionary<IBlockNode, TypeReferenceDictionary>();
		List<DModule> importStack = new List<DModule>();
		Dictionary<int, Dictionary<ISyntaxRegion, TypeReferenceKind>> Matches = new Dictionary<int, Dictionary<ISyntaxRegion, TypeReferenceKind>>();
		IEditorData editorData;
		readonly NodeTypeDeterminer nodeTypeDet;
		readonly TypeTypeDeterminer typeTypeDet;
		bool resolveTypes;
		#endregion

		#region Constructor / IO
		protected TypeReferenceFinder (ResolutionContext ctxt, List<ISyntaxRegion> i, bool resolveTypes) : base(ctxt)
		{
			this.invalidConditionalCodeRegions = i;
			this.resolveTypes = resolveTypes;
			nodeTypeDet = new NodeTypeDeterminer(this);
			typeTypeDet = new TypeTypeDeterminer(this);
		}

		public static Dictionary<int, Dictionary<ISyntaxRegion, TypeReferenceKind>>
			Scan(IEditorData ed, CancellationToken cancelToken, bool resolveTypes, List<ISyntaxRegion> invalidConditionalCodeRegions = null)
		{
			if (ed == null || ed.SyntaxTree == null)
				return new Dictionary<int, Dictionary<ISyntaxRegion, TypeReferenceKind>>();

			var ctxt = ResolutionContext.Create(ed, false);

			// Since it's just about enumerating, not checking types, ignore any conditions
			if (!resolveTypes)
				ctxt.ContextIndependentOptions |= ResolutionOptions.IgnoreDeclarationConditions;

			var typeRefFinder = new TypeReferenceFinder(ctxt, invalidConditionalCodeRegions, resolveTypes);
			typeRefFinder.importStack.Add(ed.SyntaxTree);
			typeRefFinder.editorData = ed;
			CodeCompletion.DoTimeoutableCompletionTask(null, ctxt, () => ed.SyntaxTree.Accept(typeRefFinder), cancelToken);

			return typeRefFinder.Matches;
		}
		#endregion

		struct NodeTypeDeterminer : NodeVisitor<LazyTypeReferenceKind>
		{
			private TypeReferenceFinder refFinder;

			public NodeTypeDeterminer(TypeReferenceFinder rf)
			{
				refFinder = rf;
			}

			public LazyTypeReferenceKind Visit(DEnumValue n)
			{
				return TypeReferenceKind.EnumValue;
			}

			public LazyTypeReferenceKind VisitDVariable(DVariable n)
			{
				if (n.IsAlias && !n.IsAliasThis)
					return new LazyTypeReferenceKind(TypeReferenceKind.Alias, refFinder, n);
				if (n.ContainsAnyAttribute(DTokens.Enum))
					return TypeReferenceKind.Constant;
				if (n.IsParameter)
					return TypeReferenceKind.ParameterVariable;
				if (n.ContainsAnyAttribute(DTokens.__gshared))
					return TypeReferenceKind.GSharedVariable;
				if (n.ContainsAnyAttribute(DTokens.Shared))
					return TypeReferenceKind.SharedVariable;
				if (n.IsStatic)
					return TypeReferenceKind.TLSVariable;
				if (n.IsLocal)
					return TypeReferenceKind.LocalVariable;
				if (n.Parent is DClassLike)
					return TypeReferenceKind.MemberVariable;
				return TypeReferenceKind.Variable;
			}

			public LazyTypeReferenceKind Visit(DMethod n)
			{
				if (n.Parent is DClassLike)
					return TypeReferenceKind.Method;
				else
					return TypeReferenceKind.Function;
			}

			public LazyTypeReferenceKind Visit(DClassLike n)
			{
				switch (n.ClassType)
				{
					default:
					case DTokens.Class:     return TypeReferenceKind.Class;
					case DTokens.Struct:    return TypeReferenceKind.Struct;
					case DTokens.Union:     return TypeReferenceKind.Union;
					case DTokens.Interface: return TypeReferenceKind.Interface;
					case DTokens.Template:  return TypeReferenceKind.Template;
				}
			}

			public LazyTypeReferenceKind Visit(DEnum n)
			{
				return TypeReferenceKind.Enum;
			}

			public LazyTypeReferenceKind Visit(DModule n)
			{
				return TypeReferenceKind.Module;
			}

			public LazyTypeReferenceKind Visit(DBlockNode dBlockNode)
			{
				return TypeReferenceKind.Function; // TODO
			}

			public LazyTypeReferenceKind Visit(TemplateParameter.Node templateParameterNode)
			{
				return TypeReferenceKind.TemplateTypeParameter;
			}

			public LazyTypeReferenceKind Visit(NamedTemplateMixinNode n)
			{
				return TypeReferenceKind.Template;
			}

			public LazyTypeReferenceKind VisitAttribute(Modifier attr)
			{
				throw new NotImplementedException();
			}

			public LazyTypeReferenceKind VisitAttribute(DeprecatedAttribute a)
			{
				throw new NotImplementedException();
			}

			public LazyTypeReferenceKind VisitAttribute(PragmaAttribute attr)
			{
				throw new NotImplementedException();
			}

			public LazyTypeReferenceKind VisitAttribute(BuiltInAtAttribute a)
			{
				throw new NotImplementedException();
			}

			public LazyTypeReferenceKind VisitAttribute(UserDeclarationAttribute a)
			{
				throw new NotImplementedException();
			}

			public LazyTypeReferenceKind VisitAttribute(VersionCondition a)
			{
				throw new NotImplementedException();
			}

			public LazyTypeReferenceKind VisitAttribute(DebugCondition a)
			{
				throw new NotImplementedException();
			}

			public LazyTypeReferenceKind VisitAttribute(StaticIfCondition a)
			{
				throw new NotImplementedException();
			}

			public LazyTypeReferenceKind VisitAttribute(NegatedDeclarationCondition a)
			{
				throw new NotImplementedException();
			}

			public LazyTypeReferenceKind Visit(EponymousTemplate ep)
			{
				return TypeReferenceKind.Template;
			}

			public LazyTypeReferenceKind Visit(ModuleAliasNode moduleAliasNode)
			{
				return TypeReferenceKind.Alias;
			}

			public LazyTypeReferenceKind Visit(ImportSymbolNode importSymbolNode)
			{
				return TypeReferenceKind.Alias;
			}

			public LazyTypeReferenceKind Visit(ImportSymbolAlias importSymbolAlias)
			{
				return TypeReferenceKind.Alias;
			}
		}

		struct TypeTypeDeterminer : IResolvedTypeVisitor<LazyTypeReferenceKind>
		{
			private TypeReferenceFinder refFinder;

			public TypeTypeDeterminer(TypeReferenceFinder typeReferenceFinder)
			{
				refFinder = typeReferenceFinder;
			}

			public LazyTypeReferenceKind VisitPrimitiveType(PrimitiveType t) => TypeReferenceKind.BasicType;
			public LazyTypeReferenceKind VisitPointerType(PointerType t) => TypeReferenceKind.BasicType;
			public LazyTypeReferenceKind VisitArrayType(ArrayType t) => TypeReferenceKind.BasicType;
			public LazyTypeReferenceKind VisitAssocArrayType(AssocArrayType t) => TypeReferenceKind.BasicType;
			public LazyTypeReferenceKind VisitDelegateCallSymbol(DelegateCallSymbol t) => TypeReferenceKind.BasicType;
			public LazyTypeReferenceKind VisitDelegateType(DelegateType t) => TypeReferenceKind.BasicType;
			public LazyTypeReferenceKind VisitAliasedType(AliasedType t) => TypeReferenceKind.BasicType;
			public LazyTypeReferenceKind VisitEnumType(EnumType t) => TypeReferenceKind.Enum;
			public LazyTypeReferenceKind VisitStructType(StructType t) => TypeReferenceKind.Struct;
			public LazyTypeReferenceKind VisitUnionType(UnionType t) => TypeReferenceKind.Union;
			public LazyTypeReferenceKind VisitClassType(ClassType t) => TypeReferenceKind.Class;
			public LazyTypeReferenceKind VisitInterfaceType(InterfaceType t) => TypeReferenceKind.Interface;
			public LazyTypeReferenceKind VisitTemplateType(TemplateType t) => TypeReferenceKind.Template;
			public LazyTypeReferenceKind VisitMixinTemplateType(MixinTemplateType t) => TypeReferenceKind.Template;
			public LazyTypeReferenceKind VisitEponymousTemplateType(EponymousTemplateType t) => TypeReferenceKind.Template;
			public LazyTypeReferenceKind VisitStaticProperty(StaticProperty t) => TypeReferenceKind.BasicType;
			public LazyTypeReferenceKind VisitMemberSymbol(MemberSymbol t)
			{
				if (t.Definition != null)
					return t.Definition.Accept(refFinder.nodeTypeDet);
				return TypeReferenceKind.BasicType;
			}
			public LazyTypeReferenceKind VisitTemplateParameterSymbol(TemplateParameterSymbol t) => TypeReferenceKind.TemplateTypeParameter;
			public LazyTypeReferenceKind VisitArrayAccessSymbol(ArrayAccessSymbol t) => TypeReferenceKind.BasicType;
			public LazyTypeReferenceKind VisitModuleSymbol(ModuleSymbol t) => TypeReferenceKind.Module;
			public LazyTypeReferenceKind VisitPackageSymbol(PackageSymbol t) => TypeReferenceKind.Module;
			public LazyTypeReferenceKind VisitDTuple(DTuple t) => TypeReferenceKind.BasicType;

			public LazyTypeReferenceKind VisitUnknownType(UnknownType t) => TypeReferenceKind.BasicType;
			public LazyTypeReferenceKind VisitAmbigousType(AmbiguousType t) => TypeReferenceKind.BasicType;
		}

		bool inRootModule()
		{
			return importStack.Count <= 1;
		}

		/// <summary>
		/// Used for caching available types.
		/// </summary>
		protected override void OnScopedBlockChanged (IBlockNode bn)
		{
			TypeReferenceDictionary dd = null;
			if (ctxt.CancellationToken.IsCancellationRequested)
				return;
			var filter = MemberFilter.Types | MemberFilter.Enums | MemberFilter.TypeParameters | MemberFilter.Variables | MemberFilter.Methods;
			var children = ItemEnumeration.EnumScopedBlockChildren(ctxt, filter);
			foreach (var n in children)
			{
				if (n.NameHash != 0) {
					if (dd == null && !TypeCache.TryGetValue (bn, out dd))
						TypeCache [bn] = dd = new TypeReferenceDictionary ();

					dd[n.NameHash] = n.Accept(nodeTypeDet);
				}
			}
		}

		public override void VisitDNode(DNode n)
		{
			if (CheckNode(n))
			{
				if (inRootModule())
					if (DoPrimaryIdCheck(n.NameHash, out TypeReferenceKind type))
						AddResult(n, type);

				base.VisitDNode(n);
			}
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

				if (inRootModule())
					if (DoPrimaryIdCheck(block.NameHash, out TypeReferenceKind type))
						AddResult(block, type);

				base.VisitDNode(block);
				VisitChildren(block);
			}
		}

		private void AddModuleResult(ITypeDeclaration modname)
		{
			AddResult(modname, TypeReferenceKind.Module);
			for (var inner = modname.InnerDeclaration; inner != null; inner = inner.InnerDeclaration)
				AddResult(inner, TypeReferenceKind.Package);
		}
		public override void Visit(ModuleStatement mstmt)
		{
			if (inRootModule())
				if (mstmt.ModuleName != null)
					AddModuleResult(mstmt.ModuleName);

			base.Visit(mstmt);
		}

		public override void VisitImport(ImportStatement.Import imp)
		{
			if (inRootModule())
			{
				if (imp.ModuleAlias != null)
					AddResult(imp.ModuleAlias, TypeReferenceKind.Module);
				if (imp.ModuleIdentifier != null)
					AddModuleResult(imp.ModuleIdentifier);
			}
			base.VisitImport(imp);
		}

		public override void Visit(ImportStatement istmt)
		{
			// do not recurse into private imports
			if (importStack.Count > 1 && !istmt.IsPublic)
				return;
			base.Visit(istmt);
		}

		public override void VisitImport(ImportStatement.ImportBindings ibind)
		{
			var curmod = importStack.Count > 0 ? importStack[importStack.Count - 1] : editorData.SyntaxTree;
			if (ctxt.ParseCache != null)
			{
				var modules = ctxt.ParseCache.LookupModuleName(curmod, ibind.Module.ToString());
				foreach (var mod in modules)
				{
					if (!importStack.Contains(mod))
					{
						importStack.Add(mod);
						mod.Accept(this);
						importStack.Remove(mod);
					}

					// TODO: refine	for static and renamed import
					TypeReferenceDictionary curtc = null;
					if (!TypeCache.TryGetValue(curmod, out curtc))
						TypeCache[curmod] = curtc = new TypeReferenceDictionary();

					TypeReferenceDictionary tc = null;
					if (TypeCache.TryGetValue(mod, out tc))
					{
						if (ibind.SelectedSymbols != null)
						{
							foreach (var sym in ibind.SelectedSymbols)
							{
								int key = sym.Symbol.IdHash;
								if (tc.TryGetValue(key, out LazyTypeReferenceKind type))
								{
									if (sym.Alias != null)
										key = sym.Alias.IdHash;
									curtc[key] = type.get();
								}
							}
						}
						else
						{
							// transfer all toplevel symbols
							foreach (var node in tc)
								if (!curtc.ContainsKey(node.Key))
									curtc[node.Key] = node.Value;
						}
					}
				}
			}
			base.VisitImport(ibind);
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

		public override void Visit(DEnum n)
		{
			if (CheckNode(n))
				base.Visit(n);
		}

		public override void Visit (DClassLike n)
		{
			if (CheckNode(n))
				base.Visit (n);
		}

		public override void Visit (DMethod dm)
		{
			if (!inRootModule())
				return;

			var bn = ctxt.ScopedBlock;
			TypeReferenceDictionary dd = null;
			if (dd == null && !TypeCache.TryGetValue(bn, out dd))
				TypeCache[bn] = dd = new TypeReferenceDictionary();
			dd[dm.NameHash] = dm.Accept(nodeTypeDet);

			base.Visit (dm);
			TypeReferenceDictionary tc;
			if (!TypeCache.TryGetValue (dm, out tc))
				return;

			// Reset locals
			foreach (var n in dm.Parameters)
				tc [n.NameHash] = TypeReferenceKind.Unknown;
		}

		public override void VisitTemplateParameter (TemplateParameter tp)
		{
			if (inRootModule())
				AddResult(tp, TypeReferenceKind.TemplateTypeParameter);
		}

		public override void Visit (TemplateInstanceExpression x)
		{
			if (inRootModule())
				if (DoPrimaryIdCheck(x.TemplateIdHash, out TypeReferenceKind type))
					AddResult(x, type);

			base.Visit (x);
		}

		public override void Visit (IdentifierDeclaration td)
		{
			if (inRootModule())
				if (DoPrimaryIdCheck(td.IdHash, out TypeReferenceKind type))
					AddResult(td, type);

			base.Visit (td);
		}

		public override void Visit (IdentifierExpression x)
		{
			//TODO: If there is a type result, try to resolve x (or postfix-access expressions etc.) to find out whether it's overwritten by some local non-type
			if (inRootModule())
				if (DoPrimaryIdCheck(x.IdHash, out TypeReferenceKind type))
					AddResult(x, type);

			base.Visit (x);
		}

		public override void Visit (PostfixExpression_Access x)
		{
			VisitPostfixExpression(x);
			if (inRootModule())
			{
				var id = x.AccessExpression as IdentifierExpression;
				if (id != null)
				{
					var kind = TypeReferenceKind.MemberVariable;
					if (resolveTypes)
					{
						try
						{
							var type = LooseResolution.ResolveTypeLoosely(editorData, x, out _, false);
							if (type != null)
								kind = type.Accept(typeTypeDet).get();
						}
						catch (Exception)
						{
							// avoid cancelling everything if it fails for some, e.g. System.NotImplementedException
						}
					}
					AddResult(id, kind);
				}
			}
		}
		
		void AddResult(INode n, TypeReferenceKind type)
		{
			Dictionary<ISyntaxRegion, TypeReferenceKind> l;
			if(!Matches.TryGetValue(n.NameLocation.Line, out l))
				Matches[n.NameLocation.Line] = l = new Dictionary<ISyntaxRegion, TypeReferenceKind>();

			l[n] = type;
		}

		void AddResult(ISyntaxRegion sr, TypeReferenceKind type)
		{
			Dictionary<ISyntaxRegion, TypeReferenceKind> l;
			if(!Matches.TryGetValue(sr.Location.Line, out l))
				Matches[sr.Location.Line] = l = new Dictionary<ISyntaxRegion, TypeReferenceKind>();

			l[sr] = type;
		}

		/// <summary>
		/// Returns true if a type called 'id' exists in the current scope
		/// </summary>
		bool DoPrimaryIdCheck(int id, out TypeReferenceKind type)
		{
			if (id != 0) {
				TypeReferenceDictionary tc;
				var bn = ctxt.ScopedBlock;

				while (bn != null) {
					if (TypeCache.TryGetValue (bn, out tc) && tc.TryGetValue (id, out LazyTypeReferenceKind lkind))
					{
						type = lkind.get();
						return true;
					}
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

		public TypeReferenceKind ResolveNodeType(INode node)
		{
			TypeReferenceKind kind = TypeReferenceKind.Unknown;
			if (resolveTypes && node.Type != null)
			{
				var type = LooseResolution.ResolveTypeLoosely(editorData, node.Type, out _, false);
				if (type != null)
					kind = type.Accept(typeTypeDet).get();
			}
			return kind;
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
			if (c == null ||
				c is StaticIfCondition || 
				(c is NegatedDeclarationCondition && (c as NegatedDeclarationCondition).FirstCondition is NegatedDeclarationCondition))
				return 0;

			int retCode;
			if (handledConditions.TryGetValue(c, out retCode))
				return retCode;

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

			retCode = ((ret || (!(c is NegatedDeclarationCondition) && ctxt.CompilationEnvironment.IsMatching(c, null))) ? 1 : -1);
			handledConditions[c] = retCode;
			return retCode;
		}

		public override void Visit(DeclarationStatement declarationStatement)
		{
			var bn = ctxt.ScopedBlock;
			TypeReferenceDictionary dd = null;
			if (dd == null && !TypeCache.TryGetValue(bn, out dd))
				TypeCache[bn] = dd = new TypeReferenceDictionary();

			foreach (var declaration in declarationStatement.Declarations)
			{
				if (declaration is DVariable variable)
					if (declaration.NameHash != 0)
						dd[declaration.NameHash] = declaration.Accept(nodeTypeDet);
			}
			base.Visit(declarationStatement);
		}
	}
}

