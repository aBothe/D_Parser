using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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

		static ImportStatement.Import _objectImport = new ImportStatement.Import
		{
			ModuleIdentifier = new IdentifierDeclaration("object")
		};

		protected readonly ResolutionContext ctxt;
		#endregion

		#region Constructor
		public AbstractVisitor(ResolutionContext context)
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
				for(int i=0;i < ctxt.ParseCache.Count; i++)
				{
					ModulePackage[] packs;
					var mods = PrefilterSubnodes(ctxt.ParseCache[i].Root, out packs);
					
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

		protected bool ScanBlock(IBlockNode curScope, CodeLocation Caret, MemberFilter VisibleMembers)
		{
			if (curScope is DClassLike)
			{
				return DeepScanClass(curScope as DClassLike, VisibleMembers);
			}
			else if (curScope is DMethod)
			{
				bool breakOnNextScope = false;
				var dm = curScope as DMethod;

				// Add 'out' variable if typing in the out test block currently
				if (dm.OutResultVariable != null && dm.Out != null && dm.GetSubBlockAt(Caret) == dm.Out)
					breakOnNextScope |= HandleItem(new DVariable
					{ // Create pseudo variable
						Name = dm.OutResultVariable.Id as string,
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
				return scanChildren(curScope as DBlockNode, VisibleMembers);
		}
		
		
		bool DeepScanClass(DClassLike cls, MemberFilter VisibleMembers)
		{
			return DeepScanClass(new ClassType(cls, null, null), VisibleMembers, true);
		}
		
		protected bool DeepScanClass(UserDefinedType udt, MemberFilter vis, bool resolveBaseClassIfRequired = false)
		{
			bool isBase = false;
			bool scopeIsInInheritanceHierarchy = udt != null && ctxt.NodeIsInCurrentScopeHierarchy(udt.Definition);
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

			while(udt!= null)
			{
				if(scanChildren(udt.Definition as DBlockNode, vis, false, isBase, false, takeStaticChildrenOnly, scopeIsInInheritanceHierarchy))
					return true;
				
				if(udt is TemplateIntermediateType){
					if(resolveBaseClassIfRequired && udt.Base == null && 
					   udt.Definition is DClassLike && (udt.Definition as DClassLike).ClassType == DTokens.Class)
						udt = DResolver.ResolveBaseClasses(udt, ctxt, true);
					
					udt = udt.Base as UserDefinedType;
					
					isBase = true;
				}
				else
					break;
			}
			return false;
		}
		
		protected bool scanChildren(DBlockNode curScope, 
									MemberFilter VisibleMembers,
									bool publicImports = false,
									bool isBaseClass = false,
									bool isMixinAst = false,
									bool takeStaticChildrenOnly = false,
		                            bool scopeIsInInheritanceHierarchy =false)
		{
			bool foundItems = false;

			var ch = PrefilterSubnodes(curScope);
			if (ch != null)
				foreach (var n in ch)
				{
					var dn = n as DNode;
					if(dn!=null && !ctxt.CurrentContext.MatchesDeclarationEnvironment(dn))
						continue;
					
					if((ctxt.Options & ResolutionOptions.IgnoreAllProtectionAttributes) != ResolutionOptions.IgnoreAllProtectionAttributes){
						if((CanShowMember(dn, ctxt.ScopedBlock) || isBaseClass && !isMixinAst) && ((!takeStaticChildrenOnly && (!publicImports || !isBaseClass)) || IsConstOrStatic(dn)))
						{
							if (!(CheckForProtectedAttribute (dn, ctxt.ScopedBlock) || scopeIsInInheritanceHierarchy))
								continue;
						}
						else
							continue;
					}

					// Add anonymous enums' items
					if (dn is DEnum && string.IsNullOrEmpty(dn.Name) && CanAddMemberOfType(VisibleMembers, dn))
					{
						var ch2 = PrefilterSubnodes(dn as DEnum);
						if (ch2 != null)
							foundItems |= HandleItems(ch2);
						continue;
					}

					var dm3 = dn as DMethod; // Only show normal & delegate methods
					if (!CanAddMemberOfType(VisibleMembers, n) ||
						(dm3 != null && !(dm3.SpecialType == DMethod.MethodType.Normal || dm3.SpecialType == DMethod.MethodType.Delegate || dm3.Name != null)))
						continue;

					foundItems |= HandleItem(n);
				}

			if (foundItems)
				return true;

			if (!dontHandleTemplateParamsInNodeScan)
			{
				if (curScope.TemplateParameters != null && (VisibleMembers & MemberFilter.TypeParameters) != 0)
				{
					var t = ctxt.ScopedBlock;
					while (t != null)
					{
						if (t == curScope)
						{
							if (HandleItems(curScope.TemplateParameterNodes as IEnumerable<INode>))
								return true;
							break;
						}
						t = t.Parent as IBlockNode;
					}
				}
			}
			else
				dontHandleTemplateParamsInNodeScan = false;
			
			return HandleDBlockNode(curScope, VisibleMembers, publicImports);
		}
		
		static bool IsConstOrStatic(DNode dn)
		{
			return dn != null && (dn.IsStatic || ((dn is DVariable) && (dn as DVariable).IsConst));
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
				return !string.IsNullOrEmpty(n.Name) && ((vis & MemberFilter.Methods) == MemberFilter.Methods);

			else if (n is DVariable)
			{
				var d = n as DVariable;

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
						return (vis & MemberFilter.StructsAndUnions) != 0;
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
			else if (n is NamedTemplateMixinNode)
				return (vis & (MemberFilter.Variables | MemberFilter.Types)) == (MemberFilter.Variables | MemberFilter.Types);
			
			return false;
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

			while (Statement != null)
			{
				if (ScanSingleStatement(Statement, Caret, VisibleMembers))
					return true;

				Statement = Statement.Parent;
			}

			return false;
		}

		bool ScanSingleStatement(IStatement Statement, CodeLocation Caret, MemberFilter VisibleMembers)
		{
			if (Statement is ImportStatement)
			{
				// Handled in DBlockNode
			}
			else if (Statement is IDeclarationContainingStatement)
			{
				var decls = ((IDeclarationContainingStatement)Statement).Declarations;

				if (decls != null)
					foreach (var decl in decls)
					{
						if (Caret != CodeLocation.Empty)
						{
							if (Caret < decl.Location)
								continue;

							var dv = decl as DVariable;
							if (dv != null &&
								dv.Initializer != null &&
								!(Caret < dv.Initializer.Location ||
								Caret > dv.Initializer.EndLocation))
								continue;
						}

						if (HandleItem(decl))
							return true;
					}
			}
			/// http://dlang.org/statement.html#WithStatement
			else if (Statement is WithStatement)
			{
				var ws = (WithStatement)Statement;

				if (ws.ScopedStatement == null || Caret < ws.ScopedStatement.Location)
					return false;

				AbstractType r = null;

				var back = ctxt.ScopedStatement;
				ctxt.CurrentContext.Set(ws.Parent);

				// Must be an expression that returns an object reference
				if (ws.WithExpression != null)
					r = Evaluation.EvaluateType(ws.WithExpression, ctxt);
				else if (ws.WithSymbol != null) // This symbol will be used as default
					r = TypeDeclarationResolver.ResolveSingle(ws.WithSymbol, ctxt);

				ctxt.CurrentContext.Set(back);

				if ((r = DResolver.StripMemberSymbols(r)) != null)
					if (r is TemplateIntermediateType && 
						DeepScanClass((r as TemplateIntermediateType).Definition as DClassLike, VisibleMembers))
							return true;
			}

			if (Statement is StatementContainingStatement)
				foreach (var s in (Statement as StatementContainingStatement).SubStatements)
				{
					/*
					 * void foo()
					 * {
					 * 
					 *	writeln(); -- error, writeln undefined
					 *	
					 *  import std.stdio;
					 *  
					 *  writeln(); -- ok
					 * 
					 * }
					 */
					if (s == null || 
					    Caret < s.Location && Caret != CodeLocation.Empty ||
					    s is ModuleStatement)
						continue;
					
					if (s is StatementCondition)
					{
						var sc = (StatementCondition)s;

						if (ctxt.CurrentContext.MatchesDeclarationEnvironment(sc.Condition))
							return ScanSingleStatement(sc.ScopedStatement, Caret, VisibleMembers);
						else if (sc.ElseStatement != null)
							return ScanSingleStatement(sc.ElseStatement, Caret, VisibleMembers);
					}
					
					var ss = s as StaticStatement;
					if(ss==null || !ctxt.CurrentContext.MatchesDeclarationEnvironment(ss.Attributes))
						continue;
					
					if (s is ImportStatement)
					{
						// Selective imports were handled in the upper section already!

						var impStmt = (ImportStatement)s;

						foreach (var imp in impStmt.Imports)
							if (imp.ModuleAlias == null)
								if (HandleNonAliasedImport(imp, VisibleMembers))
									return true;
					}
					else if (s is MixinStatement)
					{
						if(HandleMixin(s as MixinStatement, false, VisibleMembers))
							return true;
					}
					else if (s is TemplateMixin)
					{
						if(HandleUnnamedTemplateMixin(s as TemplateMixin, false, VisibleMembers))
							return true;
					}
				}

			return false;
		}

		#region Imports
		/// <summary>
		/// Handle the node's static statements (but not the node itself)
		/// </summary>
		bool HandleDBlockNode(DBlockNode dbn, MemberFilter VisibleMembers, bool takePublicImportsOnly = false)
		{
			bool foundItems = false;

			if (dbn != null && dbn.StaticStatements != null)
			{
				foreach (var stmt in dbn.StaticStatements)
				{
					var dstmt = stmt as IDeclarationContainingStatement;
					if (dstmt != null)
					{
						var impStmt = dstmt as ImportStatement;
						if ((takePublicImportsOnly && impStmt!=null && !impStmt.IsPublic) || !MatchesCompilationEnv(stmt))
							continue;

						/*
						 * Mainly used for selective imports/import module aliases
						 */
						if (dstmt.Declarations != null)
							foreach (var d in dstmt.Declarations)
								foundItems |= HandleItem(d); //TODO: Handle visibility?

						if (impStmt!=null)
						{
							foreach (var imp in impStmt.Imports)
								if (imp.ModuleAlias == null)
									foundItems |= HandleNonAliasedImport(imp, VisibleMembers);
						}
					}
					else if(stmt is MixinStatement)
					{
						if(MatchesCompilationEnv(stmt))
							foundItems |= HandleMixin(stmt as MixinStatement,true,VisibleMembers);
					}
					else if(stmt is TemplateMixin)
					{
						if (MatchesCompilationEnv(stmt))
							foundItems |= HandleUnnamedTemplateMixin(stmt as TemplateMixin, true, VisibleMembers);
					}
				}
			}

			// Every module imports 'object' implicitly
			if (dbn is DModule && !takePublicImportsOnly)
				foundItems |= HandleNonAliasedImport(_objectImport, VisibleMembers);

			return foundItems;
		}
		
		bool MatchesCompilationEnv(StaticStatement ss)
		{
			return ss.Attributes == null || ctxt.CurrentContext.MatchesDeclarationEnvironment(ss.Attributes);
		}

		bool HandleNonAliasedImport(ImportStatement.Import imp, MemberFilter VisibleMembers)
		{
			if (imp == null || imp.ModuleIdentifier == null)
				return false;

			var thisModuleName = (ctxt.ScopedBlock != null && ctxt.ScopedBlock.NodeRoot is DModule) ? ((DModule)ctxt.ScopedBlock.NodeRoot).ModuleName : string.Empty;
			
			if(string.IsNullOrEmpty(thisModuleName))
				return false;
			
			var moduleName = imp.ModuleIdentifier.ToString();
			
			List<string> seenModules = null;

			if (!scannedModules.TryGetValue(thisModuleName, out seenModules))
				seenModules = scannedModules[thisModuleName] = new List<string>();
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

					if(ScanImportedModule(module as DModule,VisibleMembers))
					{
						//ctxt.Pop();
						return true;
					}
					
					//ctxt.Pop();
				}
			return false;
		}
		
		bool ScanImportedModule(DModule module, MemberFilter VisibleMembers)
		{
			return scanChildren(module, VisibleMembers, true);
		}
		#endregion
		
		#region Mixins
		/// <summary>
		/// Evaluates the literal given as expression and tries to parse it as a string.
		/// Important: Assumes all its compilation conditions to be checked already!
		/// </summary>
		bool HandleMixin(MixinStatement mx, bool parseDeclDefs, MemberFilter vis)
		{
			if (CompletionOptions.Instance.DisableMixinAnalysis)
				return false;

			// If in a class/module block => MixinDeclaration
			if(parseDeclDefs)
			{
				var ast = MixinAnalysis.ParseMixinDeclaration(mx, ctxt);
				
				if(ast ==null)
					return false;
				
				// take ast.Endlocation because the cursor must be beyond the actual mixin expression 
				// - and therewith _after_ each declaration
				if(ctxt.ScopedBlock == mx.ParentNode.NodeRoot)
					return ScanBlockUpward(ast, ast.EndLocation, vis);
				else
				{
					return scanChildren(ast, vis, isMixinAst:true);
				}
			}
			else // => MixinStatement
			{
				var bs = MixinAnalysis.ParseMixinStatement(mx, ctxt);
				
				// As above, disregard the caret position because 1) caret and parsed code do not match 
				// and 2) the caret must be located somewhere after the mixin statement's end
				if(bs!=null){
					return ScanStatementHierarchy(bs, CodeLocation.Empty, vis);
				}
			}
			
			return false;
		}

		public static MixinTemplateType GetTemplateMixinContent (ResolutionContext ctxt, TemplateMixin tmx, bool pushOnAnalysisStack = true)
		{
			if (pushOnAnalysisStack) {
				if(templateMixinsBeingAnalyzed == null)
					templateMixinsBeingAnalyzed = new List<TemplateMixin>();
				
				if(templateMixinsBeingAnalyzed.Contains(tmx))
					return null;
				templateMixinsBeingAnalyzed.Add(tmx);
			}

			AbstractType t;
			if(!templateMixinCache.TryGet(ctxt, tmx, out t))
			{
				t = TypeDeclarationResolver.ResolveSingle(tmx.Qualifier, ctxt);
				// Deadly important: To prevent mem leaks, all references from the result to the TemplateMixin must be erased!
				// Elsewise there remains one reference from the dict value to the key - and won't get free'd THOUGH we can't access it anymore
				if(t != null)
					t.DeclarationOrExpressionBase = null;
				templateMixinCache.Add(ctxt, tmx, t);
			}

			if(pushOnAnalysisStack)
				templateMixinsBeingAnalyzed.Remove(tmx);

			return t as MixinTemplateType;
		}

		static ResolutionCache<AbstractType> templateMixinCache = new ResolutionCache<AbstractType>();
		[ThreadStatic]
		static List<TemplateMixin> templateMixinsBeingAnalyzed;
		/// <summary>
		/// Temporary flag that is used for telling scanChildren() not to handle template parameters.
		/// Used to prevent the insertion of a template mixin's parameter set into the completion list etc.
		/// </summary>
		bool dontHandleTemplateParamsInNodeScan = false;
		// http://dlang.org/template-mixin.html#TemplateMixin
		bool HandleUnnamedTemplateMixin(TemplateMixin tmx, bool treatAsDeclBlock, MemberFilter vis)
		{
			if (CompletionOptions.Instance.DisableMixinAnalysis)
				return false;

			if(templateMixinsBeingAnalyzed == null)
				templateMixinsBeingAnalyzed = new List<TemplateMixin>();
			
			if(templateMixinsBeingAnalyzed.Contains(tmx))
				return false;
			templateMixinsBeingAnalyzed.Add(tmx);

			var tmxTemplate = GetTemplateMixinContent(ctxt, tmx, false);
			
			bool res = false;
			if(tmxTemplate == null)
				ctxt.LogError(tmx.Qualifier, "Mixin qualifier must resolve to a mixin template declaration.");
			else
			{
				bool pop = !ctxt.NodeIsInCurrentScopeHierarchy(tmxTemplate.Definition);
				if(pop)
					ctxt.PushNewScope(tmxTemplate.Definition);
				ctxt.CurrentContext.IntroduceTemplateParameterTypes(tmxTemplate);
				dontHandleTemplateParamsInNodeScan = true;
				res |= DeepScanClass(tmxTemplate, vis);
				if(pop)
					ctxt.Pop();
				else
					ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(tmxTemplate);
			}
			
			templateMixinsBeingAnalyzed.Remove(tmx);
			return res;
		}
		#endregion
	}
}
