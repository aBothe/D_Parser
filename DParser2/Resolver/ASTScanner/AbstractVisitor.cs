using System;
using System.Collections.Generic;
using System.Linq;

using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Parser;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using System.Threading.Tasks;
using System.Threading;

namespace D_Parser.Resolver.ASTScanner
{
	public abstract class AbstractVisitor
	{
		#region Properties
		public static readonly DVariable __ctfe;
		Dictionary<string, List<string>> scannedModules = new Dictionary<string, List<string>>();

		static readonly ImportStatement.Import _objectImport = new ImportStatement.Import
		{
			ModuleIdentifier = new IdentifierDeclaration("object")
		};

		protected readonly ResolutionContext ctxt;

		Stack<ConditionsFrame> ConditionsStack;// = new Stack<ConditionsFrame>();
		protected virtual bool StopEnumerationOnNextScope {get{ return false; }}
		#endregion

		#region Constructor
		protected AbstractVisitor(ResolutionContext context)
		{
			ctxt = context;
		}

		static AbstractVisitor()
		{
			__ctfe = new DVariable
			{
				Name = "__ctfe",
				Type = new DTokenDeclaration(DTokens.Bool),
				Initializer = new TokenExpression(DTokens.True),
				Description = @"The __ctfe boolean pseudo-vari­able, 
which eval­u­ates to true at com­pile time, but false at run time, 
can be used to pro­vide an al­ter­na­tive ex­e­cu­tion path 
to avoid op­er­a­tions which are for­bid­den at com­pile time.",
				Attributes = new List<DAttribute>{new Modifier(DTokens.Static),new Modifier(DTokens.Const)}
			};
		}
		#endregion

		/// <summary>
		/// Used in NameScans to filter out unwanted items. Otherwise simply returns the children of the block node passed as argument.
		/// </summary>
		public virtual IEnumerable<INode> PrefilterSubnodes(IBlockNode bn)
		{
			return bn.Children;
		}
		
		public virtual IEnumerable<DModule> PrefilterSubnodes(ModulePackage pack, out IEnumerable<ModulePackage> subPackages)
		{
			subPackages = pack.GetPackages();
			return pack.GetModules();
		}

		protected virtual void HandleItem (INode n) { }

		/// <summary>
		/// Return true if search shall stop(!), false if search shall go on
		/// </summary>
		protected virtual void HandleItem(INode n, AbstractType resolvedCurrentScope) { HandleItem(n); }
		
		protected virtual void HandleItem(PackageSymbol pack) { }

		void HandleItems(IEnumerable<INode> nodes, ItemCheckParameters parms)
		{
			foreach (var n in nodes)
				HandleItemInternal (n, parms);
		}

		protected virtual bool PreCheckItem(INode n) { return true; }

		void HandleItemInternal(INode n, ItemCheckParameters parms)
		{
			if (n == null || !PreCheckItem (n))
				return;

			if (!CanHandleNode (n as DNode, parms, false))
				return;

			if(MatchesCompilationConditions(n as DNode))
				HandleItem (n, parms.resolvedCurScope);
		}

		public void IterateThroughScopeLayers(CodeLocation Caret, MemberFilter VisibleMembers = MemberFilter.All)
		{
			var parms = new ItemCheckParameters(VisibleMembers);

			var scopedBlock = DResolver.SearchBlockAt (ctxt.ScopedBlock, Caret);

			if (VisitStatementHierarchy(scopedBlock as DMethod, Caret, parms))
				return;

			if (ctxt.ScopedBlock != null) {
				ScanBlockUpward (ctxt.ScopedBlock, Caret, parms);
				if(StopEnumerationOnNextScope)
					return;
			}
			
			// Handle available modules/packages
			var nameStubs = new List<string>();
			if(ctxt.ParseCache != null)
				foreach(var root in ctxt.ParseCache.EnumRootPackagesSurroundingModule(ctxt.ScopedBlock))
				{
					IEnumerable<ModulePackage> packs;
					var mods = PrefilterSubnodes(root, out packs);

					if(packs != null)
						foreach(var pack in packs)
						{
							if(nameStubs.Contains(pack.Name))
								continue;
							
							HandleItem(new PackageSymbol(pack));
							nameStubs.Add(pack.Name);
						}
					
					if(mods != null)
						HandleItems(mods, parms);
				}

			// On the root level, handle __ctfe variable
			HandleItemInternal (__ctfe, parms);
		}
		
		void ScanBlockUpward(IBlockNode curScope, CodeLocation Caret, ItemCheckParameters parms)
		{
			// 2)
			do {
				ScanBlock (curScope, Caret, parms);

				curScope = curScope.Parent as IBlockNode;
			} while (!StopEnumerationOnNextScope && curScope != null && !ctxt.CancellationToken.IsCancellationRequested);
		}

