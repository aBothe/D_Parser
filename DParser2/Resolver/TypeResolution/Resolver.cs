using System;
using System.Collections.Generic;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.Templates;
using System.Threading;


namespace D_Parser.Resolver.TypeResolution
{
	/// <summary>
	/// Generic class for resolve module relations and/or declarations
	/// </summary>
	public class DResolver
	{
		class ScopedObjectVisitor : DefaultDepthFirstVisitor
		{
			public ISyntaxRegion IdNearCaret;
			readonly CodeLocation caret;

			public ScopedObjectVisitor(CodeLocation caret)
			{
				this.caret = caret;
			}

			public override void Visit(PostfixExpression_MethodCall x)
			{
				base.Visit(x);
				if (IdNearCaret == x.PostfixForeExpression)
					IdNearCaret = x;
			}

			public override void Visit(PostfixExpression_Access x)
			{
				if (x.AccessExpression != null && 
					x.AccessExpression.Location <= caret && 
					x.AccessExpression.EndLocation >= caret)
					IdNearCaret = x;
				else
					base.Visit(x);
			}

			public override void Visit(IdentifierExpression x)
			{
				if (x.Location <= caret && x.EndLocation >= caret)
					IdNearCaret = x;
				else
					base.Visit(x);
			}

			public override void Visit(IdentifierDeclaration x)
			{
				if (x.Location <= caret && x.EndLocation >= caret)
					IdNearCaret = x;
				else
					base.Visit(x);
			}

			public override void VisitDNode(DNode n)
			{
				var nl = n.NameLocation;
				string name;	
				if (n.NameHash != 0 &&
					caret.Line == nl.Line &&
					caret.Column >= nl.Column &&
					(name = n.Name) != null &&
					caret.Column <= nl.Column + name.Length)
					IdNearCaret = n;
				else
					base.VisitDNode(n);
			}

			// Template parameters
		}

		/// <summary>Used for code completion/symbol resolution.</summary>
		/// <param name="editor">Can be null</param>
		public static ISyntaxRegion GetScopedCodeObject(IEditorData editor)
		{
			IStatement stmt;
			var block = SearchBlockAt(editor.SyntaxTree, editor.CaretLocation, out stmt);

			var vis = new ScopedObjectVisitor(editor.CaretLocation);
			if (stmt != null)
				stmt.Accept(vis);
			else
				block.Accept(vis);

			return vis.IdNearCaret;
		}

		public static AbstractType ResolveType(IEditorData editor, ResolutionContext ctxt = null)
		{
			if (ctxt == null)
				ctxt = ResolutionContext.Create(editor);

			var o = GetScopedCodeObject(editor);

			var optionBackup = ctxt.CurrentContext.ContextDependentOptions;
			ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			AbstractType ret;

			if (o is IExpression)
				ret = ExpressionTypeEvaluation.EvaluateType((IExpression)o, ctxt);
			else if (o is ITypeDeclaration)
				ret = TypeDeclarationResolver.ResolveSingle((ITypeDeclaration)o, ctxt);
			else if (o is INode)
				ret = TypeDeclarationResolver.HandleNodeMatch(o as INode, ctxt);
			else
				ret = null;

			ctxt.CurrentContext.ContextDependentOptions = optionBackup;

			return ret;
		}

		public enum NodeResolutionAttempt
		{
			Normal,
			NoParameterOrTemplateDeduction,
			RawSymbolLookup
		}

