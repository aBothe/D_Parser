using System;
using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.Templates;

namespace D_Parser.Resolver.TypeResolution
{
	public class TypeDeclarationResolver
	{
		public static PrimitiveType Resolve(DTokenDeclaration token)
		{
			var tk = (token as DTokenDeclaration).Token;

			if (DTokens.BasicTypes[tk])
				return new PrimitiveType(tk, 0, token);

			return null;
		}

		public static ISemantic[] Convert(IEnumerable<AbstractType> at)
		{
			var l = new List<ISemantic>();

			if (at != null)
				foreach (var t in at)
					l.Add(t);

			return l.ToArray();
		}

		public static AbstractType[] Convert(IEnumerable<ISemantic> at)
		{
			var l = new List<AbstractType>();

			if (at != null)
				foreach (var t in at)
				{
					if (t is AbstractType)
						l.Add((AbstractType)t);
					else if (t is ISymbolValue)
						l.Add(((ISymbolValue)t).RepresentedType);
				}

			return l.ToArray();
		}

		public static AbstractType[] ResolveIdentifier(string id, ResolutionContext ctxt, object idObject, bool ModuleScope = false)
		{
			return ResolveIdentifier (id.GetHashCode (), ctxt, idObject, ModuleScope);
		}

		/// <summary>
		/// Resolves an identifier and returns the definition + its base type.
		/// Does not deduce any template parameters or nor filters out unfitting template specifications!
		/// </summary>
		public static AbstractType[] ResolveIdentifier(int idHash, ResolutionContext ctxt, object idObject, bool ModuleScope = false)
		{
			var loc = idObject is ISyntaxRegion ? ((ISyntaxRegion)idObject).Location : CodeLocation.Empty;

			if (ModuleScope)
				ctxt.PushNewScope(ctxt.ScopedBlock.NodeRoot as DModule);

			// If there are symbols that must be preferred, take them instead of scanning the ast
			else
			{
				TemplateParameterSymbol dedTemplateParam;
				if (ctxt.GetTemplateParam(idHash, out dedTemplateParam))
					return new[] { dedTemplateParam };
			}

			var res = NameScan.SearchAndResolve(ctxt, loc, idHash, idObject);

			if (ModuleScope)
				ctxt.Pop();

			return /*res.Count == 0 ? null :*/ res.ToArray();
		}

		/// <summary>
		/// See <see cref="ResolveIdentifier"/>
		/// </summary>
		public static AbstractType ResolveSingle(string id, ResolutionContext ctxt, object idObject, bool ModuleScope = false)
		{
			var r = ResolveIdentifier(id, ctxt, idObject, ModuleScope);

			ctxt.CheckForSingleResult(r, idObject as ISyntaxRegion);

			return r != null && r.Length != 0 ? r[0] : null;
		}

		/// <summary>
		/// See <see cref="Resolve"/>
		/// </summary>
		public static AbstractType ResolveSingle(IdentifierDeclaration id, ResolutionContext ctxt, AbstractType[] resultBases = null, bool filterForTemplateArgs = true)
		{
			var r = Resolve(id, ctxt, resultBases, filterForTemplateArgs);

			ctxt.CheckForSingleResult(r, id);

			return r != null && r.Length != 0 ? r[0] : null;
		}

		public static AbstractType[] Resolve(IdentifierDeclaration id, ResolutionContext ctxt, AbstractType[] resultBases = null, bool filterForTemplateArgs = true)
		{
			AbstractType[] res;

			if (id.InnerDeclaration == null && resultBases == null)
				res = ResolveIdentifier(id.IdHash, ctxt, id, id.ModuleScoped);
			else
			{
				var rbases = resultBases ?? Resolve(id.InnerDeclaration, ctxt);

				if (rbases == null || rbases.Length == 0)
					return null;

				res = ResolveFurtherTypeIdentifier(id.IdHash, rbases, ctxt, id);
			}

			if (filterForTemplateArgs && (ctxt.Options & ResolutionOptions.NoTemplateParameterDeduction) == 0)
			{
				var l_ = new List<AbstractType>();

				if (res != null)
					foreach (var s in res)
						l_.Add(s);

				return TemplateInstanceHandler.DeduceParamsAndFilterOverloads(l_, null, false, ctxt);
			}
			else
				return res;
		}