		protected void ScanBlock(IBlockNode curScope, CodeLocation Caret, ItemCheckParameters parms)
		{
			SearchAttributesForIsExprDecls (curScope, Caret, parms);
			if(StopEnumerationOnNextScope)
				return;

			if (curScope is DClassLike)
			{
				DeepScanClass(new ClassType(curScope as DClassLike, null,null), parms);
			}
			else if (curScope is DMethod)
			{
				var dm = curScope as DMethod;

				// Add 'out' variable if typing in the out test block currently
				if (dm.OutResultVariable != null && dm.Out != null && dm.GetSubBlockAt(Caret) == dm.Out)
					HandleItemInternal(new DVariable
					{ // Create pseudo variable
						NameHash = dm.OutResultVariable.IdHash,
						NameLocation = dm.OutResultVariable.Location,
						Type = dm.Type, // TODO: What to do on auto functions?
						Parent = dm,
						Location = dm.OutResultVariable.Location,
						EndLocation = dm.OutResultVariable.EndLocation
					}, parms);

				HandleItems(dm.Parameters, parms);

				if (dm.TemplateParameters != null)
					HandleItems(dm.TemplateParameterNodes, parms);

				// The method's declaration children are handled above already via BlockStatement.GetItemHierarchy().
				// except AdditionalChildren:
				HandleItems(dm.Children, parms);

				// If the method is a nested method,
				// this method won't be 'linked' to the parent statement tree directly - 
				// so, we've to gather the parent method and add its locals to the return list
				VisitStatementHierarchy(dm.Parent as DMethod, Caret, parms);
			}
			else
				scanChildren(curScope as DBlockNode, new ItemCheckParameters(parms));
		}
		
		protected void DeepScanClass(UserDefinedType udt, ItemCheckParameters parms, bool resolveBaseClassIfRequired = true)
		{
			parms = new ItemCheckParameters (parms) {
				scopeIsInInheritanceHierarchy = udt != null && ctxt.ScopedBlockIsInNodeHierarchy(udt.Definition),
				takeStaticChildrenOnly = (udt == null || !udt.NonStaticAccess) && ctxt.ScopedBlock is DMethod && (ctxt.ScopedBlock as DMethod).IsStatic
			};
			
			// Check if the scoped node's parent is the current class
			/*if(takeStaticChildrenOnly)
			{
				takeStaticChildrenOnly = false;
				var sc = udt.Definition as IBlockNode;
				while(sc != null)
				{
					if(ctxt.ScopedBlock.Parent == sc)
					{
						takeStaticChildrenOnly = true;
						break;
					}
					sc = sc.Parent as IBlockNode;
				}
			}*/

			List<InterfaceType> interfaces = null;

			while(udt!= null)
			{
				parms.resolvedCurScope = udt;

				scanChildren (udt.Definition as DBlockNode, parms);
				if (StopEnumerationOnNextScope)
					return;

				if(udt is TemplateIntermediateType){
					var tit = udt as TemplateIntermediateType;
					var type = tit.Definition.ClassType;

					if (type == DTokens.Struct || type == DTokens.Class || type == DTokens.Template) {
						HandleAliasThisDeclarations (tit, parms);
						if(StopEnumerationOnNextScope)
							return;
					}

					if (tit.BaseInterfaces != null) {
						if (interfaces == null)
							interfaces = new List<InterfaceType> ();
						EnlistInterfaceHierarchy(interfaces, tit);
					}

					if(resolveBaseClassIfRequired && udt.Base == null && 
						(type == DTokens.Class ||type == DTokens.Interface))
						udt = DResolver.ResolveClassOrInterface(udt.Definition as DClassLike, ctxt, null);

					if(udt != null)
						udt = udt.Base as UserDefinedType;

					parms.isBaseClass = true;
				}
				else
					break;
			}

			if (interfaces != null) {
				parms.isBaseClass = true;

				foreach (var I in interfaces) {
					parms.resolvedCurScope = I;
					scanChildren (I.Definition, parms);
					if (StopEnumerationOnNextScope)
						return;
				}
			}
		}

		static void EnlistInterfaceHierarchy(List<InterfaceType> l, TemplateIntermediateType t)
		{
			if (l.Contains(t))
				return;

			if(t is InterfaceType)
				l.Add(t as InterfaceType);
			
			if(t.BaseInterfaces != null && t.BaseInterfaces.Length != 0)
				foreach (var nested in t.BaseInterfaces)
					EnlistInterfaceHierarchy(l, nested);
		}

		static bool IsAnonEnumOrClass(INode n)
		{
			return (n is DEnum || n is DClassLike) && (n as DNode).IsAnonymous;
		}

		protected struct ItemCheckParameters
		{
			public ItemCheckParameters(ItemCheckParameters essentialThingsToCopy)
			{
				VisibleMembers = essentialThingsToCopy.VisibleMembers;
				resolvedCurScope = essentialThingsToCopy.resolvedCurScope;
				dontHandleTemplateParamsInNodeScan = essentialThingsToCopy.dontHandleTemplateParamsInNodeScan;

				publicImportsOnly = false;
				isBaseClass = false;
				isMixinAst = false;
				takeStaticChildrenOnly = false;
				scopeIsInInheritanceHierarchy = false;
			}

			public ItemCheckParameters(MemberFilter vis) { 
				VisibleMembers = vis;
				publicImportsOnly = false;
				isBaseClass = false;
				isMixinAst = false;
				takeStaticChildrenOnly = false;
				scopeIsInInheritanceHierarchy = false;
				resolvedCurScope = null;
				dontHandleTemplateParamsInNodeScan = false;
			}

			public MemberFilter VisibleMembers;
			public bool publicImportsOnly;
			public bool isBaseClass;
			public bool isMixinAst;
			public bool takeStaticChildrenOnly;
			public bool scopeIsInInheritanceHierarchy; 
			public DSymbol resolvedCurScope;