		public static AbstractType ResolveTypeLoosely(IEditorData editor, out NodeResolutionAttempt resolutionAttempt, ResolutionContext ctxt = null)
		{
			if (ctxt == null)
				ctxt = ResolutionContext.Create(editor);

			var o = GetScopedCodeObject(editor);

			var optionBackup = ctxt.CurrentContext.ContextDependentOptions;
			ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;
			resolutionAttempt = NodeResolutionAttempt.Normal;

			AbstractType ret;

			if (o is IExpression)
				ret = ExpressionTypeEvaluation.EvaluateType((IExpression)o, ctxt);
			else if(o is ITypeDeclaration)
				ret = TypeDeclarationResolver.ResolveSingle((ITypeDeclaration)o, ctxt);
			else if (o is INode)
				ret = TypeDeclarationResolver.HandleNodeMatch(o as INode, ctxt, null, o);
			else
				ret = null;

			if (ret == null) {
				resolutionAttempt = NodeResolutionAttempt.NoParameterOrTemplateDeduction;

				if (o is PostfixExpression_MethodCall)
					o = (o as PostfixExpression_MethodCall).PostfixForeExpression;

				ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.NoTemplateParameterDeduction;

				if (o is IdentifierExpression)
				{
					var overloads = ExpressionTypeEvaluation.GetOverloads(o as IdentifierExpression, ctxt, deduceParameters: false);
					if (overloads != null && overloads.Length != 0)
						ret = overloads.Length == 1 ? overloads[0] : new AmbiguousType(overloads, o);
				}
				else if (o is ITypeDeclaration)
					ret = TypeDeclarationResolver.ResolveSingle(o as ITypeDeclaration, ctxt);
				else if (o is IExpression)
					ret = ExpressionTypeEvaluation.EvaluateType(o as IExpression, ctxt);
			}

			if (ret == null) {
				resolutionAttempt = NodeResolutionAttempt.RawSymbolLookup;
				ret = TypeDeclarationResolver.HandleNodeMatches (LookupIdRawly (editor, o as ISyntaxRegion), ctxt, null, o);
			}

			if (ret != null)
				foreach (var r in ret)
					if (r != null)
						r.DeclarationOrExpressionBase = o;

			ctxt.CurrentContext.ContextDependentOptions = optionBackup;
			return ret;
		}

		public static List<DNode> LookupIdRawly(IEditorData ed, ISyntaxRegion o)
		{
			// Extract a concrete id from that syntax object. (If access expression/nested decl, use the inner-most one)
			int idHash=0;

			chkAgain:
			if (o is ITypeDeclaration)
			{
				var td = ((ITypeDeclaration)o).InnerMost;

				if (td is IdentifierDeclaration)
					idHash = ((IdentifierDeclaration)td).IdHash;
				else if (td is TemplateInstanceExpression)
					idHash = ((TemplateInstanceExpression)td).TemplateIdHash;
			}
			else if (o is IExpression)
			{
				var x = (IExpression)o;

				while (x is PostfixExpression)
					x = ((PostfixExpression)x).PostfixForeExpression;

				if (x is IdentifierExpression && ((IdentifierExpression)x).IsIdentifier)
					idHash = ((IdentifierExpression)x).ValueStringHash;
				else if (x is TemplateInstanceExpression)
					idHash = ((TemplateInstanceExpression)x).TemplateIdHash;
				else if (x is NewExpression)
				{
					o = ((NewExpression)x).Type;
					goto chkAgain;
				}
			}

			if (idHash == 0)
				return null;

			var l = new List<DNode> ();

			// Rawly scan through all modules' roots of the parse cache to find that id.
			foreach(var pc in ed.ParseCache)
				foreach (var mod in pc)
				{
					if (mod.NameHash == idHash)
						l.Add(mod);

					var ch = mod[idHash];
					if(ch!=null)
						foreach (var c in ch)
						{
							var dn = c as DNode;

							// TODO: At least check for proper protection attributes properly!
							if (dn != null && !dn.ContainsAttribute(DTokens.Package, DTokens.Private, DTokens.Protected)) // Can this
								l.Add(dn);
						}

					//TODO: Mixins
				}

			return l;
		}

		static readonly int ObjectNameHash = "Object".GetHashCode();

