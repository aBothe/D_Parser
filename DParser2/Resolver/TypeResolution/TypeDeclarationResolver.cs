using System;
using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver.ExpressionSemantics;
using System.Collections.ObjectModel;

namespace D_Parser.Resolver.TypeResolution
{
	public static class TypeDeclarationResolver
	{
		/// <summary>
		/// Used for searching further identifier list parts.
		/// 
		/// a.b -- nextIdentifier would be 'b' whereas <param name="resultBases">resultBases</param> contained the resolution result for 'a'
		/// </summary>
		public static List<AbstractType> ResolveFurtherTypeIdentifier(int nextIdentifierHash,
			AbstractType resultBases,
			ResolutionContext ctxt,
			ISyntaxRegion typeIdObject = null, bool ufcsItem = true)
		{
			MemberSymbol statProp;
			if ((resultBases = DResolver.StripMemberSymbols(resultBases)) == null)
				return null;

			var r = new List<AbstractType>();

			foreach(var b_ in AmbiguousType.TryDissolve(resultBases))
			{
				var b = TryPostDeduceAliasDefinition(b_, typeIdObject, ctxt);

				if (b is PointerType)
					b = (b as DerivedDataType).Base;

				if (b is UserDefinedType)
				{
					var udt = b as UserDefinedType;

					using (b is MixinTemplateType || udt is TemplateType ? ctxt.Push(udt) : null)
					{
						r.AddRange(SingleNodeNameScan.SearchChildrenAndResolve(ctxt, udt, nextIdentifierHash, typeIdObject));

						statProp = StaticProperties.TryEvalPropertyType(ctxt, b, nextIdentifierHash);
						if (statProp != null)
							r.Add(statProp);

						// go the opDispatch way if possible - http://dlang.org/operatoroverloading.html#Dispatch
						if (r.Count == 0 && nextIdentifierHash != OpDispatchResolution.opDispatchId)
							r.AddRange(OpDispatchResolution.TryResolveFurtherIdViaOpDispatch(ctxt, nextIdentifierHash, udt, typeIdObject));

						if (r.Count == 0 && ufcsItem)
							r.AddRange(UFCSResolver.TryResolveUFCS(b, nextIdentifierHash, ctxt.ScopedBlock != udt.Definition && typeIdObject != null ? typeIdObject.Location : ctxt.ScopedBlock.BlockStartLocation, ctxt, typeIdObject));
					}
				}
				else if (b is PackageSymbol)
				{
					var pack = (b as PackageSymbol).Package;

					var accessedModule = pack.GetModule(nextIdentifierHash);
					if (accessedModule != null)
						r.Add(new ModuleSymbol(accessedModule, b as PackageSymbol));
					else if ((pack = pack.GetPackage(nextIdentifierHash)) != null)
						r.Add(new PackageSymbol(pack));
				}
				else if (b is ModuleSymbol)
					r.AddRange(SingleNodeNameScan.SearchChildrenAndResolve(ctxt, b as ModuleSymbol, nextIdentifierHash, typeIdObject));
				else
				{
					statProp = StaticProperties.TryEvalPropertyType(ctxt, b, nextIdentifierHash);
					if (statProp != null)
						r.Add(statProp);

					if(r.Count == 0 && ufcsItem) // Only if there hasn't been a result yet?
						r.AddRange(UFCSResolver.TryResolveUFCS (b, nextIdentifierHash, typeIdObject != null ? typeIdObject.Location : ctxt.ScopedBlock.BlockStartLocation, ctxt, typeIdObject));
				}
			}

			return r;
		}

		public static AbstractType ResolveSingle(ITypeDeclaration declaration, ResolutionContext ctxt, bool filterTemplates = true)
		{
			return declaration == null ? null : declaration.Accept (new SingleResolverVisitor (ctxt, filterTemplates));
		}

		public static AbstractType ResolveTemplateParameter(ResolutionContext ctxt, TemplateParameter.Node tpn)
		{
			TemplateParameterSymbol tpnBase;

			if (ctxt.GetTemplateParam(tpn.NameHash, out tpnBase) && tpnBase.Parameter == tpn.TemplateParameter)
				return tpnBase;

			AbstractType baseType;
			//TODO: What if there are like nested default constructs like (T = U*, U = int) ?
			var ttp = tpn.TemplateParameter as TemplateTypeParameter;
			if (ttp != null && (ttp.Default != null || ttp.Specialization != null))
				baseType = ResolveSingle(ttp.Default ?? ttp.Specialization, ctxt);
			else
				baseType = null;

			return new TemplateParameterSymbol(tpn, baseType);
		}