			/// <summary>
			/// Temporary flag that is used for telling scanChildren() not to handle template parameters.
			/// Used to prevent the insertion of a template mixin's parameter set into the completion list etc.
			/// </summary>
			public bool dontHandleTemplateParamsInNodeScan;
		}

		void scanChildren(DBlockNode curScope, ItemCheckParameters parms)
		{
			var ch = PrefilterSubnodes(curScope);
			if (ch != null)
				foreach (var n in ch) {
					if (ctxt.CancellationToken.IsCancellationRequested)
						return;

					if (IsAnonEnumOrClass (n))
						continue;

					HandleItemInternal (n, parms);
				}

			// Add anonymous enums',structs' or unions' items (also recursively!)
			ch = curScope[0]; // 0 is the hash of unnamed nodes
			if (ch != null) {
				foreach (var n in ch) {
					if (!IsAnonEnumOrClass(n) ||
						!CanHandleNode (n as DNode, parms))
						continue;
					
					scanChildren (n as DBlockNode, parms);
				}
			}

			if (StopEnumerationOnNextScope)
				return;

			if (!parms.dontHandleTemplateParamsInNodeScan)
			{
				if (curScope.TemplateParameters != null && (parms.VisibleMembers & MemberFilter.TypeParameters) != 0)
				{
					var t = ctxt.ScopedBlock;
					while (t != null)
					{
						if (t == curScope)
						{
							HandleItems (curScope.TemplateParameterNodes as IEnumerable<INode>, parms);

							if (StopEnumerationOnNextScope)
								return;
							break;
						}
						t = t.Parent as IBlockNode;
					}
				}
			}
			else
				parms.dontHandleTemplateParamsInNodeScan = false;

			using(var stmtVis = new StatementHandler(curScope, this, parms, CodeLocation.Empty, true))
				curScope.Accept(stmtVis);
		}

		bool VisitStatementHierarchy(DMethod dm, CodeLocation caret, ItemCheckParameters parms)
		{
			BlockStatement s;
			return dm != null && (s = dm.GetSubBlockAt (caret)) != null && s.Accept (new StatementHandler (dm, this, parms, caret));
		}

		class StatementHandler : StatementVisitor<bool>, NodeVisitor<bool>, IDisposable
		{
			#region Properties
			public readonly ItemCheckParameters parms;
			public bool caretInsensitive { get{ return Caret.IsEmpty; } }
			public readonly CodeLocation Caret;
			ResolutionContext ctxt { get { return v.ctxt; } }

			readonly AbstractVisitor v;
			readonly bool pushResolvedCurScope;
			IDisposable frameToPop;

			readonly IBlockNode parentNodeOfVisitedStmt;
			#endregion

			#region Constuctor/IO
			public StatementHandler(IBlockNode parentNodeOfVisitedStmt, AbstractVisitor v, ItemCheckParameters parms, CodeLocation caret, bool pushResolvedCurScope = false)
			{
				this.parentNodeOfVisitedStmt = parentNodeOfVisitedStmt;
				this.v = v;
				this.Caret = caret;
				this.parms = parms;

				frameToPop = null;
				this.pushResolvedCurScope = pushResolvedCurScope;
			}

			public void TryPushCurScope()
			{
				if (frameToPop == null && pushResolvedCurScope) {
					frameToPop = ctxt.Push (parms.resolvedCurScope);
				}
			}

			public void Dispose ()
			{
				if (frameToPop != null)
					frameToPop.Dispose ();
			}

			#endregion

			#region Nodes
			public bool Visit(DEnumValue dEnumValue)
			{
				throw new NotImplementedException();
			}

			public bool Visit(DMethod dMethod)
			{
				return false;
			}

			public bool Visit(DClassLike dc)
			{
				return Visit(dc as DBlockNode);
			}

			public bool Visit(DEnum de)
			{
				return Visit(de as DBlockNode);
			}

			public bool Visit(DModule dbn)
			{
				// Every module imports 'object' implicitly
				bool ret = false;

				if (!parms.publicImportsOnly)
					ret = _objectImport.Accept (this);
				
				ret |= Visit(dbn as DBlockNode);

				return ret;
			}

			public bool Visit(TemplateParameter.Node templateParameterNode)
			{
				throw new NotImplementedException();
			}

			public bool Visit(NamedTemplateMixinNode n)
			{
				throw new NotImplementedException();
			}

			public bool Visit(DBlockNode dbn)
			{
				bool ret = false;
				if (dbn != null && dbn.StaticStatements != null)
				{
					foreach (var ss in dbn.StaticStatements)
						ret |= ss != null && ss.Accept(this);
				}

				return ret;
			}


			public bool Visit(DVariable dv)
			{
				if (dv.Initializer != null &&
					(caretInsensitive || (dv.Initializer.Location > Caret && dv.Initializer.EndLocation < Caret)))
				{
					TryPushCurScope ();
					v.HandleExpression (dv.Initializer, parms);

					if (v.StopEnumerationOnNextScope)
						return true;
				}

				return VisitDNode(dv);
			}

			public bool VisitDNode(DNode decl)
			{
				if (!(caretInsensitive || Caret >= decl.Location))
					return false;

				TryPushCurScope ();
				v.HandleItemInternal(decl, parms);
				return v.StopEnumerationOnNextScope;
			}