		public static AbstractType[] ResolveFurtherTypeIdentifier(string nextIdentifier,
		                                                          IEnumerable<AbstractType> resultBases,
		                                                          ResolutionContext ctxt,
		                                                          object typeIdObject = null)
		{
			return ResolveFurtherTypeIdentifier (nextIdentifier.GetHashCode (), resultBases, ctxt, typeIdObject);
		}

		/// <summary>
		/// Used for searching further identifier list parts.
		/// 
		/// a.b -- nextIdentifier would be 'b' whereas <param name="resultBases">resultBases</param> contained the resolution result for 'a'
		/// </summary>
		public static AbstractType[] ResolveFurtherTypeIdentifier(int nextIdentifierHash,
			IEnumerable<AbstractType> resultBases,
			ResolutionContext ctxt,
			object typeIdObject = null)
		{
			MemberSymbol statProp;
			if ((resultBases = DResolver.StripMemberSymbols(resultBases)) == null)
				return null;

			var r = new List<AbstractType>();

			foreach(var b in resultBases)
			{
				if (b is UserDefinedType)
				{
					var udt = b as UserDefinedType;
					var bn = udt.Definition as IBlockNode;
					
					bool pop = !(b is MixinTemplateType);
					if(!pop)
						ctxt.PushNewScope(bn);
					ctxt.CurrentContext.IntroduceTemplateParameterTypes(udt);

					r.AddRange(SingleNodeNameScan.SearchChildrenAndResolve(ctxt, bn, nextIdentifierHash, typeIdObject));

					List<TemplateParameterSymbol> dedTypes = null;
					foreach (var t in r)
					{
						var ds = t as DSymbol;
						if (ds != null && ds.DeducedTypes == null)
						{
							if (dedTypes == null)
								dedTypes = ctxt.DeducedTypesInHierarchy;

							ds.DeducedTypes = new System.Collections.ObjectModel.ReadOnlyCollection<TemplateParameterSymbol>(dedTypes);
						}
					}

					statProp = StaticProperties.TryEvalPropertyType(ctxt, b, nextIdentifierHash);
					if (statProp != null)
						r.Add(statProp);

					ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(udt);
					if(!pop)
						ctxt.Pop();
				}
				else if (b is PackageSymbol)
				{
					var pack = (b as PackageSymbol).Package;

					var accessedModule = pack.GetModule(nextIdentifierHash);
					if (accessedModule != null)
						r.Add(new ModuleSymbol(accessedModule as DModule, typeIdObject as ISyntaxRegion, b as PackageSymbol));
					else if ((pack = pack.GetPackage(nextIdentifierHash)) != null)
						r.Add(new PackageSymbol(pack, typeIdObject as ISyntaxRegion));
				}
				else if (b is ModuleSymbol)
					r.AddRange(SingleNodeNameScan.SearchChildrenAndResolve(ctxt, (b as ModuleSymbol).Definition, nextIdentifierHash, typeIdObject));
				else
				{
					statProp = StaticProperties.TryEvalPropertyType(ctxt, b, nextIdentifierHash);
					if (statProp != null)
						r.Add(statProp);
				}
				// TODO: Search for UFCS symbols
			}

			return r.Count == 0 ? null : r.ToArray();
		}

		public static AbstractType Resolve(TypeOfDeclaration typeOf, ResolutionContext ctxt)
		{
			// typeof(return)
			if (typeOf.Expression is TokenExpression && (typeOf.Expression as TokenExpression).Token == DTokens.Return)
			{
				var m = HandleNodeMatch(ctxt.ScopedBlock, ctxt, null, typeOf);
				if (m != null)
					return m;
			}
			// typeOf(myInt)  =>  int
			else if (typeOf.Expression != null)
			{
				var wantedTypes = Evaluation.EvaluateType(typeOf.Expression, ctxt);
				return DResolver.StripMemberSymbols(wantedTypes);
			}

			return null;
		}

		public static AbstractType Resolve(MemberFunctionAttributeDecl attrDecl, ResolutionContext ctxt)
		{
			if (attrDecl != null)
			{
				var ret = Resolve(attrDecl.InnerType, ctxt);

				ctxt.CheckForSingleResult(ret, attrDecl.InnerType);

				if (ret != null && ret.Length != 0 && ret[0] != null)
				{
					ret[0].Modifier = attrDecl.Modifier;
					return ret[0];
				}
			}
			return null;
		}