		#region Intermediate methods
		[ThreadStatic]
		static Dictionary<INode, int> stackCalls;

		[ThreadStatic]
		static Stack<ISyntaxRegion> aliasDeductionStack = new Stack<ISyntaxRegion>();

		internal static AbstractType TryPostDeduceAliasDefinition(AbstractType b, ISyntaxRegion typeBase, ResolutionContext ctxt)
		{
			if (typeBase != null &&
				b != null && 
				b.Tag<AliasTag>(AliasTag.Id) != null && 
				(ctxt.Options & ResolutionOptions.DontResolveAliases) == 0)
			{
				if (aliasDeductionStack == null)
					aliasDeductionStack = new Stack<ISyntaxRegion>();
				else if (aliasDeductionStack.Contains(typeBase))
					return b;
				aliasDeductionStack.Push(typeBase);

				var bases = AmbiguousType.TryDissolve(b);

				//TODO: Declare alias-level context? 

				if (typeBase is TemplateInstanceExpression)
					b = AmbiguousType.Get(TemplateInstanceHandler.DeduceParamsAndFilterOverloads(bases, typeBase as TemplateInstanceExpression, ctxt, false));
				else 
					b = AmbiguousType.Get(TemplateInstanceHandler.DeduceParamsAndFilterOverloads(bases, Enumerable.Empty<ISemantic>(), false, ctxt));

				aliasDeductionStack.Pop();
			}

			return b;
		}

		public class AliasTag
		{
			public const string Id = "AliasTag";
			public DVariable aliasDefinition;
			public ISyntaxRegion typeBase;
		}

		struct NodeMatchHandleVisitor : NodeVisitor<AbstractType>
		{
			public ResolutionContext ctxt;
			public bool CanResolveBase(INode m)
			{
				int stkC;
				stackCalls.TryGetValue(m, out stkC);
				return ((ctxt.Options & ResolutionOptions.DontResolveBaseTypes) != ResolutionOptions.DontResolveBaseTypes) &&
						stkC < 4 && 
						(!(m.Type is IdentifierDeclaration) || (m.Type as IdentifierDeclaration).IdHash != m.NameHash || m.Type.InnerDeclaration != null); // pretty rough and incomplete SO prevention hack
			}
			public ISyntaxRegion typeBase;
			public AbstractType resultBase;

			[System.Diagnostics.DebuggerStepThrough]
			public NodeMatchHandleVisitor(ResolutionContext ctxt, ISyntaxRegion typeBase, AbstractType resultBase)
			{
				this.ctxt = ctxt;
				this.typeBase = typeBase;
				this.resultBase = resultBase;
			}


			public AbstractType Visit(DEnumValue n)
			{
				return new MemberSymbol(n, resultBase ?? HandleNodeMatch(n.Parent, ctxt));
			}

			AbstractType VisitAliasDefinition(DVariable v)
			{
				AbstractType bt;

				// Ignore explicitly set resolution restrictions - aliases must be resolved!
				if (!CanResolveBase(v) &&
					(ctxt.Options & ResolutionOptions.DontResolveBaseTypes) == 0)
					return new AliasedType(v, null);

				// Is it really that easy?
				var optBackup = ctxt.CurrentContext.ContextDependentOptions;
				ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;
				if (v.Type is IdentifierDeclaration)
					ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.NoTemplateParameterDeduction;

				bt = TypeDeclarationResolver.ResolveSingle(v.Type, ctxt);

				ctxt.CurrentContext.ContextDependentOptions = optBackup;

				// For auto variables, use the initializer to get its type
				if (bt == null && v.Initializer != null)
					bt = DResolver.StripMemberSymbols(ExpressionTypeEvaluation.EvaluateType(v.Initializer, ctxt));

				// Check if inside an foreach statement header
				if (bt == null)
					bt = GetForeachIteratorType(v);

				if (bt == null)
					return new AliasedType(v, null);

				bt.Tag(AliasTag.Id, new AliasTag { aliasDefinition = v, typeBase = typeBase });
				return bt;
			}