			public bool Visit(ModuleAliasNode n)
			{
				return Visit(n as DVariable);
			}

			public bool Visit(ImportSymbolNode n)
			{
				return Visit(n as DVariable);
			}

			public bool Visit(ImportSymbolAlias n)
			{
				return Visit(n as DVariable);
			}

			public bool Visit(EponymousTemplate n)
			{
				return Visit(n as DVariable);
			}
			#endregion

			#region Attributes
			public bool VisitAttribute(Modifier attr)
			{
				throw new NotImplementedException();
			}

			public bool VisitAttribute(DeprecatedAttribute a)
			{
				throw new NotImplementedException();
			}

			public bool VisitAttribute(PragmaAttribute attr)
			{
				throw new NotImplementedException();
			}

			public bool VisitAttribute(BuiltInAtAttribute a)
			{
				throw new NotImplementedException();
			}

			public bool VisitAttribute(UserDeclarationAttribute a)
			{
				throw new NotImplementedException();
			}

			public bool VisitAttribute(VersionCondition a)
			{
				throw new NotImplementedException();
			}

			public bool VisitAttribute(DebugCondition a)
			{
				throw new NotImplementedException();
			}

			public bool VisitAttribute(StaticIfCondition a)
			{
				throw new NotImplementedException();
			}

			public bool VisitAttribute(NegatedDeclarationCondition a)
			{
				throw new NotImplementedException();
			}
			#endregion

			#region Static Statements
			public bool VisitExpressionStmt(IExpressionContainingStatement Statement)
			{
				if (Statement.Location < Caret && Statement.EndLocation >= Caret && Statement.SubExpressions != null)
					foreach (var x in Statement.SubExpressions)
						if (x != null && x.Location < Caret && x.EndLocation >= Caret) {
							TryPushCurScope ();
							v.HandleExpression (x, parms);
							if (v.StopEnumerationOnNextScope)
								return true;
						}
				return false;
			}

			public bool VisitSubStatements(StatementContainingStatement stmtContainer, bool ignoreBounds = false)
			{
				if (!(ignoreBounds || caretInsensitive) && 
					(stmtContainer.Location > Caret || stmtContainer.EndLocation < Caret))
					return false;

				var ss = stmtContainer.SubStatements;
				if (ss != null)
				{
					foreach (var s in ss)
					{
						if (s == null)
							continue;

						if ((!caretInsensitive && s.Location >= Caret) || ctxt.CancellationToken.IsCancellationRequested)
							break;

						if (s.Accept(this))
							return true;
					}
				}

				return false;
			}

			public bool Visit(ImportStatement impStmt)
			{
				if ((parms.publicImportsOnly && impStmt != null && !impStmt.IsPublic))
					return false;

				TryPushCurScope ();

				if(!ctxt.CurrentContext.MatchesDeclarationEnvironment(impStmt.Attributes))
					return false;

				/*
				 * Mainly used for selective imports/import module aliases
				 */
				foreach (var decl in impStmt.PseudoAliases) {
					v.HandleItemInternal (decl, parms);
					if (v.StopEnumerationOnNextScope) //TODO: Handle visibility?
						return true;
				}

				var ret = false;

				if (!impStmt.IsStatic) {
					foreach (var imp in impStmt.Imports)
						ret |= imp.ModuleAlias == null && imp.Accept (this);
				}

				return ret;
			}

			public bool VisitImport(ImportStatement.Import imp)
			{
				if (imp == null || imp.ModuleIdentifier == null)
					return false;

				TryPushCurScope ();

				DModule mod;
				var thisModuleName = (ctxt.ScopedBlock != null && (mod = ctxt.ScopedBlock.NodeRoot as DModule) != null) ? mod.ModuleName : string.Empty;

				if (string.IsNullOrEmpty(thisModuleName))
					return false;

				var moduleName = imp.ModuleIdentifier.ToString();

				List<string> seenModules;
				lock (v.scannedModules) {
					if (!v.scannedModules.TryGetValue (thisModuleName, out seenModules))
						seenModules = v.scannedModules [thisModuleName] = new List<string> ();
					else if (seenModules.Contains (moduleName))
						return false;
					seenModules.Add (moduleName);
				}

				if (ctxt.ParseCache == null)
					return false;

				var scopedModule = parentNodeOfVisitedStmt.NodeRoot as DModule;

				foreach (var module in ctxt.ParseCache.LookupModuleName(scopedModule, moduleName)) //TODO: Only take the first module? Notify the user about ambigous module names?
				{
					if (module.FileName != null && 
						module.FileName == scopedModule.FileName)
						continue;

					v.scanChildren(module, new ItemCheckParameters(parms) { publicImportsOnly = true });

					if (v.StopEnumerationOnNextScope)
						return true;
				}
				return false;
			}
			#endregion