		[ThreadStatic]
		static int bcStack = 0;
		[ThreadStatic]
		static List<ISyntaxRegion> parsedClassInstanceDecls;
		/// <summary>
		/// Takes the class passed via the tr, and resolves its base class and/or implemented interfaces.
		/// Also usable for enums.
		/// 
		/// Never returns null. Instead, the original 'tr' object will be returned if no base class was resolved.
		/// Will clone 'tr', whereas the new object will contain the base class.
		/// </summary>
		public static TemplateIntermediateType ResolveClassOrInterface(DClassLike dc, ResolutionContext ctxt, ISyntaxRegion instanceDeclaration, bool ResolveFirstBaseIdOnly=false, IEnumerable<TemplateParameterSymbol> extraDeducedTemplateParams = null)
		{
			if (parsedClassInstanceDecls == null)
				parsedClassInstanceDecls = new List<ISyntaxRegion> ();

			switch (dc.ClassType)
			{
				case DTokens.Class:
				case DTokens.Interface:
					break;
				default:
					if (dc.BaseClasses.Count != 0)
						ctxt.LogError(dc, "Only classes and interfaces may inherit from other classes/interfaces");
					return null;
			}

			bool isClass = dc.ClassType == DTokens.Class;

			if (bcStack > 6 || (instanceDeclaration != null && parsedClassInstanceDecls.Contains(instanceDeclaration)))
			{
				return isClass ? new ClassType(dc, instanceDeclaration, null) as TemplateIntermediateType : new InterfaceType(dc, instanceDeclaration);
			}

			if (instanceDeclaration != null)
				parsedClassInstanceDecls.Add(instanceDeclaration);
			bcStack++;

			var deducedTypes = new DeducedTypeDictionary(dc);
			var tix = instanceDeclaration as TemplateInstanceExpression;
			if (tix != null && (ctxt.Options & ResolutionOptions.NoTemplateParameterDeduction) == 0)
			{
				bool hasUndeterminedArgs;
				var givenTemplateArguments = TemplateInstanceHandler.PreResolveTemplateArgs(tix, ctxt, out hasUndeterminedArgs);

				if (!TemplateInstanceHandler.DeduceParams(givenTemplateArguments, false, ctxt, null, dc, deducedTypes))
				{
					parsedClassInstanceDecls.Remove(instanceDeclaration);
					bcStack--;
					return null;
				}
			}

			if (extraDeducedTemplateParams != null)
				foreach (var tps in extraDeducedTemplateParams)
					deducedTypes[tps.Parameter] = tps;


			if(dc.BaseClasses == null || dc.BaseClasses.Count < 1)
			{
				parsedClassInstanceDecls.Remove (instanceDeclaration);
				bcStack--;

				// The Object class has no further base class;
				// Normal class instances have the object as base class;
				// Interfaces must not have any default base class/interface
				return isClass ? new ClassType(dc, instanceDeclaration, dc.NameHash != ObjectNameHash ? ctxt.ParseCache.ObjectClassResult : null, null, deducedTypes.Count != 0 ? deducedTypes.ToReadonly() : null) :
					new InterfaceType(dc, instanceDeclaration, null, deducedTypes.Count != 0 ? deducedTypes.ToReadonly() : null) as TemplateIntermediateType;
			}


			#region Base class & interface resolution
			AbstractType[] res;
			var pop = ctxt.ScopedBlock != dc.Parent;
			if (pop)
				ctxt.PushNewScope(dc.Parent as IBlockNode);

			foreach (var kv in deducedTypes)
				ctxt.CurrentContext.DeducedTemplateParameters[kv.Key] = kv.Value;

			TemplateIntermediateType baseClass=null;
			var interfaces = new List<InterfaceType>();
			try
			{
				for (int i = 0; i < (ResolveFirstBaseIdOnly ? 1 : dc.BaseClasses.Count); i++)
				{
					var type = dc.BaseClasses[i];

					// If there's an explicit 'Object' inheritance, also return the pre-resolved object class
					if (type is IdentifierDeclaration && 
						(type as IdentifierDeclaration).IdHash == ObjectNameHash)
					{
						if (baseClass!=null)
						{
							ctxt.LogError(new ResolutionError(dc, "Class must not have two base classes"));
							continue;
						}
						else if (i != 0)
						{
							ctxt.LogError(new ResolutionError(dc, "The base class name must preceed base interfaces"));
							continue;
						}

						baseClass = ctxt.ParseCache.ObjectClassResult;
						continue;
					}

					if (type == null || (type is IdentifierDeclaration && (type as IdentifierDeclaration).IdHash == dc.NameHash) || dc.NodeRoot == dc)
					{
						ctxt.LogError(new ResolutionError(dc, "A class cannot inherit from itself"));
						continue;
					}
				
					res= DResolver.StripAliasSymbols(TypeDeclarationResolver.Resolve(type, ctxt));

					ctxt.CheckForSingleResult(res, type);

					if(res!=null && res.Length != 0)
					{
						var r = res[0];
						if (r is ClassType || r is TemplateType)
						{
							if (!isClass)
								ctxt.LogError(new ResolutionError(type, "An interface cannot inherit from non-interfaces"));
							else if (i == 0)
							{
								baseClass = r as TemplateIntermediateType;
							}
							else
								ctxt.LogError(new ResolutionError(dc, "The base "+(r is ClassType ?  "class" : "template")+" name must preceed base interfaces"));
						}
						else if (r is InterfaceType)
						{
							interfaces.Add(r as InterfaceType);

							if (isClass && dc.NameHash != ObjectNameHash && baseClass == null)
								baseClass = ctxt.ParseCache.ObjectClassResult;
						}
						else
						{
							ctxt.LogError(new ResolutionError(type, "Resolved class is neither a class nor an interface"));
							continue;
						}
					}
				}
			}
			finally
			{
				bcStack--;
				parsedClassInstanceDecls.Remove(instanceDeclaration);
			}

			if (pop)
				ctxt.Pop();
			else
				foreach (var kv in deducedTypes) // May be backup old tps?
					ctxt.CurrentContext.DeducedTemplateParameters.Remove(kv.Key);

			#endregion

			if (isClass)
				return new ClassType(dc, instanceDeclaration, baseClass, interfaces.Count == 0 ? null : interfaces.ToArray(), deducedTypes.Count != 0 ? deducedTypes.ToReadonly() : null);

			return new InterfaceType(dc, instanceDeclaration, interfaces.Count == 0 ? null : interfaces.ToArray(), deducedTypes.Count != 0 ? deducedTypes.ToReadonly() : null);
		}

