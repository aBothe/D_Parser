using System;
using System.Collections.Generic;

using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Parser;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ASTScanner
{
	public abstract class AbstractVisitor
	{
		#region Properties
		public static DVariable __ctfe;
		Dictionary<string, List<string>> scannedModules = new Dictionary<string, List<string>>();
		WeakReference tempResolvedNodeParent = new WeakReference(null);
		protected DSymbol TemporaryResolvedNodeParent
		{
			get { return tempResolvedNodeParent.Target as DSymbol; }
			set { tempResolvedNodeParent.Target = value; }
		}

		static ImportStatement.Import _objectImport = new ImportStatement.Import
		{
			ModuleIdentifier = new IdentifierDeclaration("object")
		};

		protected readonly ResolutionContext ctxt;

		Stack<ConditionsFrame> ConditionsStack;// = new Stack<ConditionsFrame>();
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
		
		public virtual IEnumerable<DModule> PrefilterSubnodes(ModulePackage pack, out ModulePackage[] subPackages)
		{
			subPackages = pack.GetPackages();
			if(subPackages.Length == 0)
				subPackages = null;
			
			var mods = pack.GetModules();
			return mods.Length != 0 ? mods : null;
		}

		/// <summary>
		/// Return true if search shall stop(!), false if search shall go on
		/// </summary>
		protected abstract bool HandleItem(INode n);
		
		protected abstract bool HandleItem(PackageSymbol pack);

		protected virtual bool HandleItems(IEnumerable<INode> nodes)
		{
			foreach (var n in nodes)
				if (HandleItem(n))
					return true;
			return false;
		}

		public virtual void IterateThroughScopeLayers(CodeLocation Caret, MemberFilter VisibleMembers = MemberFilter.All)
		{
			if (ctxt.ScopedStatement != null &&
				ScanStatementHierarchy(ctxt.ScopedStatement, Caret, VisibleMembers))
			{
				if (ctxt.ScopedBlock is DMethod &&
					ScanBlock(ctxt.ScopedBlock, Caret, VisibleMembers))
				{
					// Error: Locals are shadowing parameters!
				}
				
				return;
			}

			if(ctxt.ScopedBlock != null && 
			   ScanBlockUpward(ctxt.ScopedBlock, Caret, VisibleMembers))
				return;
			
			// Handle available modules/packages
			var nameStubs = new List<string>();
			if(ctxt.ParseCache != null)
				foreach(var root in ctxt.ParseCache)
				{
					ModulePackage[] packs;
					var mods = PrefilterSubnodes(root, out packs);
					
					if(packs != null){
						foreach(var pack in packs)
						{
							if(nameStubs.Contains(pack.Name))
								continue;
							
							HandleItem(new PackageSymbol(pack, null));
							nameStubs.Add(pack.Name);
						}
					}
					
					if(mods != null)
					{
						HandleItems(mods);
					}
				}

			// On the root level, handle __ctfe variable
			if (CanAddMemberOfType(VisibleMembers, __ctfe) &&
				HandleItem(__ctfe))
				return;
		}
		
		bool ScanBlockUpward(IBlockNode curScope, CodeLocation Caret, MemberFilter VisibleMembers)
		{
			// 2)
			do
			{
				if(ScanBlock(curScope, Caret, VisibleMembers))
					return true;
				
				curScope = curScope.Parent as IBlockNode;
			}
			while (curScope != null);

			return false;
		}

		protected bool ScanBlock(IBlockNode curScope, CodeLocation Caret, MemberFilter VisibleMembers, bool publicImportsOnly = false)
		{
			if (SearchAttributesForIsExprDecls(curScope, Caret, VisibleMembers))
				return true;

			if (curScope is DClassLike)
			{
				return DeepScanClass(new ClassType(curScope as DClassLike, null,null), VisibleMembers);
			}
			else if (curScope is DMethod)
			{
				bool breakOnNextScope = false;
				var dm = curScope as DMethod;

				// Add 'out' variable if typing in the out test block currently
				if (dm.OutResultVariable != null && dm.Out != null && dm.GetSubBlockAt(Caret) == dm.Out)
					breakOnNextScope |= HandleItem(new DVariable
					{ // Create pseudo variable
						NameHash = dm.OutResultVariable.IdHash,
						NameLocation = dm.OutResultVariable.Location,
						Type = dm.Type, // TODO: What to do on auto functions?
						Parent = dm,
						Location = dm.OutResultVariable.Location,
						EndLocation = dm.OutResultVariable.EndLocation
					});

				if ((VisibleMembers & MemberFilter.Variables) == MemberFilter.Variables)
					breakOnNextScope |= HandleItems(dm.Parameters);

				if (dm.TemplateParameters != null)
					breakOnNextScope |= HandleItems(dm.TemplateParameterNodes as IEnumerable<DNode>);

				// The method's declaration children are handled above already via BlockStatement.GetItemHierarchy().
				// except AdditionalChildren:
				foreach (var ch in dm.AdditionalChildren)
					if (CanAddMemberOfType(VisibleMembers, ch))
						breakOnNextScope |= HandleItem(ch);

				// If the method is a nested method,
				// this method won't be 'linked' to the parent statement tree directly - 
				// so, we've to gather the parent method and add its locals to the return list
				if (dm.Parent is DMethod)
				{
					var nestedBlock = (dm.Parent as DMethod).GetSubBlockAt(Caret);

					// Search for the deepest statement scope and add all declarations done in the entire hierarchy
					if (nestedBlock != null)
						breakOnNextScope |= ScanStatementHierarchy(nestedBlock.SearchStatementDeeply(Caret), Caret, VisibleMembers);
				}

				return breakOnNextScope;
			}
			else
				return scanChildren(curScope as DBlockNode, VisibleMembers, publicImportsOnly);
		}
		
		protected bool DeepScanClass(UserDefinedType udt, MemberFilter vis, bool resolveBaseClassIfRequired = true)
		{
			bool isBase = false;
			bool scopeIsInInheritanceHierarchy = udt != null && ctxt.ScopedBlockIsInNodeHierarchy(udt.Definition);
			bool takeStaticChildrenOnly = ctxt.ScopedBlock is DMethod && (ctxt.ScopedBlock as DMethod).IsStatic;
			
			// Check if the scoped node's parent is the current class
			if(takeStaticChildrenOnly)
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
			}

			List<TemplateIntermediateType> interfaces = null;

			while(udt!= null)
			{
				if(scanChildren(udt.Definition as DBlockNode, vis, false, isBase, false, takeStaticChildrenOnly, scopeIsInInheritanceHierarchy, udt))
					return true;

				if(udt is TemplateIntermediateType){
					var tit = udt as TemplateIntermediateType;
					var type = tit.Definition.ClassType;

					if ((type == DTokens.Struct || type == DTokens.Class || type == DTokens.Template) &&
						HandleAliasThisDeclarations (tit, vis))
						return true;

					if (tit.BaseInterfaces != null) {
						if (interfaces == null)
							interfaces = new List<TemplateIntermediateType> ();
						foreach (var I in tit.BaseInterfaces)
							EnlistInterfaceHierarchy(interfaces, I);
					}

					if(resolveBaseClassIfRequired && udt.Base == null && 
						(type == DTokens.Class ||type == DTokens.Interface))
						udt = DResolver.ResolveClassOrInterface(udt.Definition as DClassLike, ctxt, udt.DeclarationOrExpressionBase);

					udt = udt.Base as UserDefinedType;

					isBase = true;
				}
				else
					break;
			}

			if (interfaces != null)
				foreach (var I in interfaces)
					if (scanChildren (I.Definition, vis, false, true, false, takeStaticChildrenOnly, scopeIsInInheritanceHierarchy))
						return true;

			return false;
		}

		static void EnlistInterfaceHierarchy(List<TemplateIntermediateType> l, InterfaceType t)
		{
			if (l.Contains(t))
				return;

			l.Add(t);
			if(t.Base is TemplateIntermediateType)
				l.Add(t.Base as TemplateIntermediateType);
			if(t.BaseInterfaces != null && t.BaseInterfaces.Length != 0)
				foreach (var nested in t.BaseInterfaces)
					EnlistInterfaceHierarchy(l, nested);
		}

		/// <summary>
		/// Temporary flag that is used for telling scanChildren() not to handle template parameters.
		/// Used to prevent the insertion of a template mixin's parameter set into the completion list etc.
		/// </summary>
		bool dontHandleTemplateParamsInNodeScan = false;

		bool scanChildren(DBlockNode curScope, 
									MemberFilter VisibleMembers,
									bool publicImports = false,
									bool isBaseClass = false,
									bool isMixinAst = false,
									bool takeStaticChildrenOnly = false,
		                            bool scopeIsInInheritanceHierarchy =false, 
									DSymbol resolvedCurScope = null)
		{
			bool foundItems = false;
			TemporaryResolvedNodeParent = resolvedCurScope;

			//ConditionsStack.Push (new ConditionsFrame (curScope.StaticStatements, curScope.MetaBlocks));

			var ch = PrefilterSubnodes(curScope);
			if (ch != null)
				foreach (var n in ch)
				{
					//ContinueHandleStaticStatements (n.Location);

					if (!CanHandleNode (n as DNode, VisibleMembers, isBaseClass, isMixinAst, takeStaticChildrenOnly, publicImports, scopeIsInInheritanceHierarchy))
						continue;

                    // Add anonymous enums',structs' or unions' items
					if (((n is DEnum) || (n is DClassLike)) && n.NameHash == 0)
					{
						var ch2 = PrefilterSubnodes(n as DBlockNode);
						if (ch2 != null)
							foundItems |= HandleItems(ch2);
						continue;
					}

					foundItems |= HandleItem(n);
				}

			if (foundItems) {
				//ConditionsStack.Pop ();
				return true;
			}

			if (!dontHandleTemplateParamsInNodeScan)
			{
				if (curScope.TemplateParameters != null && (VisibleMembers & MemberFilter.TypeParameters) != 0)
				{
					var t = ctxt.ScopedBlock;
					while (t != null)
					{
						if (t == curScope)
						{
							if (HandleItems (curScope.TemplateParameterNodes as IEnumerable<INode>)) {
								//ConditionsStack.Pop ();
								return true;
							}
							break;
						}
						t = t.Parent as IBlockNode;
					}
				}
			}
			else
				dontHandleTemplateParamsInNodeScan = false;

			//ContinueHandleStaticStatements (curScope.EndLocation);	ConditionsStack.Pop ();

			var ss = new StatementHandler(this)
			{
				handlePublicImportsOnly = publicImports,
				caretInsensitive = true,
				VisibleMembers = VisibleMembers
			};

			return curScope.Accept(ss);
		}

		/// <summary>
		/// Walks up the statement scope hierarchy and enlists all declarations that have been made BEFORE the caret position. 
		/// (If CodeLocation.Empty given, this parameter will be ignored)
		/// </summary>
		/// <returns>True if scan shall stop, false if not</returns>
		bool ScanStatementHierarchy(IStatement Statement, CodeLocation Caret, MemberFilter VisibleMembers)
		{
			// To a prevent double entry of the same declaration, skip a most scoped declaration first
			if (Statement is DeclarationStatement)
				Statement = Statement.Parent;

			var ss = new StatementHandler(this) { Caret=Caret, caretInsensitive = Caret.IsEmpty, VisibleMembers = VisibleMembers };

			while (Statement != null)
			{
				if (Statement.Accept(ss))
					return true;

				Statement = Statement.Parent;
			}

			return false;
		}

		class StatementHandler : StatementVisitor<bool>, NodeVisitor<bool>
		{
			#region Properties
			public bool handlePublicImportsOnly;
			public bool caretInsensitive;
			public CodeLocation Caret;
			public MemberFilter VisibleMembers;
			ResolutionContext ctxt { get { return v.ctxt; } }

			readonly AbstractVisitor v;
			#endregion

			#region Constuctor/IO
			public StatementHandler(AbstractVisitor v)
			{
				this.v = v;
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
				var ret = !handlePublicImportsOnly && _objectImport.Accept(this);
				
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
					if (v.HandleExpression(dv.Initializer, VisibleMembers))
						return true;
				}

				return VisitDNode(dv);
			}

			public bool VisitDNode(DNode decl)
			{
				return (caretInsensitive || Caret >= decl.Location) && v.HandleItem(decl);
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
						if (x != null && x.Location < Caret && x.EndLocation >= Caret && v.HandleExpression(x, VisibleMembers))
							return true;
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

						if (!caretInsensitive && s.Location >= Caret)
							break;

						if (s.Accept(this))
							return true;
					}
				}

				return false;
			}

			public bool Visit(ImportStatement impStmt)
			{
				if ((handlePublicImportsOnly && impStmt != null && !impStmt.IsPublic) ||
					!ctxt.CurrentContext.MatchesDeclarationEnvironment(impStmt.Attributes))
					return false;

				/*
				 * Mainly used for selective imports/import module aliases
				 */
				foreach (var decl in impStmt.PseudoAliases)
					if (v.HandleItem(decl)) //TODO: Handle visibility?
						return true;

				var ret = false;

				if (!impStmt.IsStatic)
					foreach (var imp in impStmt.Imports)
						ret |= imp.ModuleAlias == null && imp.Accept(this);

				return ret;
			}

			public bool VisitImport(ImportStatement.Import imp)
			{
				if (imp == null || imp.ModuleIdentifier == null)
					return false;

				DModule mod;
				var thisModuleName = (ctxt.ScopedBlock != null && (mod = ctxt.ScopedBlock.NodeRoot as DModule) != null) ? mod.ModuleName : string.Empty;

				if (string.IsNullOrEmpty(thisModuleName))
					return false;

				var moduleName = imp.ModuleIdentifier.ToString();

				List<string> seenModules = null;

				if (!v.scannedModules.TryGetValue(thisModuleName, out seenModules))
					seenModules = v.scannedModules[thisModuleName] = new List<string>();
				else if (seenModules.Contains(moduleName))
					return false;
				seenModules.Add(moduleName);

				var scAst = ctxt.ScopedBlock == null ? null : ctxt.ScopedBlock.NodeRoot as DModule;
				if (ctxt.ParseCache != null)
					foreach (var module in ctxt.ParseCache.LookupModuleName(moduleName)) //TODO: Only take the first module? Notify the user about ambigous module names?
					{
						if (module == null || (scAst != null && module.FileName == scAst.FileName && module.FileName != null))
							continue;

						//ctxt.PushNewScope(module);

						if (v.scanChildren(module as DModule, VisibleMembers, true))
						{
							//ctxt.Pop();
							return true;
						}

						//ctxt.Pop();
					}
				return false;
			}
			#endregion

			#region Template Mixins
			public bool Visit(TemplateMixin s)
			{
				return ctxt.CurrentContext.MatchesDeclarationEnvironment(s.Attributes) &&
					HandleUnnamedTemplateMixin(s, caretInsensitive, VisibleMembers);
			}
			
			static ResolutionCache<AbstractType> templateMixinCache = new ResolutionCache<AbstractType>();
			[ThreadStatic]
			static List<TemplateMixin> templateMixinsBeingAnalyzed;
			
			// http://dlang.org/template-mixin.html#TemplateMixin
			bool HandleUnnamedTemplateMixin(TemplateMixin tmx, bool treatAsDeclBlock, MemberFilter vis)
			{
				if (CompletionOptions.Instance.DisableMixinAnalysis)
					return false;

				if (templateMixinsBeingAnalyzed == null)
					templateMixinsBeingAnalyzed = new List<TemplateMixin>();

				if (templateMixinsBeingAnalyzed.Contains(tmx))
					return false;
				templateMixinsBeingAnalyzed.Add(tmx);

				var tmxTemplate = GetTemplateMixinContent(ctxt, tmx, false);

				bool res = false;
				if (tmxTemplate == null)
					ctxt.LogError(tmx.Qualifier, "Mixin qualifier must resolve to a mixin template declaration.");
				else
				{
					bool pop = !ctxt.ScopedBlockIsInNodeHierarchy(tmxTemplate.Definition);
					if (pop)
						ctxt.PushNewScope(tmxTemplate.Definition);
					ctxt.CurrentContext.IntroduceTemplateParameterTypes(tmxTemplate);
					v.dontHandleTemplateParamsInNodeScan = true;
					res |= v.DeepScanClass(tmxTemplate, vis);
					if (pop)
						ctxt.Pop();
					else
						ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(tmxTemplate);
				}

				templateMixinsBeingAnalyzed.Remove(tmx);
				return res;
			}
			#endregion

			#region Mixin
			public bool VisitMixinStatement(MixinStatement s)
			{
				return ctxt.CurrentContext.MatchesDeclarationEnvironment(s.Attributes) &&
					HandleMixin(s, caretInsensitive, VisibleMembers);
			}

			/// <summary>
			/// Evaluates the literal given as expression and tries to parse it as a string.
			/// Important: Assumes all its compilation conditions to be checked already!
			/// </summary>
			bool HandleMixin(MixinStatement mx, bool parseDeclDefs, MemberFilter vis)
			{
				if (CompletionOptions.Instance.DisableMixinAnalysis)
					return false;

				// If in a class/module block => MixinDeclaration
				if (parseDeclDefs)
				{
					var ast = MixinAnalysis.ParseMixinDeclaration(mx, ctxt);

					if (ast == null)
						return false;

					// take ast.Endlocation because the cursor must be beyond the actual mixin expression 
					// - and therewith _after_ each declaration
					if (ctxt.ScopedBlock == mx.ParentNode.NodeRoot)
						return v.ScanBlockUpward(ast, ast.EndLocation, vis);
					else
					{
						return v.scanChildren(ast, vis, isMixinAst: true);
					}
				}
				else // => MixinStatement
				{
					var bs = MixinAnalysis.ParseMixinStatement(mx, ctxt);

					// As above, disregard the caret position because 1) caret and parsed code do not match 
					// and 2) the caret must be located somewhere after the mixin statement's end
					if (bs != null)
					{
						return v.ScanStatementHierarchy(bs, CodeLocation.Empty, vis);
					}
				}

				return false;
			}

			public static MixinTemplateType GetTemplateMixinContent(ResolutionContext ctxt, TemplateMixin tmx, bool pushOnAnalysisStack = true)
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
				if (!templateMixinCache.TryGet(ctxt, tmx, out t))
				{
					t = TypeDeclarationResolver.ResolveSingle(tmx.Qualifier, ctxt);
					// Deadly important: To prevent mem leaks, all references from the result to the TemplateMixin must be erased!
					// Elsewise there remains one reference from the dict value to the key - and won't get free'd THOUGH we can't access it anymore
					if (t != null)
						t.DeclarationOrExpressionBase = null;
					templateMixinCache.Add(ctxt, tmx, t);
				}

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

				return VisitExpressionStmt(s);
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
						if (n.Accept(this))
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

				var back = ctxt.ScopedStatement;
				ctxt.CurrentContext.Set(ws.Parent);

				AbstractType r;
				// Must be an expression that returns an object reference
				if (ws.WithExpression != null)
					r = ExpressionTypeEvaluation.EvaluateType(ws.WithExpression, ctxt);
				else if (ws.WithSymbol != null) // This symbol will be used as default
					r = TypeDeclarationResolver.ResolveSingle(ws.WithSymbol, ctxt);
				else
					r = null;

				ctxt.CurrentContext.Set(back);

				if ((r = DResolver.StripMemberSymbols(r)) is TemplateIntermediateType &&
					v.DeepScanClass(r as TemplateIntermediateType, VisibleMembers))
					return true;

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

			public bool Visit(AssertStatement s)
			{
				return VisitExpressionStmt(s);
			}

			public bool Visit(StatementCondition sc)
			{
				if ((ctxt.Options & ResolutionOptions.IgnoreDeclarationConditions) != 0)
				{
					if (sc.ScopedStatement != null && sc.ScopedStatement.Accept(this))
							return false;

					return sc.ElseStatement != null && sc.ElseStatement.Accept(this);
				}

				if (!ctxt.CurrentContext.MatchesDeclarationEnvironment(sc.Condition))
					return false;

				if(sc.Condition is StaticIfCondition && sc.Location < Caret && sc.EndLocation >= Caret &&
					v.HandleExpression((sc.Condition as StaticIfCondition).Expression, VisibleMembers))
					return true;

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

		public static MixinTemplateType GetTemplateMixinContent(ResolutionContext ctxt, TemplateMixin tmx, bool pushOnAnalysisStack = true)
		{
			return StatementHandler.GetTemplateMixinContent(ctxt, tmx, pushOnAnalysisStack);
		}

		[ThreadStatic]
		static Dictionary<IBlockNode, DVariable> aliasThisDefsBeingParsed;

		bool HandleAliasThisDeclarations(TemplateIntermediateType tit, MemberFilter vis)
		{
			bool pop;
			var ch = tit.Definition [DVariable.AliasThisIdentifierHash];
			if(ch != null){
				foreach (DVariable aliasDef in ch) {
					if (aliasDef.Type == null || !MatchesCompilationConditions(aliasDef))
						continue;

					if (aliasThisDefsBeingParsed == null)
						aliasThisDefsBeingParsed = new Dictionary<IBlockNode, DVariable>();
					
					pop = ctxt.ScopedBlock != tit.Definition;
					if (pop)
					{
						ctxt.PushNewScope(tit.Definition);
						ctxt.CurrentContext.IntroduceTemplateParameterTypes(tit);
					}

					DVariable alreadyParsedAliasThis;
					AbstractType aliasedSymbol;

					if (!aliasThisDefsBeingParsed.TryGetValue(ctxt.ScopedBlock, out alreadyParsedAliasThis) || alreadyParsedAliasThis != aliasDef)
					{
						aliasThisDefsBeingParsed[ctxt.ScopedBlock] = aliasDef;

						// Resolve the aliased symbol and expect it to be a member symbol(?).
						//TODO: Check if other cases are allowed as well!
						aliasedSymbol = DResolver.StripMemberSymbols(TypeDeclarationResolver.ResolveSingle(aliasDef.Type, ctxt));

						aliasThisDefsBeingParsed.Remove(ctxt.ScopedBlock);

						if (aliasedSymbol is TemplateParameterSymbol)
							aliasedSymbol = DResolver.StripAliasSymbol((aliasedSymbol as TemplateParameterSymbol).Base);
					}
					else
						aliasedSymbol = null;

					if (pop)
						ctxt.Pop ();

					

					foreach (var statProp in StaticProperties.ListProperties (aliasedSymbol))
						if (HandleItem (statProp))
							return true;

					/** TODO: Visit ufcs recommendations and other things that
					 * become added in e.g. MemberCompletionProvider
					 */

					var tit_ = aliasedSymbol as TemplateIntermediateType;
					DSymbol ds;
					if (tit_ != null)
					{
						pop = !ctxt.ScopedBlockIsInNodeHierarchy(tit_.Definition);
						if (pop)
							ctxt.PushNewScope(tit_.Definition);
						ctxt.CurrentContext.IntroduceTemplateParameterTypes(tit_);
						var r = DeepScanClass(tit_, vis, true);
						if (pop)
							ctxt.Pop();
						else
							ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(tit_);
						if (r)
							return true;
					}
					// Applies to DEnums
					else if ((ds = aliasedSymbol as DSymbol) != null && ds.Definition is DBlockNode &&
						scanChildren(ds.Definition as DBlockNode, vis, resolvedCurScope:ds))
						return true;
				}
			}

			return false;
		}

		#region Declaration conditions & Static statements
		bool MatchesCompilationConditions(DNode n)
		{
			if((ctxt.Options & ResolutionOptions.IgnoreDeclarationConditions) != 0)
				return true;

			return ctxt.CurrentContext.MatchesDeclarationEnvironment (n.Attributes);
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

		bool SearchAttributesForIsExprDecls(IBlockNode block, CodeLocation caret, MemberFilter vis)
		{
			var dblock = block as DBlockNode;
			if (dblock != null)
			{
				foreach(var mbl in dblock.GetMetaBlockStack(caret, false, true))
					if(mbl is AttributeMetaDeclaration)
						foreach(var attr in (mbl as AttributeMetaDeclaration).AttributeOrCondition)
							if (attr is StaticIfCondition)
								if (HandleExpression((attr as StaticIfCondition).Expression, vis))
									return true;
			}

			var n = DResolver.SearchRegionAt<INode>(block.Children.ItemAt, block.Count, caret) as DNode;

			if (n != null && n.Attributes != null && n.Attributes.Count != 0)
				foreach (var attr in n.Attributes)
					if (attr is StaticIfCondition)
						if (HandleExpression((attr as StaticIfCondition).Expression, vis))
							return true;

			return false;
		}

		class IsExprVisitor : DefaultDepthFirstVisitor
		{
			AbstractVisitor v;
			public bool ret;

			public IsExprVisitor(AbstractVisitor v)
			{
				this.v = v;
			}

			public override void Visit(IsExpression x)
			{
				if (x.TypeAliasIdentifierHash != 0)
					ret |= v.HandleItem(x.ArtificialFirstSpecParam.Representation);
			}
		}

		IsExprVisitor isExprVisitor;

		/// <summary>
		/// Scans an expression for aliases inside Is-Expressions or other implicit declarations
		/// </summary>
		/// <param name="x"></param>
		/// <param name="vis"></param>
		/// <returns></returns>
		bool HandleExpression(IExpression x, MemberFilter vis)
		{
			if (isExprVisitor == null)
				isExprVisitor = new IsExprVisitor(this);

			x.Accept(isExprVisitor);

			if (isExprVisitor.ret)
			{
				isExprVisitor.ret = false;
				return true;
			}

			return false;
		}

		#endregion

		#region Handle-ability checks for Nodes
		bool CanHandleNode(DNode dn, MemberFilter VisibleMembers, bool isBaseClass, bool isMixinAst, bool takeStaticChildrenOnly, bool publicImports, bool scopeIsInInheritanceHierarchy)
		{
			if (dn == null || !MatchesCompilationConditions(dn) ||
				!CanAddMemberOfType (VisibleMembers, dn))
				return false;

			if((ctxt.Options & ResolutionOptions.IgnoreAllProtectionAttributes) != ResolutionOptions.IgnoreAllProtectionAttributes){
				if((CanShowMember(dn, ctxt.ScopedBlock) || isBaseClass && !isMixinAst) && ((!takeStaticChildrenOnly && (!publicImports || !isBaseClass)) || IsConstOrStatic(dn)))
				{
					if (!(CheckForProtectedAttribute (dn, ctxt.ScopedBlock) || scopeIsInInheritanceHierarchy))
						return false;
				}
				else
					return false;
			}

			var dm3 = dn as DMethod; // Only show normal & delegate methods
			if (dm3 != null && !(dm3.SpecialType == DMethod.MethodType.Normal || dm3.SpecialType == DMethod.MethodType.Delegate || dm3.NameHash != 0))
				return false;

			return true;
		}

		static bool IsConstOrStatic(DNode dn)
		{
			var dv = dn as DVariable;
			return dn != null && (dn.IsStatic || (dv != null && (dv.IsConst || dv.IsAlias))); // Aliases are always static - it only depends on their base types then
		}

		static bool CanShowMember(DNode dn, IBlockNode scope)
		{
			if (dn.ContainsAttribute(DTokens.Deprecated) && CompletionOptions.Instance.HideDeprecatedNodes)
				return false;

			// http://dlang.org/attribute.html#ProtectionAttribute
			if (dn.ContainsAttribute(DTokens.Private))
				return dn.NodeRoot == scope.NodeRoot;
			else if (dn.ContainsAttribute(DTokens.Package))
				return dn.NodeRoot is DModule &&
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
			else if (n is DVariable)
			{
				var d = n as DVariable;

				if (d.IsAliasThis)
					return false;

				// Only add aliases if at least types,methods or variables shall be shown.
				if (d.IsAlias)
					return
						vis.HasFlag(MemberFilter.Methods) ||
						vis.HasFlag(MemberFilter.Types) ||
						vis.HasFlag(MemberFilter.Variables);

				return (vis & MemberFilter.Variables) == MemberFilter.Variables;
			}

			else if (n is DClassLike)
			{
				var dc = n as DClassLike;
				switch (dc.ClassType)
				{
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
			}

			else if (n is DEnum)
			{
				var d = n as DEnum;

				// Only show enums if a) they're named and enums are allowed or b) variables are allowed
				return d.IsAnonymous ? 
					(vis & MemberFilter.Variables) != 0 :
					(vis & MemberFilter.Enums) != 0;
			}

			return false;
		}
		#endregion

		
	}
}