		public static AbstractType ResolveKey(ArrayDecl ad, out int fixedArrayLength, out ISymbolValue keyVal, ResolutionContext ctxt)
		{
			keyVal = null;
			fixedArrayLength = -1;
			AbstractType keyType = null;

			if (ad.KeyExpression != null)
			{
				//TODO: Template instance expressions?
				var id_x = ad.KeyExpression as IdentifierExpression;
				if (id_x != null && id_x.IsIdentifier)
				{
					var id = new IdentifierDeclaration((string)id_x.Value)
					{
						Location = id_x.Location,
						EndLocation = id_x.EndLocation
					};

					keyType = TypeDeclarationResolver.ResolveSingle(id, ctxt);

					if (keyType != null)
					{
						var tt = DResolver.StripAliasSymbol(keyType) as MemberSymbol;

						if (tt == null ||
							!(tt.Definition is DVariable) ||
							((DVariable)tt.Definition).Initializer == null)
							return keyType;
					}
				}

				try
				{
					keyVal = Evaluation.EvaluateValue(ad.KeyExpression, ctxt);

					if (keyVal != null)
					{
						// Take the value's type as array key type
						keyType = keyVal.RepresentedType;

						// It should be mostly a number only that points out how large the final array should be
						var pv = Evaluation.GetVariableContents(keyVal, new StandardValueProvider(ctxt)) as PrimitiveValue;
						if (pv != null)
						{
							fixedArrayLength = System.Convert.ToInt32(pv.Value);

							if (fixedArrayLength < 0)
								ctxt.LogError(ad, "Invalid array size: Length value must be greater than 0");
						}
						//TODO Is there any other type of value allowed?
					}
				}
				catch { }
			}
			else
			{
				var t = Resolve(ad.KeyType, ctxt);
				ctxt.CheckForSingleResult(t, ad.KeyType);

				if (t != null && t.Length != 0)
					return t[0];
			}

			return keyType;
		}

		public static AbstractType Resolve(ArrayDecl ad, ResolutionContext ctxt)
		{
			var valueTypes = Resolve(ad.ValueType, ctxt);

			ctxt.CheckForSingleResult(valueTypes, ad);

			AbstractType valueType = null;
			AbstractType keyType = null;
			int fixedArrayLength = -1;

			if (valueTypes != null && valueTypes.Length != 0)
				valueType = valueTypes[0];

			ISymbolValue val;
			keyType = ResolveKey(ad, out fixedArrayLength, out val, ctxt);

			if (keyType == null || (keyType is PrimitiveType &&
			    ((PrimitiveType)keyType).TypeToken == DTokens.Int)) {

				if (fixedArrayLength >= 0) {
					// D Magic: One might access tuple items directly in the pseudo array declaration - so stuff like Tup[0] i; becomes e.g. int i;
					var dtup = DResolver.StripMemberSymbols (valueType) as DTuple;
					if (dtup == null)
						return new ArrayType (valueType, fixedArrayLength, ad);

					if (fixedArrayLength < dtup.Items.Length)
						return AbstractType.Get(dtup.Items [fixedArrayLength]);
					else {
						ctxt.LogError (ad, "TypeTuple only consists of " + dtup.Items.Length + " items. Can't access item at index " + fixedArrayLength);
						return null;
					}
				}
				return new ArrayType (valueType, ad);
			}

			return new AssocArrayType(valueType, keyType, ad);
		}

		public static PointerType Resolve(PointerDecl pd, ResolutionContext ctxt)
		{
			var ptrBaseTypes = Resolve(pd.InnerDeclaration, ctxt);

			ctxt.CheckForSingleResult(ptrBaseTypes, pd);

			if (ptrBaseTypes == null || ptrBaseTypes.Length == 0)
				return null;

			return new PointerType(ptrBaseTypes[0], pd);
		}

		public static DelegateType Resolve(DelegateDeclaration dg, ResolutionContext ctxt)
		{
			var returnTypes = Resolve(dg.ReturnType, ctxt);

			ctxt.CheckForSingleResult(returnTypes, dg.ReturnType);

			if (returnTypes != null && returnTypes.Length != 0)
			{
				List<AbstractType> paramTypes=null;
				if(dg.Parameters!=null && 
				   dg.Parameters.Count != 0)
				{	
					paramTypes = new List<AbstractType>();
					foreach(var par in dg.Parameters)
						paramTypes.Add(ResolveSingle(par.Type, ctxt));
				}
				return new DelegateType(returnTypes[0], dg, paramTypes);
			}
			return null;
		}

