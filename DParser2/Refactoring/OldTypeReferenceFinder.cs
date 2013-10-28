using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using System.Threading;
using D_Parser.Resolver.ASTScanner;

namespace D_Parser.Refactoring
{
	/// <summary>
	/// Analyses an AST and returns all Syntax Regions that represent a type
	/// </summary>
	public class OldTypeReferenceFinder : DeepASTVisitor
	{
		readonly Dictionary<IBlockNode, Dictionary<int, INode>> TypeCache = new Dictionary<IBlockNode, Dictionary<int, INode>>();

		/// <summary>
		/// Contains the current scope as well as the syntax region
		/// </summary>
		readonly List<ISyntaxRegion> q = new List<ISyntaxRegion>();
		int queueCount;
		int curQueueOffset = 0;
		object _lockObject = new Object();

		IBlockNode curScope = null;
		DModule ast = null;

		readonly TypeReferencesResult result = new TypeReferencesResult();
		ConditionalCompilationFlags gFlags_shared;
		readonly ParseCacheView sharedParseCache;
		ResolutionContext sharedCtxt;

		private OldTypeReferenceFinder(ParseCacheView sharedCache, ConditionalCompilationFlags compilationEnvironment = null)
		{
			this.sharedParseCache = sharedCache;
			sharedCtxt = ResolutionContext.Create(sharedCache, gFlags_shared = compilationEnvironment, null);
		}

		public static TypeReferencesResult Scan(DModule ast, ParseCacheView pcl, ConditionalCompilationFlags compilationEnvironment = null)
		{
			if (ast == null)
				return new TypeReferencesResult();

			var typeRefFinder = new OldTypeReferenceFinder(pcl, compilationEnvironment);

			typeRefFinder.ast = ast;
			// Enum all identifiers
			typeRefFinder.S(ast);

			// Crawl through all remaining expressions by evaluating their types and check if they're actual type references.
			typeRefFinder.queueCount = typeRefFinder.q.Count;
			typeRefFinder.ResolveAllIdentifiers();

			return typeRefFinder.result;
		}

		void CreateDeeperLevelCache(IBlockNode bn)
		{
			var dd = TypeCache[bn] = new Dictionary<int,INode>();

			// Set the parent to null to crawl through current level only. Imports/Mixins etc. will be handled though.
			var parentBackup = bn.Parent;
			bn.Parent = null;

			sharedCtxt.CurrentContext.Set(bn);
			foreach (var n in ItemEnumeration.EnumScopedBlockChildren(sharedCtxt, MemberFilter.Types))
			{
				if (n.NameHash != 0)
					dd[n.NameHash] = n;
			}

			bn.Parent = parentBackup;
		}

		#region Preparation list generation
		protected override void OnScopeChanged(IBlockNode scopedBlock)
		{
			CreateDeeperLevelCache(curScope = scopedBlock);
		}

		protected override void Handle(ISyntaxRegion o)
		{
			if (o is IdentifierDeclaration || o is TemplateInstanceExpression)
			{
				if (DoPrimaryIdCheck(ExtractId(o)))
					result.TypeMatches.Add(o);
			}
			/* Though type resolution is very fast now it's still kinda slow - 300 ms for all expressions in std.stdio
			else if (o is IdentifierExpression)
			{
				if (DoPrimaryIdCheck((string)((IdentifierExpression)o).Value))
					q.Add(o);
			}
			else if (o is PostfixExpression_Access)
				q.AddRange(DoPrimaryIdCheck((PostfixExpression_Access)o));*/
		}
		#endregion

		/// <summary>
		/// Returns true if a type called 'id' exists in the current scope
		/// </summary>
		bool DoPrimaryIdCheck(int id)
		{
			if (id == 0)
				return false;

			var tc = TypeCache[curScope];
			var bn = curScope;

			while (bn != null)
			{
				if(tc.ContainsKey(id))
					return true;

				bn=bn.Parent as IBlockNode;
				if (bn == null || !TypeCache.TryGetValue(bn, out tc))
					return false;
			}
			
			return false;
		}

		List<IExpression> DoPrimaryIdCheck(PostfixExpression_Access acc)
		{
			var r = new List<IExpression>();
			while(acc != null){
				if (DoPrimaryIdCheck(ExtractId(acc)))
					r.Add(acc);

				// Scan down the access expression for other, deeper expressions
				if (acc.PostfixForeExpression is PostfixExpression_Access)
					acc = (PostfixExpression_Access)acc.PostfixForeExpression;
				else
				{
					if (DoPrimaryIdCheck(ExtractId(acc.PostfixForeExpression)))
						r.Add(acc.PostfixForeExpression);
					break;
				}
			}
			return r;
		}