			#region Template Mixins
			/// <summary>
			/// http://dlang.org/template-mixin.html#TemplateMixin
			/// </summary>
			public bool Visit(TemplateMixin tmx)
			{
				if (CompletionOptions.Instance.DisableMixinAnalysis)
					return false;

				if (templateMixinsBeingAnalyzed == null)
					templateMixinsBeingAnalyzed = new List<TemplateMixin>();

				if (templateMixinsBeingAnalyzed.Contains(tmx))
					return false;
				templateMixinsBeingAnalyzed.Add(tmx);


				TryPushCurScope ();

				if (!ctxt.CurrentContext.MatchesDeclarationEnvironment (tmx.Attributes))
					return false;

				var tmxTemplate = GetTemplateMixinContent(ctxt, tmx, false);

				if (tmxTemplate == null)
					ctxt.LogError(tmx.Qualifier, "Mixin qualifier must resolve to a mixin template declaration.");
				else using (ctxt.Push(tmxTemplate))
				{
					v.DeepScanClass(tmxTemplate, new ItemCheckParameters(parms) { dontHandleTemplateParamsInNodeScan = true });
				}

				templateMixinsBeingAnalyzed.Remove(tmx);

				return v.StopEnumerationOnNextScope;
			}
			
			[ThreadStatic]
			static List<TemplateMixin> templateMixinsBeingAnalyzed;
			#endregion

			#region Mixin
			/// <summary>
			/// Evaluates the literal given as expression and tries to parse it as a string.
			/// Important: Assumes all its compilation conditions to be checked already!
			/// </summary>
			public bool VisitMixinStatement(MixinStatement mx)
			{
				if (CompletionOptions.Instance.DisableMixinAnalysis)
					return false;

				TryPushCurScope ();

				if (!ctxt.CurrentContext.MatchesDeclarationEnvironment (mx.Attributes))
					return false;

				VariableValue vv;
				// If in a class/module block => MixinDeclaration
				if (caretInsensitive)
				{
					var ast = MixinAnalysis.ParseMixinDeclaration(mx, ctxt, out vv);

					if (ast == null)
						return false;

					if(vv != null)
						ctxt.CurrentContext.IntroduceTemplateParameterTypes(vv.RepresentedType);

					// take ast.Endlocation because the cursor must be beyond the actual mixin expression 
					// - and therewith _after_ each declaration
					v.scanChildren(ast, new ItemCheckParameters(parms) { isMixinAst = true });

					if (vv != null)
						ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(vv.RepresentedType);
				}
				else // => MixinStatement
				{
					var bs = MixinAnalysis.ParseMixinStatement(mx, ctxt, out vv);

					// As above, disregard the caret position because 1) caret and parsed code do not match 
					// and 2) the caret must be located somewhere after the mixin statement's end
					if (bs != null)
					{
						if (vv != null)
							ctxt.CurrentContext.IntroduceTemplateParameterTypes(vv.RepresentedType);

						bs.Accept (new StatementHandler(parentNodeOfVisitedStmt, v, parms, CodeLocation.Empty));

						if (vv != null)
							ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(vv.RepresentedType);
					}
				}

				return v.StopEnumerationOnNextScope;
			}

			static MixinTemplateType GetTemplateMixinContent(ResolutionContext ctxt, TemplateMixin tmx, bool pushOnAnalysisStack = true)
			{
				if (pushOnAnalysisStack)
				{
					if (templateMixinsBeingAnalyzed == null)
						templateMixinsBeingAnalyzed = new List<TemplateMixin>();

					if (templateMixinsBeingAnalyzed.Contains(tmx))
						return null;
					templateMixinsBeingAnalyzed.Add(tmx);
				}

				AbstractType t;
				t = TypeDeclarationResolver.ResolveSingle(tmx.Qualifier, ctxt);
				
				if (pushOnAnalysisStack)
					templateMixinsBeingAnalyzed.Remove(tmx);

				return t as MixinTemplateType;
			}
			#endregion

			#region In-Method statements
			public bool Visit(ModuleStatement moduleStatement)
			{
				return false;
			}

			public bool VisitImport(ImportStatement.ImportBinding importBinding)
			{
				return false;
			}

			public bool VisitImport(ImportStatement.ImportBindings bindings)
			{
				return false;
			}

			public bool Visit(BlockStatement s)
			{
				return VisitSubStatements(s);
			}

			public bool Visit(LabeledStatement s)
			{
				return false;
			}

			public bool Visit(IfStatement s)
			{
				if (s.IfVariable != null && Visit(s.IfVariable))
					return true;

				return VisitExpressionStmt(s) || VisitSubStatements(s);
			}

			public bool Visit(WhileStatement s)
			{
				return VisitExpressionStmt(s) || VisitSubStatements(s);
			}

			public bool Visit(ForStatement s)
			{
				return VisitExpressionStmt(s) || VisitSubStatements(s);
			}

			public bool Visit(ForeachStatement s)
			{
				if (s.Location < Caret && 
					(s.ScopedStatement == null || s.ScopedStatement.EndLocation >= Caret) && 
					s.ForeachTypeList != null)
					foreach (var n in s.ForeachTypeList)
						if (n!= null && n.Accept(this))
							return true;

				return VisitExpressionStmt(s) || VisitSubStatements(s);
			}

			public bool Visit(SwitchStatement s)
			{
				return VisitExpressionStmt(s) || VisitSubStatements(s);
			}

			public bool Visit(SwitchStatement.CaseStatement s)
			{
				return VisitExpressionStmt(s) || VisitSubStatements(s);
			}

			public bool Visit(SwitchStatement.DefaultStatement s)
			{
				return VisitSubStatements(s);
			}

			public bool Visit(ContinueStatement s)
			{
				return VisitExpressionStmt(s);
			}

			public bool Visit(BreakStatement s)
			{
				return VisitExpressionStmt(s);
			}