			public AbstractType VisitDVariable(DVariable variable)
			{
				if (variable.IsAlias)
					return VisitAliasDefinition(variable);
				AbstractType bt;

				if (CanResolveBase(variable))
				{
					bt = TypeDeclarationResolver.ResolveSingle(variable.Type, ctxt);

					// For auto variables, use the initializer to get its type
					if (bt == null && variable.Initializer != null)
						bt = DResolver.StripMemberSymbols(ExpressionTypeEvaluation.EvaluateType(variable.Initializer, ctxt));

					// Check if inside an foreach statement header
					if (bt == null)
						bt = GetForeachIteratorType(variable);

					if (bt != null && variable.Attributes != null && variable.Attributes.Count > 0) {
						var variableModifiers = variable.Attributes.FindAll ((DAttribute obj) => obj is Modifier).Select ((arg) => ((Modifier)arg).Token).ToArray();
						if (variableModifiers.Length > 0) {
							bt = ResolvedTypeCloner.Clone(bt);
							if(bt.HasModifiers) {
								bt.Modifiers = bt.Modifiers.Union (variableModifiers).ToArray();
							} else {
								bt.Modifiers = variableModifiers;
							}
						}
					}
				}
				else
					bt = null;

				return new MemberSymbol(variable, bt, variable.Initializer != null ? ctxt.DeducedTypesInHierarchy : null);
			}

			/// <summary>
			/// string[] s;
			/// 
			/// foreach(i;s)
			/// {
			///		// i is of type 'string'
			///		writeln(i);
			/// }
			/// </summary>
			public AbstractType GetForeachIteratorType(DVariable i)
			{
				var r = new List<AbstractType>();
				var curMethod = ctxt.ScopedBlock as DMethod;
				var loc = ctxt.CurrentContext.Caret;
				loc = new CodeLocation(loc.Column-1, loc.Line); // SearchStmtDeeplyAt only checks '<' EndLocation, we may need to have '<=' though due to completion offsets.
				var curStmt = curMethod != null ? ASTSearchHelper.SearchStatementDeeplyAt(curMethod.GetSubBlockAt(ctxt.CurrentContext.Caret), loc) : null;

				if (curStmt == null)
					return null;

				bool init = true;
				// Walk up statement hierarchy -- note that foreach loops can be nested
				while (curStmt != null)
				{
					if (init)
						init = false;
					else
						curStmt = curStmt.Parent;

					if (curStmt is ForeachStatement)
					{
						var fe = (ForeachStatement)curStmt;

						if (fe.ForeachTypeList == null)
							continue;

						// If the searched variable is declared in the header
						int iteratorIndex = -1;

						for (int j = 0; j < fe.ForeachTypeList.Length; j++)
							if (fe.ForeachTypeList[j] == i)
							{
								iteratorIndex = j;
								break;
							}

						if (iteratorIndex == -1)
							continue;

						bool keyIsSearched = iteratorIndex == 0 && fe.ForeachTypeList.Length > 1;


						// foreach(var k, var v; 0 .. 9)
						if (keyIsSearched && fe.IsRangeStatement)
						{
							// -- it's static type int, of course(?)
							return new PrimitiveType(DTokens.Int);
						}

						if (fe.Aggregate == null)
							return null;

						var aggregateType = ExpressionTypeEvaluation.EvaluateType(fe.Aggregate, ctxt);

						aggregateType = DResolver.StripMemberSymbols(aggregateType);

						if (aggregateType == null)
							return null;

						// The most common way to do a foreach
						if (aggregateType is AssocArrayType)
						{
							var ar = (AssocArrayType)aggregateType;

							return keyIsSearched ? ar.KeyType : ar.ValueType;
						}
						else if (aggregateType is PointerType)
							return keyIsSearched ? TypeDeclarationResolver.ResolveSingle(new IdentifierDeclaration("size_t"), ctxt, false /* Generally, size_t isn't templated or such..so for performance, step through additional filtering */) : (aggregateType as PointerType).Base;
						else if (aggregateType is UserDefinedType)
						{
							var tr = (UserDefinedType)aggregateType;

							if (keyIsSearched || !(tr.Definition is IBlockNode))
								continue;

							bool foundIterPropertyMatch = false;
							#region Foreach over Structs and Classes with Ranges

							// Enlist all 'back'/'front' members
							var iterPropertyTypes = new List<AbstractType>();

							foreach (var n in (IBlockNode)tr.Definition)
								if (fe.IsReverse ? n.Name == "back" : n.Name == "front")
									iterPropertyTypes.Add(HandleNodeMatch(n, ctxt));

							foreach (var iterPropType in iterPropertyTypes)
								if (iterPropType is MemberSymbol)
								{
									foundIterPropertyMatch = true;

									var itp = (MemberSymbol)iterPropType;

									// Only take non-parameterized methods
									if (itp.Definition is DMethod && ((DMethod)itp.Definition).Parameters.Count != 0)
										continue;

									// Handle its base type [return type] as iterator type
									if (itp.Base != null)
										r.Add(itp.Base);

									foundIterPropertyMatch = true;
								}

							if (foundIterPropertyMatch)
								continue;
							#endregion

							#region Foreach over Structs and Classes with opApply
							iterPropertyTypes.Clear();
							r.Clear();

							foreach (var n in (IBlockNode)tr.Definition)
								if (n is DMethod &&
									(fe.IsReverse ? n.Name == "opApplyReverse" : n.Name == "opApply"))
									iterPropertyTypes.Add(HandleNodeMatch(n, ctxt));

							foreach (var iterPropertyType in iterPropertyTypes)
								if (iterPropertyType is MemberSymbol)
								{
									var mr = (MemberSymbol)iterPropertyType;
									var dm = mr.Definition as DMethod;

									if (dm == null || dm.Parameters.Count != 1)
										continue;

									var dg = dm.Parameters[0].Type as DelegateDeclaration;

									if (dg == null || dg.Parameters.Count != fe.ForeachTypeList.Length)
										continue;

									var paramType = ResolveSingle(dg.Parameters[iteratorIndex].Type, ctxt);

									if (paramType != null)
										r.Add(paramType);
								}
							#endregion
						}

						return AmbiguousType.Get(r);
					}
				}

				return null;
			}

