using System;
using D_Parser.Completion;
using D_Parser.Dom;
using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver
{
	/**
	 * <summary>Provides utilities for quickly searching INodes inside ASTs without paying attention to any constraints, access restrictions or mixed-in code.</summary>
	 */
	public static class LooseResolution
	{
		public enum NodeResolutionAttempt
		{
			Normal,
			NoParameterOrTemplateDeduction,
			RawSymbolLookup,
		}

		public static AbstractType ResolveTypeLoosely(IEditorData editor, ISyntaxRegion o, out NodeResolutionAttempt resolutionAttempt, bool editorFriendly)
		{
			resolutionAttempt = NodeResolutionAttempt.Normal;

			if (o == null)
				return null;

			if (editorFriendly) {
				if (o is PostfixExpression_MethodCall)
					o = (o as PostfixExpression_MethodCall).PostfixForeExpression;
			}
			

			AbstractType ret = null;
			NodeResolutionAttempt resAttempt = NodeResolutionAttempt.Normal;
			var ctxt = editor.GetLooseResolutionContext(resAttempt) ?? ResolutionContext.Create(editor, false);

			CodeCompletion.DoTimeoutableCompletionTask(null, ctxt, () =>
				{
					ctxt.Push(editor);

					ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly | ResolutionOptions.DontResolveAliases;

					if (o is IExpression)
						ret = ExpressionTypeEvaluation.EvaluateType((IExpression)o, ctxt, false);
					else if (o is ITypeDeclaration)
						ret = TypeDeclarationResolver.ResolveSingle((ITypeDeclaration)o, ctxt);
					else if (o is INode)
						ret = TypeDeclarationResolver.HandleNodeMatch(o as INode, ctxt, null, o);

					if(ret != null && !(ret is UnknownType))
						return;

					resAttempt = NodeResolutionAttempt.NoParameterOrTemplateDeduction;
					var ct = editor.GetLooseResolutionContext(resAttempt);
					if (ct != null)
					{
						ct.Push(editor);
						ctxt = ct;
					}
					else
						ctxt.ClearCaches();

					ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly | ResolutionOptions.DontResolveAliases;

					if (o is IdentifierExpression)
						ret = AmbiguousType.Get(ExpressionTypeEvaluation.GetOverloads(o as IdentifierExpression, ctxt, deduceParameters: false));
					else if (o is ITypeDeclaration)
						ret = TypeDeclarationResolver.ResolveSingle(o as ITypeDeclaration, ctxt);
					else if (o is IExpression)
						ret = ExpressionTypeEvaluation.EvaluateType(o as IExpression, ctxt, false);

					if(ret != null && !(ret is UnknownType))
						return;

					resAttempt = NodeResolutionAttempt.RawSymbolLookup;
					ct = editor.GetLooseResolutionContext(resAttempt);
					if (ct != null)
					{
						ct.Push(editor);
						ctxt = ct;
					}
					else
						ctxt.ClearCaches();

					ret = LookupIdRawly(editor.ParseCache, o, editor.SyntaxTree);
				}, editor.CancelToken);

			resolutionAttempt = resAttempt;

			return ret;
		}

		public static AbstractType ResolveTypeLoosely(IEditorData editor, out NodeResolutionAttempt resolutionAttempt, out ISyntaxRegion sr, bool editorFriendly = false)
		{
			return ResolveTypeLoosely (editor, sr = DResolver.GetScopedCodeObject(editor), out resolutionAttempt, editorFriendly);
		}

		public static AbstractType SearchNodesByName(ref ISyntaxRegion identifier, IEditorData editor)
		{
			
			var stk = new List<ISyntaxRegion>();

			if (identifier is ITypeDeclaration) {
				var td = identifier as ITypeDeclaration;
				do {
					stk.Add (td);
					td = td.InnerDeclaration;
				} while(td != null);
			} else if (identifier is IExpression) {
				var x = identifier as IExpression;
				while (x != null) {
					stk.Add (x);

					if (!(x is PostfixExpression))
						break;
					
					x = (x as PostfixExpression).PostfixForeExpression;
				}
			} else
				return null;

			if (stk.Count > 0)
				identifier = stk [stk.Count - 1];

			int idToScanForFirst = identifier is IntermediateIdType ? (identifier as IntermediateIdType).IdHash : 0;

			List<ModulePackage> foundPackages;
			List<INode> foundItems;

			SearchNodesByName (idToScanForFirst, editor.SyntaxTree, editor.ParseCache, out foundPackages, out foundItems);

			var res = new List<AbstractType> ();

			foreach (var pack in foundPackages)
				res.Add (new PackageSymbol(pack));

			var ctxt = ResolutionContext.Create(editor, true);
			ctxt.ContextIndependentOptions = ResolutionOptions.DontResolveBaseTypes | ResolutionOptions.NoTemplateParameterDeduction;

			res.AddRange(TypeDeclarationResolver.HandleNodeMatches (foundItems, ctxt));

			return AmbiguousType.Get (res);
		}

		public static void SearchNodesByName(int idToFind, DModule parseCacheContext, Misc.ParseCacheView pcw, out List<ModulePackage> foundPackages, out List<INode> foundItems)
		{
			foundItems = new List<INode>();
			foundPackages = new List<ModulePackage>();

			if(idToFind == 0)
				return;

			var currentList = new List<IBlockNode>();
			var nextList = new List<IBlockNode>();

			var currentPackageList = new List<ModulePackage>();
			var nextPackageList = new List<ModulePackage>();

			currentPackageList.AddRange(pcw.EnumRootPackagesSurroundingModule(parseCacheContext));

			while(currentPackageList.Count != 0)
			{
				foreach(var pack in currentPackageList)
				{
					currentList.AddRange (pack.GetModules());

					if(pack.NameHash == idToFind)
						foundPackages.Add(pack);

					nextPackageList.AddRange(pack);
				}

				if(nextPackageList.Count == 0)
					break;

				currentPackageList.Clear();
				currentPackageList.AddRange(nextPackageList);
				nextPackageList.Clear();
			}


			while (currentList.Count != 0) {

				foreach (var i in currentList) {
					var items = i[idToFind];
					if (items != null)
						foundItems.AddRange (items);

					foreach (var k in i)
						if (k is IBlockNode && !foundItems.Contains (k))
							nextList.Add (k as IBlockNode);
				}

				currentList.Clear ();
				currentList.AddRange (nextList);
				nextList.Clear ();
			}
		}

		public static AbstractType LookupIdRawly(Misc.ParseCacheView parseCache, ISyntaxRegion o, DModule oContext)
		{
			if (parseCache == null)
				throw new ArgumentNullException ("parseCache");
			if (o == null)
				throw new ArgumentNullException ("o");

			var ctxt = new ResolutionContext (parseCache, null, oContext, o.Location);

			/*
			 * Stuff like std.stdio.someSymbol should be covered by this already
			 */
			ctxt.ContextIndependentOptions = 
				ResolutionOptions.DontResolveBaseTypes | 
				ResolutionOptions.IgnoreAllProtectionAttributes | 
				ResolutionOptions.IgnoreDeclarationConditions | 
				ResolutionOptions.NoTemplateParameterDeduction | 
				ResolutionOptions.ReturnMethodReferencesOnly;

			AbstractType res;

			var td = o as ITypeDeclaration;
			var x = o as IExpression;

			if (td != null)
				res = TypeDeclarationResolver.ResolveSingle (td, ctxt, false);
			else if (x != null)
				res = ExpressionTypeEvaluation.EvaluateType (x, ctxt, false);
			else
				return null;

			if(res != null)
				return res;


			IntermediateIdType id;

			if (td != null)
				id = td.InnerMost as IntermediateIdType;
			else
				id = x as IntermediateIdType;

			if (id == null)
				return null;

			var l = new List<AbstractType> ();

			foreach (var pack in parseCache.EnumRootPackagesSurroundingModule(oContext)) {
				if (pack == null)
					continue;
				
				foreach (DModule mod in pack) {
					if (mod == null)
						continue;

					var children = mod [id.IdHash];
					if (children != null)
						foreach (var n in children)
							l.Add (TypeDeclarationResolver.HandleNodeMatch (n, ctxt, null, id));
				}
			}

			return AmbiguousType.Get(l);
		}
	}
}