		public static IBlockNode SearchBlockAt(IBlockNode Parent, CodeLocation Where)
		{
			IStatement s;
			return SearchBlockAt(Parent, Where, out s);
		}

		/// <summary>
		/// Binary search implementation for ordered syntax region (derivative) lists. 
		/// </summary>
		public static SR SearchRegionAt<SR>(Func<int,SR> childGetter, int childCount, CodeLocation Where) where SR : ISyntaxRegion
		{
			int start = 0;
			SR midElement = default(SR);
			int midIndex = 0;
			int len = childCount;

			while (len > 0)
			{
				midIndex = (len % 2 + len) / 2;

				// Take an element from the middle
				if ((midElement = childGetter(start + midIndex - 1)) == null)
					break;

				// If 'Where' is beyond its start location
				if (Where >= midElement.Location)
				{
					start += midIndex;

					// If we've reached the (temporary) goal, break immediately
					if (Where <= midElement.EndLocation)
						break;
					// If it's the last tested element and if the caret is beyond the end location, 
					// return the Parent instead the last tested child
					else if (midIndex == len)
					{
						midElement = default(SR);
						break;
					}
				}
				else if (midIndex == len)
				{
					midElement = default(SR);
					break;
				}

				len -= midIndex;
			}

			return midElement;
		}

		public static SR SearchRegionAt<SR>(List<SR> children, CodeLocation Where) where SR : ISyntaxRegion
		{
			int start = 0;
			SR midElement = default(SR);
			int midIndex = 0;
			int len = children.Count;

			while (len > 0)
			{
				midIndex = (len % 2 + len) / 2;

				// Take an element from the middle
				if ((midElement = children[start + midIndex - 1]) == null)
					break;

				// If 'Where' is beyond its start location
				if (Where > midElement.Location)
				{
					start += midIndex;

					// If we've reached the (temporary) goal, break immediately
					if (Where < midElement.EndLocation)
						break;
					// If it's the last tested element and if the caret is beyond the end location, 
					// return the Parent instead the last tested child
					else if (midIndex == len)
					{
						midElement = default(SR);
						break;
					}
				}
				else if (midIndex == len)
				{
					midElement = default(SR);
					break;
				}

				len -= midIndex;
			}

			return midElement;
		}

		public static IBlockNode SearchBlockAt(IBlockNode Parent, CodeLocation Where, out IStatement ScopedStatement)
		{
			ScopedStatement = null;

			if (Parent == null)
				return null;

			var pCount = Parent.Count;
			while (pCount != 0)
			{
				var midElement = SearchRegionAt<INode> (Parent.Children.ItemAt, pCount, Where);

				if (midElement is IBlockNode) {
					Parent = (IBlockNode)midElement;
					pCount = Parent.Count;
				}
				else
					break;
			}

			var dm = Parent as DMethod;
			if (dm != null)
			{
				// Do an extra re-scan for anonymous methods etc.
				var subItem = SearchRegionAt<INode> (dm.AdditionalChildren, Where);
				if (subItem != null) {
					if (!(subItem is DMethod))
						subItem = subItem.Parent;
					if (subItem is DMethod) {
						dm = subItem as DMethod;
						Parent = dm;
					}
				}

				var body = dm.GetSubBlockAt(Where);

				// First search the deepest statement under the caret
				if (body != null){
					ScopedStatement = body.SearchStatementDeeply(Where);

					if (ScopedStatement is IDeclarationContainingStatement)
					{
						var dcs = (ScopedStatement as IDeclarationContainingStatement).Declarations;

						if (dcs != null && dcs.Length != 0)
							foreach (var decl in dcs)
								if (decl is IBlockNode &&
								    Where > decl.Location &&
								    Where < decl.EndLocation)
									return SearchBlockAt (decl as IBlockNode, Where, out ScopedStatement);
					}
				}
			} else if(Parent is DBlockNode)
				ScopedStatement = GetStatementAt(Parent as DBlockNode, Where);

			return Parent;
		}