		public static AbstractType ResolveSingle(ITypeDeclaration declaration, ResolutionContext ctxt)
		{
			if (declaration is IdentifierDeclaration)
				return ResolveSingle(declaration as IdentifierDeclaration, ctxt);
			else if (declaration is TemplateInstanceExpression)
			{
				var a = Evaluation.GetOverloads(declaration as TemplateInstanceExpression, ctxt);
				ctxt.CheckForSingleResult(a, declaration);
				return a != null && a.Length != 0 ? a[0] : null;
			}

			AbstractType t = null;

			if (declaration is DTokenDeclaration)
				t = Resolve(declaration as DTokenDeclaration);
			else if (declaration is TypeOfDeclaration)
				t = Resolve(declaration as TypeOfDeclaration, ctxt);
			else if (declaration is MemberFunctionAttributeDecl)
				t = Resolve(declaration as MemberFunctionAttributeDecl, ctxt);
			else if (declaration is ArrayDecl)
				t = Resolve(declaration as ArrayDecl, ctxt);
			else if (declaration is PointerDecl)
				t = Resolve(declaration as PointerDecl, ctxt);
			else if (declaration is DelegateDeclaration)
				t = Resolve(declaration as DelegateDeclaration, ctxt);

			//TODO: VarArgDeclaration
			else if (declaration is ITemplateParameterDeclaration)
			{
				var tpd = declaration as ITemplateParameterDeclaration;

				var templateParameter = tpd.TemplateParameter;

				//TODO: Is this correct handling?
				while (templateParameter is TemplateThisParameter)
					templateParameter = (templateParameter as TemplateThisParameter).FollowParameter;

				if (tpd.TemplateParameter is TemplateValueParameter)
				{
					// Return a member result -- it's a static variable
				}
				else
				{
					// Return a type result?
				}
			}

			return t;
		}

		public static AbstractType[] Resolve(ITypeDeclaration declaration, ResolutionContext ctxt)
		{
			if (declaration is IdentifierDeclaration)
				return Resolve((IdentifierDeclaration)declaration, ctxt);
			else if (declaration is TemplateInstanceExpression)
				return Evaluation.GetOverloads((TemplateInstanceExpression)declaration, ctxt);

			var t = ResolveSingle(declaration, ctxt);

			return t == null ? null : new[] { t };
		}







		#region Intermediate methods
		[ThreadStatic]
		static Dictionary<INode, int> stackCalls;

		/// <summary>
		/// The variable's or method's base type will be resolved (if auto type, the intializer's type will be taken).
		/// A class' base class will be searched.
		/// etc..
		/// </summary>
		public static AbstractType HandleNodeMatch(
			INode m,
			ResolutionContext ctxt,
			AbstractType resultBase = null,
			object typeBase = null)
		{
			AbstractType ret = null;

			// See https://github.com/aBothe/Mono-D/issues/161
			int stkC;

			if (stackCalls == null)
			{
				stackCalls = new Dictionary<INode, int>();
				stackCalls[m] = stkC = 1;
			}
			else if (stackCalls.TryGetValue(m, out stkC))
				stackCalls[m] = ++stkC;
			else
				stackCalls[m] = stkC = 1;
			/*
			 * Pushing a new scope is only required if current scope cannot be found in the handled node's hierarchy.
			 * Edit: No, it is required nearly every time because of nested type declarations - then, we do need the 
			 * current block scope.
			 */
			bool popAfterwards;
			{
				var newScope = m is IBlockNode ? (IBlockNode)m : m.Parent as IBlockNode;
				popAfterwards = ctxt.ScopedBlock != newScope && newScope != null;
				if (popAfterwards) {
					var options = ctxt.CurrentContext.ContextDependentOptions;
					var applyOptions = ctxt.ScopedBlockIsInNodeHierarchy (m);
					ctxt.PushNewScope (newScope);
					if (applyOptions)
						ctxt.CurrentContext.ContextDependentOptions = options;
				}
			}

			var canResolveBase = ((ctxt.Options & ResolutionOptions.DontResolveBaseTypes) != ResolutionOptions.DontResolveBaseTypes) && 
			                     stkC < 10 && (m.Type == null || m.Type.ToString(false) != m.Name);
			