			public bool Visit(ReturnStatement s)
			{
				return VisitExpressionStmt(s);
			}

			public bool Visit(GotoStatement s)
			{
				return VisitExpressionStmt(s);
			}

			/// <summary>
			/// http://dlang.org/statement.html#WithStatement
			/// </summary>
			public bool Visit(WithStatement ws)
			{
				if (ws.ScopedStatement == null ||
					(!caretInsensitive && Caret < ws.ScopedStatement.Location))
					return false;

				TryPushCurScope ();

				var back = ctxt.CurrentContext.Caret;
				ctxt.CurrentContext.Set(ctxt.ScopedBlock,ws.Parent.Location);

				AbstractType r;
				// Must be an expression that returns an object reference
				if (ws.WithExpression != null)
					r = ExpressionTypeEvaluation.EvaluateType(ws.WithExpression, ctxt);
				else if (ws.WithSymbol != null) // This symbol will be used as default
					r = TypeDeclarationResolver.ResolveSingle(ws.WithSymbol, ctxt);
				else
					r = null;

				ctxt.CurrentContext.Set(ctxt.ScopedBlock,back);

				if ((r = DResolver.StripMemberSymbols(r)) is UserDefinedType)
				{
					v.DeepScanClass (r as UserDefinedType, parms);
					if(v.StopEnumerationOnNextScope)
						return true;
				}

				return VisitSubStatements(ws);
			}

			public bool Visit(SynchronizedStatement s)
			{
				return VisitExpressionStmt(s) || VisitSubStatements(s);
			}

			public bool Visit(TryStatement s)
			{
				return VisitSubStatements(s);
			}

			public bool Visit(TryStatement.CatchStatement s)
			{
				return (s.CatchParameter != null && Visit(s.CatchParameter)) || VisitSubStatements(s);
			}

			public bool Visit(TryStatement.FinallyStatement s)
			{
				return VisitSubStatements(s);
			}

			public bool Visit(ThrowStatement s)
			{
				return VisitExpressionStmt(s);
			}

			public bool Visit(ScopeGuardStatement s)
			{
				return VisitSubStatements(s);
			}

			public bool Visit(AsmStatement s)
			{
				return VisitSubStatements(s);
			}

			public bool Visit(PragmaStatement s)
			{
				return VisitSubStatements(s);
			}

			public bool Visit(StatementCondition sc)
			{
				if ((ctxt.Options & ResolutionOptions.IgnoreDeclarationConditions) != 0)
				{
					if (sc.ScopedStatement != null && sc.ScopedStatement.Accept(this))
							return false;

					return sc.ElseStatement != null && sc.ElseStatement.Accept(this);
				}

				TryPushCurScope ();

				if (!ctxt.CurrentContext.MatchesDeclarationEnvironment(sc.Condition))
					return false;

				if (sc.Condition is StaticIfCondition && sc.Location < Caret && sc.EndLocation >= Caret) {
					v.HandleExpression ((sc.Condition as StaticIfCondition).Expression, parms);
					if(v.StopEnumerationOnNextScope)
						return true;
				}

				if (sc.ScopedStatement is BlockStatement &&
					VisitSubStatements(sc.ScopedStatement as StatementContainingStatement, true))
					return true;
				else if (sc.ScopedStatement != null && sc.ScopedStatement.Accept(this))
					return true;
				
				return sc.ElseStatement != null && sc.ElseStatement.Accept(this);
			}

			public bool Visit(VolatileStatement s)
			{
				return VisitSubStatements(s);
			}

			public bool Visit(ExpressionStatement s)
			{
				return VisitExpressionStmt(s);
			}

			public bool Visit(DeclarationStatement s)
			{
				if (s.Declarations != null)
					foreach (DNode decl in s.Declarations)
						if (decl is DVariable ? Visit(decl as DVariable) : VisitDNode(decl))
							return true;

				return false;
			}

			public bool Visit(DebugSpecification s)
			{
				return false;
			}

			public bool Visit(VersionSpecification s)
			{
				return false;
			}

			public bool Visit(StaticAssertStatement s)
			{
				return VisitExpressionStmt(s);
			}

			public bool Visit(AsmStatement.InstructionStatement instrStatement)
			{
				return false;
			}

			public bool Visit(AsmStatement.RawDataStatement dataStatement)
			{
				return false;
			}

			public bool Visit(AsmStatement.AlignStatement alignStatement)
			{
				return false;
			}
			#endregion
		}

		[ThreadStatic]
		static Dictionary<IBlockNode, DVariable> aliasThisDefsBeingParsed;