		public static IStatement GetStatementAt(DBlockNode db, CodeLocation Where)
		{
			if (db.StaticStatements.Count != 0)
				return SearchRegionAt<IStatement> (i => db.StaticStatements [i], db.StaticStatements.Count, Where);
			return null;
		}

		public static IBlockNode SearchClassLikeAt(IBlockNode Parent, CodeLocation Where)
		{
			if (Parent != null && Parent.Count > 0)
				foreach (var n in Parent)
				{
					var dc = n as DClassLike;
					if (dc==null)
						continue;

					if (Where > dc.BlockStartLocation && Where < dc.EndLocation)
						return SearchClassLikeAt(dc, Where);
				}

			return Parent;
		}

		public static List<T> FilterOutByResultPriority<T>(
			ResolutionContext ctxt,
			IEnumerable<T> results) where T : AbstractType
		{
			var newRes = new List<T>();

			if (results != null) {
				foreach (var rb in results) {
					var n = GetResultMember (rb);
					if (n != null) {
						// Put priority on locals
						if (n is DVariable &&
						   (n as DVariable).IsLocal) {
							newRes.Clear ();
							newRes.Add (rb);
							break;
						}

						// If member/type etc. is part of the actual module, omit external symbols
						if (n.NodeRoot != ctxt.CurrentContext.ScopedBlock.NodeRoot) {
							bool omit = false;
							foreach (var r in newRes) {
								var k = GetResultMember (r);
								if (k != null && k.NodeRoot == ctxt.CurrentContext.ScopedBlock.NodeRoot) {
									omit = true;
									break;
								}
							}

							if (omit)
								continue;
						} else
							foreach (var r in newRes.ToArray()) {
								var k = GetResultMember (r);
								if (k != null && k.NodeRoot != ctxt.CurrentContext.ScopedBlock.NodeRoot)
									newRes.Remove (r);
							}
					}
					newRes.Add (rb);
				}
			}

			return newRes;
		}

		public static DNode GetResultMember(ISemantic res)
		{
			if(res is DSymbol)
				return ((DSymbol)res).Definition;

			return null;
		}

		/// <summary>
		/// If an aliased type result has been passed to this method, it'll return the resolved type.
		/// If aliases were done multiple times, it also tries to skip through these.
		/// 
		/// alias char[] A;
		/// alias A B;
		/// 
		/// var resolvedType=TryRemoveAliasesFromResult(% the member result from B %);
		/// --> resolvedType will be StaticTypeResult from char[]
		/// 
		/// </summary>
		public static AbstractType StripAliasSymbol(AbstractType r)
		{
			while(r is AliasedType)
				r = (r as DerivedDataType).Base;

			return r;
		}

		public static AbstractType[] StripAliasSymbols(IEnumerable<AbstractType> symbols)
		{
			var l = new List<AbstractType>();

			if(symbols != null)
				foreach (var r in symbols)
					l.Add(StripAliasSymbol(r));

			return l.ToArray();
		}

		/// <summary>
		/// Removes all kinds of members from the given results.
		/// </summary>
		public static AbstractType StripMemberSymbols(AbstractType r)
		{
			r = StripAliasSymbol(r);

			var ds = r as DerivedDataType;
			if (ds != null && ds.Base != null) {
				if (ds is ArrayAccessSymbol || ds is MemberSymbol || ds is DelegateCallSymbol) {
					r = ds.Base;
					ds = r as DSymbol;
				}

				if (r is TemplateParameterSymbol) {
					if (ds.Base == null)
						return r;
					r = ds.Base;
					ds = r as DSymbol;
				}

				// There's one special case to handle (TODO: are there further cases?):
				// auto o = new Class(); -- o will be MemberSymbol and its base type will be a MemberSymbol either (i.e. the constructor reference)
				if(ds is MemberSymbol && (ds as DSymbol).Definition is DMethod && (ds as DSymbol).NameHash == DMethod.ConstructorIdentifierHash)
					r = ds.Base;
			}

			return StripAliasSymbol(r);
		}

		public static ISemantic StripValueTypeWrappers(ISemantic s)
		{
			while(true)
				if (s is TypeValue)
					s = (s as TypeValue).RepresentedType;
				else
					return s;
		}

		public static AbstractType[] StripMemberSymbols(IEnumerable<AbstractType> symbols)
		{
			var l = new List<AbstractType>();

			if(symbols != null)
				foreach (var r in symbols)
				{
					l.Add(StripMemberSymbols(r));
				}

			return l.ToArray();
		}
	}
}