		public static int ExtractId(ISyntaxRegion o)
		{
			if (o is IdentifierDeclaration)
				return ((IdentifierDeclaration)o).IdHash;
			else if (o is IdentifierExpression && ((IdentifierExpression)o).IsIdentifier)
				return ((IdentifierExpression)o).ValueStringHash;
			else if (o is PostfixExpression_Access)
				return ExtractId(((PostfixExpression_Access)o).AccessExpression);
			else if (o is TemplateInstanceExpression)
				return ((TemplateInstanceExpression)o).TemplateIdHash;
			else if (o is NewExpression)
				return ExtractId(((NewExpression)o).Type);
			return 0;
		}

		#region Threaded id analysis
		void ResolveAllIdentifiers()
		{
			if (q.Count == 0)
				return;

			if (System.Diagnostics.Debugger.IsAttached)
			{
				_th(sharedParseCache);
				return;
			}

			var threads = new Thread[GlobalParseCache.NumThreads];
			for (int i = 0; i < GlobalParseCache.NumThreads; i++)
			{
				var th = threads[i] = new Thread(_th)
				{
					IsBackground = true,
					Priority = ThreadPriority.Lowest,
					Name = "Type reference analysis thread #" + i
				};
				th.Start(sharedParseCache);
			}

			for (int i = 0; i < GlobalParseCache.NumThreads; i++)
				if (threads[i].IsAlive)
					threads[i].Join(10000);
		}

		void _th(object pcl_shared)
		{
			var ctxt = ResolutionContext.Create((ParseCacheView)pcl_shared, gFlags_shared, ast);

			// Make it as most performing as possible by avoiding unnecessary base types. 
			// Aliases should be analyzed deeper though.
			ctxt.CurrentContext.ContextDependentOptions |=
				ResolutionOptions.DontResolveBaseTypes | //TODO: Exactly find out which option can be enabled here. Resolving variables' types is needed sometimes - but only, when highlighting a variable reference is wanted explicitly.
				ResolutionOptions.NoTemplateParameterDeduction | 
				ResolutionOptions.ReturnMethodReferencesOnly;

			ISyntaxRegion sr = null;
			int i = 0;

			while (curQueueOffset < queueCount)
			{
				// Avoid race condition runtime errors
				lock (_lockObject)
				{
					i = curQueueOffset;
					curQueueOffset++;
				}

				// Resolve gotten syntax object
				sr = q[i];
				var sb = ctxt.CurrentContext.ScopedBlock;
				IStatement tStmt;
				ctxt.CurrentContext.Set(DResolver.SearchBlockAt(
					sb==null || sr.Location < sb.BlockStartLocation || sr.EndLocation > sb.EndLocation ? ast : sb,
					sr.Location, 
					out tStmt),tStmt);

				if (sr is PostfixExpression_Access)
					HandleAccessExpressions((PostfixExpression_Access)sr, ctxt);
				else
				{
					AbstractType t = null;
					if (sr is IExpression)
						t = DResolver.StripAliasSymbol(Evaluation.EvaluateType((IExpression)sr, ctxt));
					else if (sr is ITypeDeclaration)
						t = DResolver.StripAliasSymbol(TypeDeclarationResolver.ResolveSingle((ITypeDeclaration)sr, ctxt));

					// Enter into the result list
					HandleResult(sr, t);
				}
			}
		}

		AbstractType HandleAccessExpressions(PostfixExpression_Access acc, ResolutionContext ctxt)
		{
			AbstractType pfType = null;
			if (acc.PostfixForeExpression is PostfixExpression_Access)
				pfType = HandleAccessExpressions((PostfixExpression_Access)acc.PostfixForeExpression, ctxt);
			else
			{
				pfType = DResolver.StripAliasSymbol(Evaluation.EvaluateType(acc.PostfixForeExpression, ctxt));

				if (acc.PostfixForeExpression is IdentifierExpression ||
					acc.PostfixForeExpression is TemplateInstanceExpression ||
					acc.PostfixForeExpression is PostfixExpression_Access)
					HandleResult(acc.PostfixForeExpression, pfType);
			}
			
			var accessedMembers = Evaluation.GetAccessedOverloads(acc, ctxt, pfType);
			ctxt.CheckForSingleResult(accessedMembers, acc);

			if (accessedMembers != null && accessedMembers.Length != 0)
			{
				HandleResult(acc, accessedMembers[0]);
				return accessedMembers[0];
			}

			return null;
		}

		void HandleResult(ISyntaxRegion sr, AbstractType t)
		{
			if (t is UserDefinedType)
				result.TypeMatches.Add(sr);
		}

		#endregion
	}

	public class TypeReferencesResult
	{
		public Dictionary<int, List<ISyntaxRegion>> Matches = new Dictionary<int, List<ISyntaxRegion>>();
		public List<ISyntaxRegion> TypeMatches = new List<ISyntaxRegion>();
	}
}
