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
		
		public virtual IEnumerable<IAbstractSyntaxTree> PrefilterSubnodes(ModulePackage pack, out ModulePackage[] subPackages)
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

		bool breakImmediately { get { return ctxt.Options.HasFlag(ResolutionOptions.StopAfterFirstMatch); } }

		public virtual void IterateThroughScopeLayers(CodeLocation Caret, MemberFilter VisibleMembers = MemberFilter.All)
		{
			if (ctxt.ScopedStatement != null &&
				IterateThroughItemHierarchy(ctxt.ScopedStatement, Caret, VisibleMembers) &&
					(ctxt.Options.HasFlag(ResolutionOptions.StopAfterFirstOverloads) ||
					ctxt.Options.HasFlag(ResolutionOptions.StopAfterFirstMatch)))
				return;

			if(ctxt.ScopedBlock != null && 
			   ScanBlockUpward(ctxt.ScopedBlock, Caret, VisibleMembers))
				return;
			
			// On the root level, add __ctfe variable
			if (CanAddMemberOfType(VisibleMembers, __ctfe) && HandleItem(__ctfe) && breakImmediately)
				return;
			
			// and all root modules/packages
			var nameStubs = new List<string>();
			if(ctxt.ParseCache != null)
				foreach(var pc in ctxt.ParseCache)
				{
					ModulePackage[] packs;
					var mods = PrefilterSubnodes(pc.Root, out packs);
					
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
		}
		
		bool ScanBlockUpward(IBlockNode curScope, CodeLocation Caret, MemberFilter VisibleMembers)
		{
			bool breakOnNextScope = false;

			// 2)
			do
			{
				if(ScanBlock(curScope, Caret, VisibleMembers, ref breakOnNextScope))
					return true;
				
				curScope = curScope.Parent as IBlockNode;
			}
			while (curScope != null);
			
			return breakOnNextScope;
		}

		protected bool ScanBlock(IBlockNode curScope, CodeLocation Caret, MemberFilter VisibleMembers, ref bool breakOnNextScope)
		{
			if (curScope is DClassLike)
			{
				if (DeepScanClass(curScope as DClassLike, VisibleMembers, ref breakOnNextScope))
					return true;
			}
			else if (curScope is DMethod)
			{
				var dm = curScope as DMethod;

				// Add 'out' variable if typing in the out test block currently
				if (dm.OutResultVariable != null && dm.Out != null && dm.GetSubBlockAt(Caret) == dm.Out &&
					(breakOnNextScope = HandleItem(new DVariable // Create pseudo-variable
						{
							Name = dm.OutResultVariable.Id as string,
							NameLocation = dm.OutResultVariable.Location,
							Type = dm.Type, // TODO: What to do on auto functions?
							Parent = dm,
							Location = dm.OutResultVariable.Location,
							EndLocation = dm.OutResultVariable.EndLocation,
						})) &&
						breakImmediately)
					return true;

				if (((VisibleMembers & MemberFilter.Variables) == MemberFilter.Variables) &&
					(breakOnNextScope = HandleItems(dm.Parameters)) &&
					breakImmediately)
					return true;

				if (dm.TemplateParameters != null &&
					(breakOnNextScope = HandleItems(dm.TemplateParameterNodes as IEnumerable<INode>)) &&
					breakImmediately)
					return true;

				// The method's declaration children are handled above already via BlockStatement.GetItemHierarchy().
				// except AdditionalChildren:
				foreach (var ch in dm.AdditionalChildren)
					if (CanAddMemberOfType(VisibleMembers, ch) &&
						(breakOnNextScope = HandleItem(ch) && breakImmediately))
						return true;

				// If the method is a nested method,
				// this method won't be 'linked' to the parent statement tree directly - 
				// so, we've to gather the parent method and add its locals to the return list
				if (dm.Parent is DMethod)
				{
					var nestedBlock = (dm.Parent as DMethod).GetSubBlockAt(Caret);

					// Search for the deepest statement scope and add all declarations done in the entire hierarchy
					if (nestedBlock != null &&
						(breakOnNextScope = IterateThroughItemHierarchy(nestedBlock.SearchStatementDeeply(Caret), Caret, VisibleMembers)) &&
						breakImmediately)
						return true;
				}
			}
			else if(scanChildren(curScope as DBlockNode, VisibleMembers, ref breakOnNextScope))
					return true;

			return breakOnNextScope && ((ctxt.Options & ResolutionOptions.StopAfterFirstOverloads) == ResolutionOptions.StopAfterFirstOverloads);
		}
		
		
		bool DeepScanClass(DClassLike cls, MemberFilter VisibleMembers, ref bool breakOnNextScope)
		{
			return DeepScanClass(new ClassType(cls, null, null), VisibleMembers, ref breakOnNextScope, true);
		}
		
		protected bool DeepScanClass(UserDefinedType udt, MemberFilter vis, ref bool breakOnNextScope, bool resolveBaseClassIfRequired = false)
		{
			bool isBase = false;
			
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
				if(scanChildren(udt.Definition as DBlockNode, vis, ref breakOnNextScope, false, isBase, false, takeStaticChildrenOnly))
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
		
		bool scanChildren(DBlockNode curScope, 
		                  MemberFilter VisibleMembers, 
		                  ref bool breakOnNextScope,
		                  bool publicImports = false,
		                  bool isBaseClass = false,
		                  bool isMixinAst = false,
		                 bool takeStaticChildrenOnly = false)
		{
			if (!dontHandleTemplateParamsInNodeScan){
				if(curScope.TemplateParameters != null){
					var t = ctxt.ScopedBlock;
					while(t != null)
					{
						if(t == curScope)
						{
							if((breakOnNextScope = HandleItems(curScope.TemplateParameterNodes as IEnumerable<INode>)) &&
				    			breakImmediately)
								return true;
							break;
						}
						t = t.Parent as IBlockNode;
					}
				}
			}
			else
				dontHandleTemplateParamsInNodeScan = false;
			
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
							if(!CheckForProtectedAttribute(dn,ctxt.ScopedBlock))
								continue;
						}
						else
							continue;
					}

					// Add anonymous enums' items
					if (dn is DEnum && string.IsNullOrEmpty(dn.Name) && CanAddMemberOfType(VisibleMembers, dn))
					{
						var ch2 = PrefilterSubnodes(dn as DEnum);
						if (ch2 != null && (breakOnNextScope = HandleItems(ch2) && breakImmediately))
							return true;
						continue;
					}

					var dm3 = dn as DMethod; // Only show normal & delegate methods
					if (!CanAddMemberOfType(VisibleMembers, n) ||
						(dm3 != null && !(dm3.SpecialType == DMethod.MethodType.Normal || dm3.SpecialType == DMethod.MethodType.Delegate || dm3.Name != null)))
						continue;

					if ((breakOnNextScope = HandleItem(n)) && breakImmediately)
						return true;
				}
			
			return (breakOnNextScope = HandleDBlockNode(curScope, VisibleMembers, publicImports)) && breakImmediately;
		}
		
		static bool IsConstOrStatic(DNode dn)
		{
			return dn != null && (dn.IsStatic || ((dn is DVariable) && (dn as DVariable).IsConst));
		}

		static bool CanShowMember(DNode dn, IBlockNode scope)
		{
			if (dn.ContainsAttribute(DTokens.Private))
				return dn.NodeRoot == scope.NodeRoot;
			else if (dn.ContainsAttribute(DTokens.Package))
				return dn.NodeRoot is IAbstractSyntaxTree &&
					ModuleNameHelper.ExtractPackageName((dn.NodeRoot as IAbstractSyntaxTree).ModuleName) ==
						ModuleNameHelper.ExtractPackageName((scope.NodeRoot as IAbstractSyntaxTree).ModuleName);
			else if(dn.ContainsAttribute(DTokens.Protected))
			{
				while(scope!=null)
				{
					if(dn == scope || dn.Parent == scope)
						return true;
					scope = scope.Parent as IBlockNode;
				}
				return false;
			}
			return true;
		}
		
		static bool CheckForProtectedAttribute(DNode dn, IBlockNode scope)
		{
			if(!dn.ContainsAttribute(DTokens.Protected))
				return true;
			
			while(scope!=null)
			{
				if(dn == scope || dn.Parent == scope)
					return true;
				scope = scope.Parent as IBlockNode;
			}
			return false;
		}

		static bool CanAddMemberOfType(MemberFilter vis, INode n)
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
				return (vis & MemberFilter.Types) == MemberFilter.Types;

			else if (n is DEnum)
			{
				var d = n as DEnum;

				// Only show enums if a) they're named and types are allowed or b) variables are allowed
				return (d.IsAnonymous ? false : vis.HasFlag(MemberFilter.Types)) ||
					vis.HasFlag(MemberFilter.Variables);
			}
			else if(n is NamedTemplateMixinNode)
				return (vis & (MemberFilter.Variables | MemberFilter.Types)) == (MemberFilter.Variables | MemberFilter.Types);
			
			return false;
		}

		/// <summary>
		/// Walks up the statement scope hierarchy and enlists all declarations that have been made BEFORE the caret position. 
		/// (If CodeLocation.Empty given, this parameter will be ignored)
		/// </summary>
		/// <returns>True if scan shall stop, false if not</returns>
		bool IterateThroughItemHierarchy(IStatement Statement, CodeLocation Caret, MemberFilter VisibleMembers)
		{
			// To a prevent double entry of the same declaration, skip a most scoped declaration first
			if (Statement is DeclarationStatement)
				Statement = Statement.Parent;

			while (Statement != null)
			{
				if (HandleSingleStatement(Statement, Caret, VisibleMembers))
					return true;

				Statement = Statement.Parent;
			}

			return false;
		}

		bool HandleSingleStatement(IStatement Statement, CodeLocation Caret, MemberFilter VisibleMembers)
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
					if (r is TemplateIntermediateType)
					{
						var tr = (TemplateIntermediateType)r;
						var dc = tr.Definition as DClassLike;

						bool brk = false;
						if (DeepScanClass(dc, VisibleMembers, ref brk) || brk)
							return true;
					}
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
							return HandleSingleStatement(sc.ScopedStatement, Caret, VisibleMembers);
						else if (sc.ElseStatement != null)
							return HandleSingleStatement(sc.ElseStatement, Caret, VisibleMembers);
					}
					
					var ss = s as StaticStatement;
					if(ss==null || !ctxt.CurrentContext.MatchesDeclarationEnvironment(ss.Attributes))
						continue;
					
					if (s is ImportStatement)
					{
						// Selective imports were handled in the upper section already!

						var impStmt = (ImportStatement)s;

						foreach (var imp in impStmt.Imports)
							if (string.IsNullOrEmpty(imp.ModuleAlias))
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
								if (HandleItem(d)) //TODO: Handle visibility?
									return true;

						if (impStmt!=null)
						{
							foreach (var imp in impStmt.Imports)
								if (string.IsNullOrEmpty(imp.ModuleAlias))
									if (HandleNonAliasedImport(imp, VisibleMembers))
										return true;
						}
					}
					else if(stmt is MixinStatement)
					{
						if(MatchesCompilationEnv(stmt) && HandleMixin(stmt as MixinStatement,true,VisibleMembers))
							return true;
					}
					else if(stmt is TemplateMixin)
					{
						if(MatchesCompilationEnv(stmt) && HandleUnnamedTemplateMixin(stmt as TemplateMixin, true, VisibleMembers))
							return true;
					}
				}
			}

			// Every module imports 'object' implicitly
			if (dbn is IAbstractSyntaxTree && !takePublicImportsOnly)
				if (HandleNonAliasedImport(_objectImport, VisibleMembers))
					return true;

			return false;
		}
		
		bool MatchesCompilationEnv(StaticStatement ss)
		{
			return ss.Attributes == null || ctxt.CurrentContext.MatchesDeclarationEnvironment(ss.Attributes);
		}

		bool HandleNonAliasedImport(ImportStatement.Import imp, MemberFilter VisibleMembers)
		{
			if (imp == null || imp.ModuleIdentifier == null)
				return false;

			var thisModuleName = (ctxt.ScopedBlock != null && ctxt.ScopedBlock.NodeRoot is IAbstractSyntaxTree) ? ((IAbstractSyntaxTree)ctxt.ScopedBlock.NodeRoot).ModuleName : string.Empty;
			
			if(string.IsNullOrEmpty(thisModuleName))
				return false;
			
			var moduleName = imp.ModuleIdentifier.ToString();
			
			List<string> seenModules = null;

			if (!scannedModules.TryGetValue(thisModuleName, out seenModules))
				seenModules = scannedModules[thisModuleName] = new List<string>();
			else if (seenModules.Contains(moduleName))
				return false;
			seenModules.Add(moduleName);

			var scAst = ctxt.ScopedBlock == null ? null : ctxt.ScopedBlock.NodeRoot as IAbstractSyntaxTree;
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
			bool _u = false;
			return scanChildren(module, VisibleMembers, ref _u, true);
		}
		#endregion
		
		#region Mixins
		/// <summary>
		/// Evaluates the literal given as expression and tries to parse it as a string.
		/// Important: Assumes all its compilation conditions to be checked already!
		/// </summary>
		bool HandleMixin(MixinStatement mx, bool parseDeclDefs, MemberFilter vis)
		{
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
					bool _u = false;
					return scanChildren(ast, vis, ref _u, isMixinAst:true);
				}
			}
			else // => MixinStatement
			{
				var bs = MixinAnalysis.ParseMixinStatement(mx, ctxt);
				
				// As above, disregard the caret position because 1) caret and parsed code do not match 
				// and 2) the caret must be located somewhere after the mixin statement's end
				if(bs!=null){
					return IterateThroughItemHierarchy(bs, CodeLocation.Empty, vis);
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
				res |= DeepScanClass(tmxTemplate, vis, ref res);
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