			// To support resolving type parameters to concrete types if the context allows this, introduce all deduced parameters to the current context
			if (resultBase is DSymbol)
				ctxt.CurrentContext.IntroduceTemplateParameterTypes((DSymbol)resultBase);

			var importSymbolNode = m as ImportSymbolNode;
			var variable = m as DVariable;

			// Only import symbol aliases are allowed to search in the parse cache
			if (importSymbolNode != null)
				ret = HandleImportSymbolMatch (importSymbolNode,ctxt);
			else if (variable != null)
			{
				AbstractType bt = null;

				if (!(variable is EponymousTemplate)) {
					if (canResolveBase) {
						var bts = TypeDeclarationResolver.Resolve (variable.Type, ctxt);
						ctxt.CheckForSingleResult (bts, variable.Type);

						if (bts != null && bts.Length != 0)
							bt = bts [0];

					// For auto variables, use the initializer to get its type
					else if (variable.Initializer != null) {
							bt = DResolver.StripMemberSymbols (Evaluation.EvaluateType (variable.Initializer, ctxt));
						}

						// Check if inside an foreach statement header
						if (bt == null && ctxt.ScopedStatement != null)
							bt = GetForeachIteratorType (variable, ctxt);
					}

					// Note: Also works for aliases! In this case, we simply try to resolve the aliased type, otherwise the variable's base type
					ret = variable.IsAlias ?
					new AliasedType (variable, bt, typeBase as ISyntaxRegion) as MemberSymbol :
					new MemberSymbol (variable, bt, typeBase as ISyntaxRegion);
				} else
					ret = new EponymousTemplateType (variable as EponymousTemplate, GetInvisibleTypeParameters(variable, ctxt).AsReadOnly(), typeBase as ISyntaxRegion);
			}
			else if (m is DMethod)
			{
				ret = new MemberSymbol(m as DNode,canResolveBase ? GetMethodReturnType(m as DMethod, ctxt) : null, typeBase as ISyntaxRegion);
			}
			else if (m is DClassLike)
				ret = HandleClassLikeMatch (m as DClassLike, ctxt, typeBase, canResolveBase);
			else if (m is DModule)
			{
				var mod = (DModule)m;
				if (typeBase != null && typeBase.ToString() != mod.ModuleName)
				{
					var pack = ctxt.ParseCache.LookupPackage(typeBase.ToString()).FirstOrDefault();
					if (pack != null)
						ret = new PackageSymbol(pack, typeBase as ISyntaxRegion);
				}
				else
					ret = new ModuleSymbol(m as DModule, typeBase as ISyntaxRegion);
			}
			else if (m is DEnum)
				ret = new EnumType((DEnum)m, typeBase as ISyntaxRegion);
			else if (m is TemplateParameter.Node)
			{
				//ResolveResult[] templateParameterType = null;

				//TODO: Resolve the specialization type
				//var templateParameterType = TemplateInstanceHandler.ResolveTypeSpecialization(tmp, ctxt);
				ret = new TemplateParameterSymbol((m as TemplateParameter.Node).TemplateParameter, null, typeBase as ISyntaxRegion);
			}
			else if(m is NamedTemplateMixinNode)
			{
				var tmxNode = m as NamedTemplateMixinNode;
				ret = new MemberSymbol(tmxNode, canResolveBase ? ResolveSingle(tmxNode.Type, ctxt) : null, typeBase as ISyntaxRegion);
			}

			if (popAfterwards)
				ctxt.Pop();
			else if (resultBase is DSymbol)
				ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals((DSymbol)resultBase);

			if (stkC == 1)
				stackCalls.Remove(m);
			else
				stackCalls[m] = stkC-1;

			return ret;
		}