		void HandleAliasThisDeclarations(TemplateIntermediateType tit, ItemCheckParameters parms)
		{
			var ch = tit.Definition [DVariable.AliasThisIdentifierHash];
			if (ch == null || ctxt.CancellationToken.IsCancellationRequested)
				return;

			var aliasDef = ch.FirstOrDefault() as DVariable; // Only allow one alias this to be resolved ever!
			if (aliasDef == null || aliasDef.Type == null || !MatchesCompilationConditions(aliasDef))
				return;

			if (aliasThisDefsBeingParsed == null)
				aliasThisDefsBeingParsed = new Dictionary<IBlockNode, DVariable>();

			DVariable alreadyParsedAliasThis;
			if (aliasThisDefsBeingParsed.TryGetValue (tit.Definition, out alreadyParsedAliasThis) && alreadyParsedAliasThis == aliasDef)
				return;
			aliasThisDefsBeingParsed[tit.Definition] = aliasDef;

			AbstractType aliasedSymbolOverloads;
			using(ctxt.Push(tit))
				aliasedSymbolOverloads = DResolver.StripMemberSymbols(DResolver.StripMemberSymbols(TypeDeclarationResolver.ResolveSingle(aliasDef.Type, ctxt)));

			aliasThisDefsBeingParsed.Remove(tit.Definition);

			if (aliasedSymbolOverloads == null)
				return;

			foreach (var _ in AmbiguousType.TryDissolve(aliasedSymbolOverloads)) {
				var aliasedSymbol = _;

				if (aliasedSymbol is PointerType)
					aliasedSymbol = (aliasedSymbol as DerivedDataType).Base;
				
				aliasedSymbol = DResolver.StripMemberSymbols (aliasedSymbol);

				foreach (var statProp in StaticProperties.ListProperties (aliasedSymbol, ctxt)) {
					HandleItemInternal (statProp, parms);
					if (StopEnumerationOnNextScope)
						return;
				}

				/** TODO: Visit ufcs recommendations and other things that
				 * become added in e.g. MemberCompletionProvider
				 */

				var tit_ = aliasedSymbol as TemplateIntermediateType;
				DSymbol ds;
				if (tit_ != null) {
					using (ctxt.Push (tit_))
						DeepScanClass (tit_, parms, true);
				}
				// Applies to DEnums
				else if ((ds = aliasedSymbol as DSymbol) != null && ds.Definition is DBlockNode) {
						parms.resolvedCurScope = ds;
						scanChildren (ds.Definition as DBlockNode, parms);
					}
				}
		}

		#region Declaration conditions & Static statements
		INode lastCheckedNodeParent;
		Dictionary<DeclarationCondition, bool> alreadyCheckedConditions = new Dictionary<DeclarationCondition,bool>();

		bool MatchesCompilationConditions(DNode n)
		{
			if((ctxt.Options & ResolutionOptions.IgnoreDeclarationConditions) != 0)
				return true;

			if (lastCheckedNodeParent != n.Parent)
			{
				lastCheckedNodeParent = n.Parent;
				alreadyCheckedConditions.Clear();
			}

			if (n.Attributes != null)
				foreach (var c in n.Attributes)
				{
					var neg = c as NegatedDeclarationCondition;
					var cond = neg != null ? neg.FirstCondition : c as DeclarationCondition;
					if (cond == null)
						continue;

					bool res;

					if (cond is VersionCondition || cond is DebugCondition)
					{
						res = neg != null ? !ctxt.CurrentContext.MatchesDeclarationEnvironment(neg) : ctxt.CurrentContext.MatchesDeclarationEnvironment(cond);
					}
					else if (!alreadyCheckedConditions.TryGetValue(cond, out res))
					{
						alreadyCheckedConditions[cond] = res = ctxt.CurrentContext.MatchesDeclarationEnvironment(cond);
					}

					if (neg != null ? res : !res)
						return false;
				}

			return true;
		}

		// Following methods aren't used atm!
		void ContinueHandleStaticStatements(CodeLocation until)
		{
			var cf = ConditionsStack.Peek ();
			ISyntaxRegion next;
			while ((next = cf.GetNextMetaBlockOrStatStmt (until)) != null) {

				cf.PopMetaBlockDeclaration (next.Location);

				if (next is StaticStatement)
					HandleStaticStatement (next as StaticStatement);
				else
					HandleMetaDecl (next as IMetaDeclaration);
			}

			if (cf.nextStatStmt == null && cf.nextMetaDecl == null) {
				if (cf.StaticStatementEnum != null || cf.MetaBlockEnum != null)
					throw new InvalidOperationException ();

				cf.PopMetaBlockDeclaration (until);
			}
		}

		void HandleStaticStatement(StaticStatement ss)
		{

		}

		void HandleMetaDecl(IMetaDeclaration md)
		{

		}
		#endregion

		#region Is-Expression/in-expression declarations

		void SearchAttributesForIsExprDecls(IBlockNode block, CodeLocation caret, ItemCheckParameters parms)
		{
			var dblock = block as DBlockNode;
			if (dblock != null)
			{
				foreach (var mbl in dblock.GetMetaBlockStack(caret, false, true))
					if (mbl is AttributeMetaDeclaration)
						foreach (var attr in (mbl as AttributeMetaDeclaration).AttributeOrCondition)
							if (attr is StaticIfCondition) {
								HandleExpression ((attr as StaticIfCondition).Expression, parms);
								if (StopEnumerationOnNextScope)
									return;
							}
			}

			var n = DResolver.SearchRegionAt<INode>(block.Children.ItemAt, block.Count, caret) as DNode;

			if (n != null && n.Attributes != null && n.Attributes.Count != 0)
				foreach (var attr in n.Attributes)
					if (attr is StaticIfCondition) {
						HandleExpression ((attr as StaticIfCondition).Expression, parms);
						if (StopEnumerationOnNextScope)
							return;
					}
		}

		class IsExprVisitor : DefaultDepthFirstVisitor
		{
			AbstractVisitor v;
			ItemCheckParameters parms;

			public IsExprVisitor(AbstractVisitor v, ItemCheckParameters parms)
			{
				this.v = v;
				this.parms = parms;
			}

