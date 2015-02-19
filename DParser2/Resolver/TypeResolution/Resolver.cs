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

			public override void VisitTemplateParameter(TemplateParameter tp)
			{
				var nl = tp.NameLocation;
				string name;
				if (tp.NameHash != 0 &&
					caret.Line == nl.Line &&
					caret.Column >= nl.Column &&
					(name = tp.Name) != null &&
					caret.Column <= nl.Column + name.Length)
					IdNearCaret = tp.Representation;
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
			var block = SearchBlockAt(editor.SyntaxTree, editor.CaretLocation);

			IStatement stmt = null;
			if (block is DMethod)
				stmt = (block as DMethod).GetSubBlockAt(editor.CaretLocation);
			
			var vis = new ScopedObjectVisitor(editor.CaretLocation);
			if (stmt != null)
				stmt.Accept(vis);
			else
				block.Accept(vis);

			return vis.IdNearCaret;
		}

		public static AbstractType ResolveType(IEditorData editor, ResolutionContext ctxt = null)
		{
			var o = GetScopedCodeObject(editor);
			if (ctxt == null)
				ctxt = ResolutionContext.Create(editor, false);

			AbstractType ret = null;

			CodeCompletion.DoTimeoutableCompletionTask(null, ctxt, () =>
			{
				ctxt.Push(editor);
				
				var optionBackup = ctxt.CurrentContext.ContextDependentOptions;
				ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

				if (o is IExpression)
					ret = ExpressionTypeEvaluation.EvaluateType((IExpression)o, ctxt, false);
				else if (o is ITypeDeclaration)
					ret = TypeDeclarationResolver.ResolveSingle((ITypeDeclaration)o, ctxt);
				else if (o is INode)
					ret = TypeDeclarationResolver.HandleNodeMatch(o as INode, ctxt);

				ctxt.CurrentContext.ContextDependentOptions = optionBackup;
			});

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
			var o = GetScopedCodeObject(editor);
			if (ctxt == null)
				ctxt = ResolutionContext.Create(editor, false);

			AbstractType ret = null;
			NodeResolutionAttempt resAttempt = NodeResolutionAttempt.Normal;
			CodeCompletion.DoTimeoutableCompletionTask(null, ctxt, () =>
			{
				ctxt.Push(editor);
				
				var optionBackup = ctxt.CurrentContext.ContextDependentOptions;
				ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly | ResolutionOptions.DontResolveAliases;

				if (o is IExpression)
					ret = ExpressionTypeEvaluation.EvaluateType((IExpression)o, ctxt, false);
				else if (o is ITypeDeclaration)
					ret = TypeDeclarationResolver.ResolveSingle((ITypeDeclaration)o, ctxt);
				else if (o is INode)
					ret = TypeDeclarationResolver.HandleNodeMatch(o as INode, ctxt, null, o);
				else
					ret = null;

				if (ret == null)
				{
					resAttempt = NodeResolutionAttempt.NoParameterOrTemplateDeduction;

					if (o is PostfixExpression_MethodCall)
						o = (o as PostfixExpression_MethodCall).PostfixForeExpression;

					ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.NoTemplateParameterDeduction | ResolutionOptions.DontResolveAliases;

					if (o is IdentifierExpression)
						ret = AmbiguousType.Get(ExpressionTypeEvaluation.GetOverloads(o as IdentifierExpression, ctxt, deduceParameters: false), o);
					else if (o is ITypeDeclaration)
						ret = TypeDeclarationResolver.ResolveSingle(o as ITypeDeclaration, ctxt);
					else if (o is IExpression)
						ret = ExpressionTypeEvaluation.EvaluateType(o as IExpression, ctxt, false);
				}

				if (ret == null)
				{
					resAttempt = NodeResolutionAttempt.RawSymbolLookup;
					var overloads = TypeDeclarationResolver.HandleNodeMatches(LookupIdRawly(editor.ParseCache, o as ISyntaxRegion), ctxt, null, o);
					ret = AmbiguousType.Get(overloads, o);
				}

				ctxt.CurrentContext.ContextDependentOptions = optionBackup;
			});

			resolutionAttempt = resAttempt;

			if (ret != null)
				ret.DeclarationOrExpressionBase = o;

			return ret;
		}

		static void _ExtractIdOutOfTypeDecl(List<int> idParts, ref ITypeDeclaration td)
		{
			while (td != null) {
				if (td is IdentifierDeclaration)
					idParts.Add(((IdentifierDeclaration)td).IdHash);
				else if (td is TemplateInstanceExpression)
					idParts.Add(((TemplateInstanceExpression)td).TemplateIdHash);
				td = td.InnerDeclaration;
			}
		}

		public static List<DNode> LookupIdRawly(Misc.ParseCacheView parseCache, ISyntaxRegion o, DModule currentModule = null)
		{
			var idParts = new List<int> ();

			var td = o as ITypeDeclaration;
			if (td != null) {
				_ExtractIdOutOfTypeDecl (idParts, ref td);
				o = td;
			}
			else while (o is IExpression) {
					var x = o as IExpression;
					var pfa = x as PostfixExpression_Access;
					if (pfa != null) {
						x = pfa.AccessExpression;
						o = pfa.PostfixForeExpression;
					} else
						o = null;

					if (x is IdentifierExpression && ((IdentifierExpression)x).IsIdentifier)
						idParts.Add (((IdentifierExpression)x).ValueStringHash);
					else if (x is TemplateInstanceExpression)
						idParts.Add (((TemplateInstanceExpression)x).TemplateIdHash);
					else if (x is NewExpression) {
						td = ((NewExpression)x).Type;
						_ExtractIdOutOfTypeDecl (idParts, ref td);
					}
			}

			if (idParts.Count == 0)
				return null;

			var id = idParts[idParts.Count-1];
			idParts.RemoveAt (idParts.Count - 1);

			// Try to get a package first
			var currentPackages = parseCache as IEnumerable<ModulePackage>;
			var nextPackages = new List<ModulePackage> ();
			while (currentPackages != null) {
				foreach (var pack in currentPackages) {
					var p = pack.GetPackage (id);
					if(p != null)
						nextPackages.Add (p);
				}

				if (nextPackages.Count != 0) {
					currentPackages = nextPackages;
					nextPackages = new List<ModulePackage> ();
					if (idParts.Count > 0) {
						id = idParts [idParts.Count-1];
						idParts.RemoveAt (idParts.Count - 1);
					} else {
						id = -1;
						break;
					}
				} 
				else
					break;
			}

			if (idParts.Count == 0 && currentModule == null)
				return null;

			var currentNodes = new List<DNode> ();
			foreach (var pack in currentPackages) {
				var mod = pack.GetModule (id);	
				if(mod != null)
					currentNodes.Add(mod);
			}
			if (currentNodes.Count == 0) {
				if (currentModule == null)
					return null;
				
				currentNodes.Add (currentModule);
			} else {
				id = idParts[idParts.Count-1];
				idParts.RemoveAt (idParts.Count - 1);
			}

			var nextNodes = new List<DNode> ();
			while (currentNodes.Count > 0) {
				foreach (var n in currentNodes) 
					if(n is IBlockNode) {
						var ch = (n as IBlockNode).Children.GetNodes(id);
						if (ch != null)
							foreach (var c in ch)
								if (c is DNode)
									nextNodes.Add (c as DNode);
					}

				if (nextNodes.Count != 0) {
					currentNodes = nextNodes;
					nextNodes = new List<DNode> ();
					if (idParts.Count > 0) {
						id = idParts[idParts.Count-1];
						idParts.RemoveAt (idParts.Count - 1);
					} else {
						id = -1;
						break;
					}
				} 
				else
					break;
			}

			return currentNodes;
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
				// Pop a context frame as we still need to resolve the template instance expression args in the place where the expression occurs, not the instantiated class' location
				var backup = ctxt.CurrentContext;
				ctxt.Pop ();

				if (ctxt.CurrentContext == null)
					ctxt.Push (backup);

				var givenTemplateArguments = TemplateInstanceHandler.PreResolveTemplateArgs(tix, ctxt);

				if (ctxt.CurrentContext != backup) {
					foreach (var kv in ctxt.CurrentContext.DeducedTemplateParameters) {
						backup.DeducedTemplateParameters [kv.Key] = kv.Value;
						deducedTypes [kv.Key] = kv.Value;
					}
					ctxt.Push (backup);
				}

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
				return isClass ? new ClassType(dc, instanceDeclaration, dc.NameHash != ObjectNameHash ? ctxt.ParseCache.ObjectClassResult : null, null, deducedTypes) :
					new InterfaceType(dc, instanceDeclaration, null, deducedTypes) as TemplateIntermediateType;
			}


			#region Base class & interface resolution
			TemplateIntermediateType baseClass = null;
			var interfaces = new List<InterfaceType>();

			var back = ctxt.ScopedBlock;
			using (ctxt.Push(dc.Parent))
			{
				var pop = back != ctxt.ScopedBlock;

				foreach (var kv in deducedTypes)
					ctxt.CurrentContext.DeducedTemplateParameters[kv.Key] = kv.Value;

				try
				{
					for (int i = 0; i < (ResolveFirstBaseIdOnly ? 1 : dc.BaseClasses.Count); i++)
					{
						var type = dc.BaseClasses[i];

						// If there's an explicit 'Object' inheritance, also return the pre-resolved object class
						if (type is IdentifierDeclaration &&
							(type as IdentifierDeclaration).IdHash == ObjectNameHash)
						{
							if (baseClass != null)
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

						var r = DResolver.StripMemberSymbols(TypeDeclarationResolver.ResolveSingle(type, ctxt));

						if (r is ClassType || r is TemplateType)
						{
							if (!isClass)
								ctxt.LogError(new ResolutionError(type, "An interface cannot inherit from non-interfaces"));
							else if (i == 0)
							{
								baseClass = r as TemplateIntermediateType;
							}
							else
								ctxt.LogError(new ResolutionError(dc, "The base " + (r is ClassType ? "class" : "template") + " name must preceed base interfaces"));
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
				finally
				{
					bcStack--;
					parsedClassInstanceDecls.Remove(instanceDeclaration);
				}

				if (!pop)
					foreach (var kv in deducedTypes) // May be backup old tps?
						ctxt.CurrentContext.DeducedTemplateParameters.Remove(kv.Key);
			}
			#endregion

			if (isClass)
				return new ClassType(dc, instanceDeclaration, baseClass, interfaces.Count == 0 ? null : interfaces.ToArray(), deducedTypes);

			return new InterfaceType(dc, instanceDeclaration, interfaces.Count == 0 ? null : interfaces.ToArray(), deducedTypes);
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

		public static SR SearchRegionAt<SR>(IList<SR> children, CodeLocation Where) where SR : ISyntaxRegion
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

		public static IBlockNode SearchBlockAt(IBlockNode Parent, CodeLocation Where)
		{
			if (Parent == null)
				return null;

			var pCount = Parent.Count;
			while (pCount != 0)
			{
				var midElement = SearchRegionAt<INode> (Parent.Children, Where);

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
				var subItem = SearchRegionAt<INode>(dm.Children, Where) as IBlockNode;
				if (subItem != null)
					return SearchBlockAt(subItem, Where); // For e.g. nested nested methods inside anonymous class declarations that occur furtherly inside a method.
			}

			return Parent;
		}

		public static IStatement SearchStatementDeeplyAt(IBlockNode block, CodeLocation Where)
		{
			var dm = block as DMethod;
			if (dm != null)
				return SearchStatementDeeplyAt(dm.GetSubBlockAt(Where), Where);
			
			var db = block as DBlockNode;
			if (db != null && db.StaticStatements.Count != 0)
				return SearchRegionAt<IStatement>(new List<IStatement>(db.StaticStatements), Where);
			
			return null;
		}

		public static IStatement SearchStatementDeeplyAt(IStatement stmt, CodeLocation Where)
		{
			while(stmt != null)
			{
				var ss = stmt as StatementContainingStatement;
				if(ss != null)
				{
					var subst = ss.SubStatements;
					if (subst != null)
					{
						stmt = SearchRegionAt<IStatement>(subst as IList<IStatement> ?? new List<IStatement>(subst), Where);
						if (stmt == null || stmt == ss)
							return ss;
						continue;
					}
				}

				break;
			}

			return stmt;
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
			var symbols = new List<INode>();
			var newRes = new List<T>();

			if (results != null) {
				foreach (var rb in results) {
					var n = GetResultMember (rb);
					if (n != null) {
						if (symbols.Contains(n))
							continue;
						symbols.Add(n);

						// Put priority on locals
						if (n is DVariable &&
						   (n as DVariable).IsLocal) {
							newRes.Clear ();
							newRes.Add (rb);
							break;
						}
						
						if (ctxt.CurrentContext.ScopedBlock == null)
							break;

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

					if(!newRes.Contains(rb))
						newRes.Add (rb);
				}
			}

			return newRes;
		}

		public static DNode GetResultMember(ISemantic res, bool keepAliases = false)
		{
			var t = AbstractType.Get(res);

			if(t == null)
				return null;

			if (keepAliases)
			{
				var aliasTag = t.Tag as TypeResolution.TypeDeclarationResolver.AliasTag;
				if (aliasTag != null && 
					(!(aliasTag.aliasDefinition is ImportSymbolAlias) || // Only if the import symbol alias definition was selected, go to its base
					(aliasTag.typeBase != null && aliasTag.aliasDefinition.NameLocation != aliasTag.typeBase.Location)))
					return aliasTag.aliasDefinition;
			}

			if(t is DSymbol)
				return ((DSymbol)res).Definition;

			return null;
		}

		/// <summary>
		/// Removes all kinds of members from the given results.
		/// </summary>
		public static AbstractType StripMemberSymbols(AbstractType r)
		{
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

			return r;
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