		static AbstractType HandleImportSymbolMatch (ImportSymbolNode importSymbolNode,ResolutionContext ctxt)
		{
			AbstractType ret = null;

			var modAlias = importSymbolNode is ModuleAliasNode;
			if (modAlias ? importSymbolNode.Type != null : importSymbolNode.Type.InnerDeclaration != null) {
				var mods = new List<DModule> ();
				var td = modAlias ? importSymbolNode.Type : importSymbolNode.Type.InnerDeclaration;
				foreach (var mod in ctxt.ParseCache.LookupModuleName (td.ToString ()))
					mods.Add (mod);
				if (mods.Count == 0)
					ctxt.LogError (new NothingFoundError (importSymbolNode.Type));
				else
					if (mods.Count > 1) {
						var m__ = new List<ISemantic> ();
						foreach (var mod in mods)
							m__.Add (new ModuleSymbol (mod, importSymbolNode.Type));
						ctxt.LogError (new AmbiguityError (importSymbolNode.Type, m__));
					}
				var bt = mods.Count != 0 ? (AbstractType)new ModuleSymbol (mods [0], td) : null;
				//TODO: Is this correct behaviour?
				if (!modAlias) {
					var furtherId = ResolveFurtherTypeIdentifier (importSymbolNode.Type.ToString (false), new[] {
						bt
					}, ctxt, importSymbolNode.Type);
					ctxt.CheckForSingleResult (furtherId, importSymbolNode.Type);
					if (furtherId != null && furtherId.Length != 0)
						bt = furtherId [0];
					else
						bt = null;
				}
				ret = new AliasedType (importSymbolNode, bt, importSymbolNode.Type);
			}
			return ret;
		}

		/// <summary>
		/// Add 'superior' template parameters to the current symbol because 
		/// the parameters might be re-used in the nested class.
		/// </summary>
		static List<TemplateParameterSymbol> GetInvisibleTypeParameters(DNode n,ResolutionContext ctxt)
		{
			var invisibleTypeParams = new List<TemplateParameterSymbol> ();

			var tStk = new Stack<ContextFrame> ();
			do {
				var curCtxt = ctxt.Pop ();
				tStk.Push (curCtxt);
				foreach (var kv in curCtxt.DeducedTemplateParameters)
					if (!n.ContainsTemplateParameter (kv.Value.Parameter))
						invisibleTypeParams.Add (kv.Value);
			}
			while (ctxt.PrevContextIsInSameHierarchy);
			while (tStk.Count != 0)
				ctxt.Push (tStk.Pop ());

			return invisibleTypeParams;
		}

		static AbstractType HandleClassLikeMatch (DClassLike dc, ResolutionContext ctxt, object typeBase, bool canResolveBase)
		{
			AbstractType ret;
			UserDefinedType udt = null;
			var invisibleTypeParams = GetInvisibleTypeParameters (dc, ctxt);

			switch (dc.ClassType) {
				case DTokens.Struct:
					ret = new StructType (dc, typeBase as ISyntaxRegion, invisibleTypeParams);
					break;
				case DTokens.Union:
					ret = new UnionType (dc, typeBase as ISyntaxRegion, invisibleTypeParams);
					break;
				case DTokens.Class:
					udt = new ClassType (dc, typeBase as ISyntaxRegion, null, null, invisibleTypeParams);
					ret = null;
					break;
				case DTokens.Interface:
					udt = new InterfaceType (dc, typeBase as ISyntaxRegion, null, invisibleTypeParams);
					ret = null;
					break;
				case DTokens.Template:
					if (dc.ContainsAttribute (DTokens.Mixin))
						ret = new MixinTemplateType (dc, typeBase as ISyntaxRegion, invisibleTypeParams);
					else
						ret = new TemplateType (dc, typeBase as ISyntaxRegion, invisibleTypeParams);
					break;
				default:
					ret = null;
					ctxt.LogError (new ResolutionError (dc, "Unknown type (" + DTokens.GetTokenString (dc.ClassType) + ")"));
					break;
			}
			if (dc.ClassType == DTokens.Class || dc.ClassType == DTokens.Interface)
				ret = canResolveBase ? DResolver.ResolveBaseClasses (udt, ctxt) : udt;
			return ret;
		}

		public static AbstractType[] HandleNodeMatches(
			IEnumerable<INode> matches,
			ResolutionContext ctxt,
			AbstractType resultBase = null,
			object typeDeclaration = null)
		{
			// Abbreviate a foreach-loop + List alloc
			var ll = matches as IList<INode>;
			if (ll != null && ll.Count == 1)
				return new[] { ll[0] == null ? null : HandleNodeMatch(ll[0], ctxt, resultBase, typeDeclaration) };

			var rl = new List<AbstractType>();

			if (matches != null)
				foreach (var m in matches)
				{
					if (m == null)
						continue;

					var res = HandleNodeMatch(m, ctxt, resultBase, typeDeclaration);
					if (res != null)
						rl.Add(res);
				}
			
			return rl.ToArray();
		}