			/// <summary>
			/// Add 'superior' template parameters to the current symbol because 
			/// the parameters might be re-used in the nested class.
			/// Only pays attention to those parameter symbols that are located in the current resolution scope's AST hierarchy.
			/// </summary>
			IEnumerable<TemplateParameterSymbol> GetInvisibleTypeParameters(DNode n)
			{
				ContextFrame prev = null;
				foreach (var cf in ctxt.ContextStack) {
					// Only stay in the same AST hierarchy
					if (prev != null && cf.ScopedBlock != null && cf.ScopedBlock.Parent != prev.ScopedBlock)
						yield break;
					prev = cf;

					foreach (var kv in cf.DeducedTemplateParameters)
						if (!n.ContainsTemplateParameter (kv.Value.Parameter))
							yield return kv.Value;
				}
			}

			public AbstractType Visit(EponymousTemplate ep)
			{
				return new EponymousTemplateType(ep, new ReadOnlyCollection<TemplateParameterSymbol>(new List<TemplateParameterSymbol>(GetInvisibleTypeParameters(ep))));
			}

			public AbstractType Visit(DMethod m)
			{
				return new MemberSymbol(m, CanResolveBase(m) ? GetMethodReturnType(m, ctxt) : null, GetInvisibleTypeParameters(m));
			}

			public AbstractType Visit(DClassLike dc)
			{
				var invisibleTypeParams = GetInvisibleTypeParameters(dc);

				switch (dc.ClassType)
				{
					case DTokens.Struct:
						return new StructType(dc, invisibleTypeParams);

					case DTokens.Union:
						return new UnionType(dc, invisibleTypeParams);

					case DTokens.Interface:
					case DTokens.Class:
					return DResolver.ResolveClassOrInterface(dc, ctxt, typeBase, false, invisibleTypeParams.ToList());

					case DTokens.Template:
						if (dc.ContainsAnyAttribute(DTokens.Mixin))
							return new MixinTemplateType(dc, invisibleTypeParams);
						return new TemplateType(dc, invisibleTypeParams);

					default:
						ctxt.LogError(new NothingFoundError(dc, "Unknown type (" + DTokens.GetTokenString(dc.ClassType) + ")"));
						return null;
				}
			}

			public AbstractType Visit(DEnum de)
			{
				AbstractType bt;

				if (de.Type == null)
					bt = new PrimitiveType(DTokens.Int);
				else
				{
					using(ctxt.Push(de.Parent))
						bt = TypeDeclarationResolver.ResolveSingle(de.Type, ctxt);
				}

				return new EnumType(de, bt);
			}

