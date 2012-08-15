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

namespace D_Parser.Refactoring
{
	/// <summary>
	/// Analyses an AST and returns all Syntax Regions that represent a type
	/// </summary>
	public class TypeReferenceFinder : DeepASTVisitor
	{
		/// <summary>
		/// Contains the current scope as well as the syntax region
		/// </summary>
		readonly List<ISyntaxRegion> q = new List<ISyntaxRegion>();
		int queueCount;
		int curQueueOffset = 0;
		object _lockObject = new Object();

		/// <summary>
		/// Stores the block and the count position how many syntax regions are related to that block.
		/// Is kept synchronized with the q stack.
		/// </summary>
		readonly SortedDictionary<int, IBlockNode> scopes = new SortedDictionary<int, IBlockNode>();
		readonly SortedDictionary<int, IStatement> scopes_Stmts = new SortedDictionary<int, IStatement>();

		readonly TypeReferencesResult result = new TypeReferencesResult();
		readonly ParseCacheList sharedParseCache;

		private TypeReferenceFinder(ParseCacheList sharedCache)
		{
			this.sharedParseCache = sharedCache;
		}

		public static TypeReferencesResult Scan(IAbstractSyntaxTree ast, ParseCacheList pcl)
		{
			var typeRefFinder = new TypeReferenceFinder(pcl);

			// Enum all identifiers
			typeRefFinder.S(ast);

			// Crawl through all identifiers and try to resolve them.
			typeRefFinder.queueCount = typeRefFinder.q.Count;
			typeRefFinder.ResolveAllIdentifiers();

			return typeRefFinder.result;
		}

		#region Preparation list generation
		protected override void OnScopeChanged(IBlockNode scopedBlock)
		{
			scopes[q.Count] = scopedBlock;
		}

		protected override void OnScopeChanged(IStatement scopedStatement)
		{
			scopes_Stmts[q.Count] = scopedStatement;
		}

		protected override void Handle(ISyntaxRegion o)
		{
			q.Add(o);
		}
		#endregion

		#region Threaded id analysis
		void ResolveAllIdentifiers()
		{
			var threads = new Thread[ThreadedDirectoryParser.numThreads];
			for (int i = 0; i < ThreadedDirectoryParser.numThreads; i++)
			{
				var th = threads[i] = new Thread(_th)
				{
					IsBackground = true,
					Priority = ThreadPriority.Lowest,
					Name = "Type reference analysis thread #" + i
				};
				th.Start(sharedParseCache);
			}

			for (int i = 0; i < ThreadedDirectoryParser.numThreads; i++)
				if (threads[i].IsAlive)
					threads[i].Join(10000);
		}

		void _th(object pcl_shared)
		{
			var pcl = (ParseCacheList)pcl_shared;
			var ctxt = new ResolverContextStack(pcl, new ResolverContext());

			// Make it as most performing as possible by avoiding unnecessary base types. 
			// Aliases should be analyzed deeper though.
			ctxt.CurrentContext.ContextDependentOptions |= 
				ResolutionOptions.StopAfterFirstOverloads | 
				ResolutionOptions.DontResolveBaseClasses | 
				ResolutionOptions.DontResolveBaseTypes | //TODO: Exactly find out which option can be enabled here. Resolving variables' types is needed sometimes - but only, when highlighting a variable reference is wanted explicitly.
				ResolutionOptions.NoTemplateParameterDeduction | 
				ResolutionOptions.ReturnMethodReferencesOnly;

			IBlockNode bn = null;
			IStatement stmt = null;
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

				// Try to get an updated scope
				if (scopes.TryGetValue(i, out bn))
					ctxt.CurrentContext.ScopedBlock = bn;
				if (scopes_Stmts.TryGetValue(i, out stmt))
					ctxt.CurrentContext.ScopedStatement = stmt;

				// Resolve gotten syntax object
				sr = q[i];

				if (sr is PostfixExpression_Access)
					HandleAccessExpressions((PostfixExpression_Access)sr, ctxt);
				else
				{
					AbstractType t = null;
					if (sr is IExpression)
						t = DResolver.StripAliasSymbol(Evaluation.EvaluateType((IExpression)sr, ctxt));
					else if (sr is ITypeDeclaration)
						t = DResolver.StripAliasSymbol(TypeDeclarationResolver.ResolveSingle((ITypeDeclaration)sr, ctxt));

					// Enter into the result lists
					HandleResult(t, sr);
				}
			}
		}

		void HandleResult(AbstractType t, ISyntaxRegion sr)
		{
			if (t == null)
				result.UnresolvedIdentifiers.Add(sr);
			else if (t is UserDefinedType)
				result.ResolvedTypes.Add(sr, (UserDefinedType)t);
			else if (t is MemberSymbol)
				result.ResolvedVariables.Add(sr, (MemberSymbol)t);
			else
				result.MiscResults.Add(sr, t);
		}

		AbstractType HandleAccessExpressions(PostfixExpression_Access acc, ResolverContextStack ctxt)
		{
			AbstractType pfType = null;
			if (acc.PostfixForeExpression is PostfixExpression_Access)
				pfType = HandleAccessExpressions((PostfixExpression_Access)acc.PostfixForeExpression, ctxt);
			else
			{
				pfType = DResolver.StripAliasSymbol(Evaluation.EvaluateType(acc.PostfixForeExpression, ctxt));
				HandleResult(pfType, acc.PostfixForeExpression);
			}
			
			bool ufcs=false;
			var accessedMembers = Evaluation.GetAccessedOverloads(acc, ctxt, out ufcs, pfType);
			ctxt.CheckForSingleResult(accessedMembers, acc);

			if (accessedMembers != null && accessedMembers.Length != 0)
			{
				HandleResult(accessedMembers[0], acc);
				return accessedMembers[0];
			}
			else
				HandleResult(null, acc);

			return null;
		}

		#endregion
	}

	public class TypeReferencesResult
	{
		public Dictionary<ISyntaxRegion, UserDefinedType> ResolvedTypes = new Dictionary<ISyntaxRegion, UserDefinedType>();
		public Dictionary<ISyntaxRegion, MemberSymbol> ResolvedVariables = new Dictionary<ISyntaxRegion, MemberSymbol>();
		public Dictionary<ISyntaxRegion, AbstractType> MiscResults = new Dictionary<ISyntaxRegion, AbstractType>();
		public List<ISyntaxRegion> UnresolvedIdentifiers = new List<ISyntaxRegion>(); 
	}
}