		public static AbstractType GetMethodReturnType(DelegateType dg, ResolutionContext ctxt)
		{
			if (dg == null || ctxt == null)
				return null;

			if (dg.IsFunctionLiteral)
				return GetMethodReturnType(((FunctionLiteral)dg.DeclarationOrExpressionBase).AnonymousMethod, ctxt);
			else
			{
				var rt = ((DelegateDeclaration)dg.DeclarationOrExpressionBase).ReturnType;
				var r = Resolve(rt, ctxt);

				ctxt.CheckForSingleResult(r, rt);

				return r[0];
			}
		}

		public static AbstractType GetMethodReturnType(DMethod method, ResolutionContext ctxt)
		{
			if ((ctxt.Options & ResolutionOptions.DontResolveBaseTypes) == ResolutionOptions.DontResolveBaseTypes)
				return null;

			/*
			 * If a method's type equals null, assume that it's an 'auto' function..
			 * 1) Search for a return statement
			 * 2) Resolve the returned expression
			 * 3) Use that one as the method's type
			 */
			bool pushMethodScope = ctxt.ScopedBlock != method;

			if (method.Type != null)
			{
				if (pushMethodScope)
					ctxt.PushNewScope(method);

				//FIXME: Is it legal to explicitly return a nested type?
				var returnType = TypeDeclarationResolver.Resolve(method.Type, ctxt);

				if (pushMethodScope)
					ctxt.Pop();

				ctxt.CheckForSingleResult(returnType, method.Type);
				if(returnType != null && returnType.Length > 0)
					return returnType[0];
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
					if (pushMethodScope)
					{
						var dedTypes = ctxt.CurrentContext.DeducedTemplateParameters;
						ctxt.PushNewScope(method,returnStmt);

						if (dedTypes.Count != 0)
							foreach (var kv in dedTypes)
								ctxt.CurrentContext.DeducedTemplateParameters[kv.Key] = kv.Value;
					}

					var t =DResolver.StripMemberSymbols(Evaluation.EvaluateType(returnStmt.ReturnExpression, ctxt));

					if (pushMethodScope)
						ctxt.Pop();

					return t;
				}

				return new PrimitiveType (DTokens.Void);
			}

			return null;
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
		public static AbstractType GetForeachIteratorType(DVariable i, ResolutionContext ctxt)
		{
			var r = new List<AbstractType>();
			var curStmt = ctxt.ScopedStatement;

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

					var aggregateType = Evaluation.EvaluateType(fe.Aggregate, ctxt);

					aggregateType = DResolver.StripMemberSymbols(aggregateType);

					if (aggregateType == null)
						return null;

					// The most common way to do a foreach
					if (aggregateType is AssocArrayType)
					{
						var ar = (AssocArrayType)aggregateType;

						return keyIsSearched ? ar.KeyType : ar.ValueType;
					}
					else if (aggregateType is UserDefinedType)
					{
						var tr = (UserDefinedType)aggregateType;

						if (keyIsSearched || !(tr.Definition is IBlockNode))
							continue;

						bool foundIterPropertyMatch = false;
						#region Foreach over Structs and Classes with Ranges

						// Enlist all 'back'/'front' members
						var t_l = new List<AbstractType>();

						foreach (var n in (IBlockNode)tr.Definition)
							if (fe.IsReverse ? n.Name == "back" : n.Name == "front")
								t_l.Add(HandleNodeMatch(n, ctxt));

						// Remove aliases
						var iterPropertyTypes = DResolver.StripAliasSymbols(t_l);

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
						t_l.Clear();
						r.Clear();

						foreach (var n in (IBlockNode)tr.Definition)
							if (n is DMethod &&
								(fe.IsReverse ? n.Name == "opApplyReverse" : n.Name == "opApply"))
								t_l.Add(HandleNodeMatch(n, ctxt));

						iterPropertyTypes = DResolver.StripAliasSymbols(t_l);

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

								var paramType = Resolve(dg.Parameters[iteratorIndex].Type, ctxt);

								if (paramType != null && paramType.Length > 0)
									r.Add(paramType[0]);
							}
						#endregion
					}

					if (r.Count > 1)
						ctxt.LogError(new ResolutionError(curStmt, "Ambigous iterator type"));

					return r.Count != 0 ? r[0] : null;
				}
			}

			return null;
		}
		#endregion
	}
}