			public AbstractType Visit(DModule mod)
			{
				if (typeBase != null && typeBase.ToString() != mod.ModuleName)
				{
					var pack = ctxt.ParseCache.LookupPackage(ctxt.ScopedBlock, typeBase.ToString()).FirstOrDefault();
					if (pack != null)
						return new PackageSymbol(pack);
				}

				return new ModuleSymbol(mod);
			}

			public AbstractType Visit(DBlockNode dBlockNode)
			{
				throw new NotImplementedException();
			}

			public AbstractType Visit(TemplateParameter.Node tpn)
			{
				return CanResolveBase(tpn) ? ResolveTemplateParameter(ctxt, tpn) : new TemplateParameterSymbol(tpn, null);
			}

			public AbstractType Visit(NamedTemplateMixinNode n)
			{
				return VisitDVariable(n as DVariable);
			}

			// Only import symbol aliases are allowed to search in the parse cache
			public AbstractType Visit(ImportSymbolNode importSymbolNode)
			{
				return VisitAliasDefinition(importSymbolNode);
                /*
				AbstractType ret = null;

				var modAlias = importSymbolNode is ModuleAliasNode;
				if (modAlias ? importSymbolNode.Type != null : importSymbolNode.Type.InnerDeclaration != null)
				{
					var mods = new List<DModule>();
					var td = modAlias ? importSymbolNode.Type : importSymbolNode.Type.InnerDeclaration;
					foreach (var mod in ctxt.ParseCache.LookupModuleName(importSymbolNode, td.ToString()))
						mods.Add(mod);
					if (mods.Count == 0)
						ctxt.LogError(new NothingFoundError(importSymbolNode.Type));
					else
						if (mods.Count > 1)
						{
							var m__ = new List<ISemantic>();
							foreach (var mod in mods)
								m__.Add(new ModuleSymbol(mod));
							ctxt.LogError(new AmbiguityError(importSymbolNode.Type, m__));
						}
					var bt = mods.Count != 0 ? (AbstractType)new ModuleSymbol(mods[0]) : null;
					//TODO: Is this correct behaviour?
					if (!modAlias)
					{
						bt = AmbiguousType.Get(ResolveFurtherTypeIdentifier(importSymbolNode.Type.ToString(false).GetHashCode(), bt, ctxt, importSymbolNode.Type));
					}
					ret = new AliasedType(importSymbolNode, bt);
				}
				return ret;
                */
			}

			public AbstractType Visit(ModuleAliasNode moduleAliasNode)
			{
				return Visit(moduleAliasNode as ImportSymbolNode);
			}

			public AbstractType Visit(ImportSymbolAlias importSymbolAlias)
			{
				return Visit(importSymbolAlias as ImportSymbolNode);
			}

			#region Attributes etc.
			public AbstractType VisitAttribute(Modifier attr)
			{
				throw new NotImplementedException();
			}

			public AbstractType VisitAttribute(DeprecatedAttribute a)
			{
				throw new NotImplementedException();
			}

			public AbstractType VisitAttribute(PragmaAttribute attr)
			{
				throw new NotImplementedException();
			}

			public AbstractType VisitAttribute(BuiltInAtAttribute a)
			{
				throw new NotImplementedException();
			}

			public AbstractType VisitAttribute(UserDeclarationAttribute a)
			{
				throw new NotImplementedException();
			}

			public AbstractType VisitAttribute(VersionCondition a)
			{
				throw new NotImplementedException();
			}

			public AbstractType VisitAttribute(DebugCondition a)
			{
				throw new NotImplementedException();
			}

			public AbstractType VisitAttribute(StaticIfCondition a)
			{
				throw new NotImplementedException();
			}

			public AbstractType VisitAttribute(NegatedDeclarationCondition a)
			{
				throw new NotImplementedException();
			}
			#endregion
		}