			public override void Visit(IsExpression x)
			{
				if (x.TypeAliasIdentifierHash != 0)
					v.HandleItemInternal(x.ArtificialFirstSpecParam.Representation, parms);
			}
		}

		/// <summary>
		/// Scans an expression for aliases inside Is-Expressions or other implicit declarations
		/// </summary>
		/// <param name="x"></param>
		/// <param name="vis"></param>
		void HandleExpression(IExpression x, ItemCheckParameters parms)
		{
			if (x == null)
				return;

			x.Accept(new IsExprVisitor(this, parms));
		}

		#endregion

		#region Handle-ability checks for Nodes
		bool CanHandleNode(DNode dn, ItemCheckParameters parms, bool checkCompilationConditions = true)
		{
			if (dn == null || !CanAddMemberOfType (parms.VisibleMembers, dn))
				return false;

			if (CompletionOptions.Instance.HideDeprecatedNodes && dn.ContainsAttribute(DTokens.Deprecated))
				return false;

			if (CompletionOptions.Instance.HideDisabledNodes &&
				dn.ContainsPropertyAttribute(BuiltInAtAttribute.BuiltInAttributes.Disable))
				return false;

			if((ctxt.Options & ResolutionOptions.IgnoreAllProtectionAttributes) != ResolutionOptions.IgnoreAllProtectionAttributes){
				if((CanShowMember(dn, ctxt.ScopedBlock) || (parms.isBaseClass && !parms.isMixinAst)) && ((!parms.takeStaticChildrenOnly && (!parms.publicImportsOnly || !parms.isBaseClass)) || IsConstOrStatic(dn)))
				{
					if (!(CheckForProtectedAttribute (dn, ctxt.ScopedBlock) || parms.scopeIsInInheritanceHierarchy))
						return false;
				}
				else
					return false;
			}

			var dm3 = dn as DMethod; // Only show normal & delegate methods
			if (dm3 != null && !(dm3.SpecialType == DMethod.MethodType.Normal || dm3.SpecialType == DMethod.MethodType.Delegate || dm3.NameHash != 0))
				return false;

			return !checkCompilationConditions || MatchesCompilationConditions(dn);
		}

		static bool IsConstOrStatic(DNode dn)
		{
			if (dn is DEnum || dn is DEnumValue)
				return true;

			var dv = dn as DVariable;
			return dn != null && (dn.IsStatic || (dv != null && (dv.IsConst || dv.IsAlias))); // Aliases are always static - it only depends on their base types then
		}

		static bool CanShowMember(DNode dn, IBlockNode scope)
		{
			// http://dlang.org/attribute.html#ProtectionAttribute
			if (dn.ContainsAttribute(DTokens.Private))
				return scope == null || dn.NodeRoot == scope.NodeRoot;
			else if (dn.ContainsAttribute(DTokens.Package))
				return scope == null || dn.NodeRoot is DModule &&
					ModuleNameHelper.ExtractPackageName((dn.NodeRoot as DModule).ModuleName) ==
					ModuleNameHelper.ExtractPackageName((scope.NodeRoot as DModule).ModuleName);

			return CheckForProtectedAttribute(dn, scope);
		}

		static bool CheckForProtectedAttribute(DNode dn, IBlockNode scope)
		{
			if(!dn.ContainsAttribute(DTokens.Protected) || dn.NodeRoot == scope.NodeRoot)
				return true;

			while(scope!=null)
			{
				if(dn == scope || dn.Parent == scope)
					return true;
				scope = scope.Parent as IBlockNode;
			}
			return false;
		}

		public static bool CanAddMemberOfType(MemberFilter vis, INode n)
		{
			if (n is DMethod)
				return n.NameHash != 0 && ((vis & MemberFilter.Methods) == MemberFilter.Methods);
			else if (n is NamedTemplateMixinNode)
				return (vis & (MemberFilter.Variables | MemberFilter.Types)) != 0;
			else if (n is DVariable) {
				var d = n as DVariable;

				if (d.IsAliasThis)
					return false;

				// Only add aliases if at least types,methods or variables shall be shown.
				if (d.IsAlias)
					return
						vis.HasFlag (MemberFilter.Methods) ||
					vis.HasFlag (MemberFilter.Types) ||
					vis.HasFlag (MemberFilter.Variables);

				return (vis & MemberFilter.Variables) == MemberFilter.Variables;
			} else if (n is DClassLike) {
				var dc = n as DClassLike;
				switch (dc.ClassType) {
				case DTokens.Class:
					return (vis & MemberFilter.Classes) != 0;
				case DTokens.Interface:
					return (vis & MemberFilter.Interfaces) != 0;
				case DTokens.Template:
					return (vis & MemberFilter.Templates) != 0;
				case DTokens.Struct:
				case DTokens.Union:
					return dc.IsAnonymous ?
							(vis & MemberFilter.Variables) != 0 : 
							(vis & MemberFilter.StructsAndUnions) != 0;
				}
			} else if (n is DEnum) {
				var d = n as DEnum;

				// Only show enums if a) they're named and enums are allowed or b) variables are allowed
				return d.IsAnonymous ? 
					(vis & MemberFilter.Variables) != 0 :
					(vis & MemberFilter.Enums) != 0;
			}

			return true;
		}
		#endregion

		
	}
}