		/// <summary>
		/// The variable's or method's base type will be resolved (if auto type, the intializer's type will be taken).
		/// A class' base class will be searched.
		/// etc..
		/// </summary>
		public static AbstractType HandleNodeMatch(
			INode m,
			ResolutionContext ctxt,
			AbstractType resultBase = null,
			ISyntaxRegion typeBase = null)
		{
			// See https://github.com/aBothe/Mono-D/issues/161
			int stkC;

			if (stackCalls == null)
			{
				stackCalls = new Dictionary<INode, int>();
				stackCalls[m] = 1;
			}
			else
				stackCalls[m] = stackCalls.TryGetValue(m, out stkC) ? ++stkC : 1;

			/*
			 * Pushing a new scope is only required if current scope cannot be found in the handled node's hierarchy.
			 * Edit: No, it is required nearly every time because of nested type declarations - then, we do need the 
			 * current block scope.
			 */
			var options = ctxt.CurrentContext.ContextDependentOptions;
			var applyOptions = ctxt.ScopedBlockIsInNodeHierarchy(m);
			IDisposable disp;
			CodeLocation loc = typeBase != null ? typeBase.Location : m.Location;

			if (resultBase is DSymbol)
				disp = ctxt.Push (resultBase as DSymbol, loc);
			else
				disp = ctxt.Push (m, loc);

			AbstractType ret;
			using (disp)
			{
				if (applyOptions)
					ctxt.CurrentContext.ContextDependentOptions = options;

				ret = m.Accept(new NodeMatchHandleVisitor(ctxt, typeBase, resultBase));
			}

			stackCalls.TryGetValue(m, out stkC);
			if (stkC == 1)
				stackCalls.Remove(m);
			else
				stackCalls[m] = stkC-1;

			return ret;
		}

		public static List<AbstractType> HandleNodeMatches(
			IEnumerable<INode> matches,
			ResolutionContext ctxt,
			AbstractType resultBase = null,
			ISyntaxRegion typeDeclaration = null)
		{
			var rl = new List<AbstractType>();

			// Abbreviate a foreach-loop + List alloc
			var ll = matches as IList<INode>;
			if (ll != null && ll.Count == 1)
			{
				var returnType = ll[0] != null ? HandleNodeMatch(ll[0], ctxt, resultBase, typeDeclaration) : null;
				if (returnType != null)
					rl.Add(returnType);
				return rl;
			}

			if (matches == null)
				return rl;

			foreach (var m in matches)
			{
				if (m == null)
					continue;

				var res = HandleNodeMatch(m, ctxt, resultBase, typeDeclaration);
				if (res != null)
					rl.Add(res);
			}

			return rl;
		}

		public static AbstractType GetMethodReturnType(DelegateType dg, ResolutionContext ctxt)
		{
			if (dg == null || ctxt == null)
				return null;

			if (dg.IsFunctionLiteral)
				return GetMethodReturnType(((FunctionLiteral)dg.delegateTypeBase).AnonymousMethod, ctxt);
			
			return ResolveSingle(((DelegateDeclaration)dg.delegateTypeBase).ReturnType, ctxt);
		}

		public static AbstractType GetMethodReturnType(DMethod method, ResolutionContext ctxt)
		{
			AbstractType returnType;

			if ((ctxt.Options & ResolutionOptions.DontResolveBaseTypes) == ResolutionOptions.DontResolveBaseTypes)
				return null;

			/*
			 * If a method's type equals null, assume that it's an 'auto' function..
			 * 1) Search for a return statement
			 * 2) Resolve the returned expression
			 * 3) Use that one as the method's type
			 */
			if (method.Type != null)
			{
				using (ctxt.Push(method)) //FIXME: Is it legal to explicitly return a nested type?
					returnType = TypeDeclarationResolver.ResolveSingle(method.Type, ctxt);

				if (returnType != null)
					returnType.NonStaticAccess = true;

				return returnType;
			}
			else if (method.Body != null)
			{
				ReturnStatement returnStmt = null;
				var list = new List<IStatement> { method.Body };
				var list2 = new List<IStatement>();

				bool foundMatch = false;
				while (!foundMatch && list.Count > 0)
				{
					foreach (var stmt in list)
					{
						if (stmt is ReturnStatement)
						{
							returnStmt = stmt as ReturnStatement;

							var te = returnStmt.ReturnExpression as TokenExpression;
							if (te == null || te.Token != DTokens.Null)
							{
								foundMatch = true;
								break;
							}
						}

						var statementContainingStatement = stmt as StatementContainingStatement;
						if (statementContainingStatement != null)
							list2.AddRange(statementContainingStatement.SubStatements);
					}

					list = list2;
					list2 = new List<IStatement>();
				}

				if (returnStmt != null && returnStmt.ReturnExpression != null)
				{
					using (ctxt.Push(method, returnStmt.Location, true))
						returnType = DResolver.StripMemberSymbols(ExpressionTypeEvaluation.EvaluateType(returnStmt.ReturnExpression, ctxt));

					if (returnType != null)
						returnType.NonStaticAccess = true;

					return returnType;
				}

				return new PrimitiveType (DTokens.Void);
			}

			return null;
		}
		#endregion
	}
}
