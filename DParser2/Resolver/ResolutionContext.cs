using System.Collections.Generic;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver
{
	public class ResolutionContext
	{
		#region Properties
		/// <summary>
		/// Stores global compilation parameters.
		/// Used by BuildConditionSet() as global flags for ConditionSet instances.
		/// </summary>
		public readonly ConditionalCompilationFlags CompilationEnvironment;
		protected Stack<ContextFrame> stack = new Stack<ContextFrame>();
		public ResolutionOptions ContextIndependentOptions = ResolutionOptions.Default;
		public readonly List<ResolutionError> ResolutionErrors = new List<ResolutionError>();

		public ResolutionOptions Options
		{
			get { return ContextIndependentOptions | CurrentContext.ContextDependentOptions; }
		}

		public ParseCacheList ParseCache = new ParseCacheList();

		public IBlockNode ScopedBlock
		{
			get {
				if (stack.Count<1)
					return null;

				return CurrentContext.ScopedBlock;
			}
		}

		public IStatement ScopedStatement
		{
			get
			{
				if (stack.Count < 1)
					return null;

				return CurrentContext.ScopedStatement;
			}
		}

		Dictionary<object, Dictionary<string, ISemantic[]>> resolvedTypes = new Dictionary<object, Dictionary<string, ISemantic[]>>();

		/// <summary>
		/// Stores scoped-block dependent type dictionaries, which store all types that were already resolved once
		/// </summary>
		public Dictionary<object, Dictionary<string, ISemantic[]>> ResolvedTypes
		{
			get { return resolvedTypes; }
		}

		public ContextFrame CurrentContext
		{
			get {
				return stack.Peek();
			}
		}
		#endregion

		public static ResolutionContext Create(IEditorData editor, ConditionalCompilationFlags globalConditions = null)
		{
			IStatement stmt = null;
			return new ResolutionContext(editor.ParseCache, globalConditions ?? new ConditionalCompilationFlags(editor),
			                             DResolver.SearchBlockAt(editor.SyntaxTree, editor.CaretLocation, out stmt) ?? editor.SyntaxTree,
			                             stmt);
		}

		public static ResolutionContext Create(ParseCacheList pcl, ConditionalCompilationFlags globalConditions, IBlockNode scopedBlock, IStatement scopedStatement=null)
		{
			return new ResolutionContext(pcl, globalConditions, scopedBlock, scopedStatement);
		}

		protected ResolutionContext(ParseCacheList ParseCache, ConditionalCompilationFlags gFlags, IBlockNode bn, IStatement stmt=null)
		{
			this.CompilationEnvironment = gFlags;
			this.ParseCache = ParseCache;
			
			var initCtxt = new ContextFrame(this, bn, stmt);
			
			stack.Push(initCtxt);
		}

		public ContextFrame Pop()
		{
			if(stack.Count>0)
				return stack.Pop();
			return null;
		}
		
		public void Push(ContextFrame frm)
		{
			stack.Push(frm);
		}

		public void PushNewScope(IBlockNode scope, IStatement stmt = null)
		{
			stack.Push(new ContextFrame(this, scope, stmt));
		}

		/// <summary>
		/// Returns either the nearest block statement that is containing the scoped statement or the 
		/// currently scoped block if no statement scope is given.
		/// </summary>
		object GetMostFittingBlock()
		{
			if (CurrentContext == null)
				return null;

			if (CurrentContext.ScopedStatement != null)
			{
				var r = CurrentContext.ScopedStatement;

				while (r != null)
				{
					if (r is BlockStatement)
						return r;
					else
						r = r.Parent;
				}
			}
			
			return CurrentContext.ScopedBlock;
		}
		/* TODO: Resolution caching
		public void TryCacheResults(string TypeDeclarationString, ISemantic[] NodeMatches)
		{
			var ScopedType = GetMostFittingBlock();

			Dictionary<string, ISemantic[]> subDict = null;

			if (!resolvedTypes.TryGetValue(ScopedType, out subDict))
				resolvedTypes.Add(ScopedType, subDict = new Dictionary<string, ISemantic[]>());

			if (!subDict.ContainsKey(TypeDeclarationString))
				subDict.Add(TypeDeclarationString, NodeMatches);
		}

		public bool TryGetAlreadyResolvedType(string TypeDeclarationString, out ISemantic[] NodeMatches)
		{
			var ScopedType = GetMostFittingBlock();

			Dictionary<string, ISemantic[]> subDict = null;

			if (ScopedType != null && !resolvedTypes.TryGetValue(ScopedType, out subDict))
			{
				NodeMatches = null;
				return false;
			}

			if (subDict != null)
				return subDict.TryGetValue(TypeDeclarationString, out NodeMatches);

			NodeMatches = null;
			return false;
		}
*/
		/// <summary>
		/// Returns true if the the context that is stacked below the current context represents the parent item of the current block scope
		/// </summary>
		public bool PrevContextIsInSameHierarchy
		{
			get
			{
				if (stack.Count < 2)
					return false;

				var cur = stack.Pop();

				bool IsParent = cur.ScopedBlock!= null && cur.ScopedBlock.Parent == stack.Peek().ScopedBlock;

				stack.Push(cur);
				return IsParent;

			}
		}

		/// <summary>
		/// Returns true if the currently scoped node block is located somewhere inside the hierarchy of n.
		/// Used for prevention of unnecessary context pushing/popping.
		/// </summary>
		public bool NodeIsInCurrentScopeHierarchy(INode n)
		{
			var t_node_scoped = CurrentContext.ScopedBlock;
			var t_node = n is IBlockNode ? (IBlockNode)n : n.Parent as IBlockNode;

			while (t_node != null)
			{
				if (t_node == t_node_scoped)
					return true;
				t_node = t_node.Parent as IBlockNode;
			}

			return false;
		}

		/// <summary>
		/// Returns true if 'results' only contains one valid item
		/// </summary>
		public bool CheckForSingleResult<T>(T[] results, ISyntaxRegion td) where T : ISemantic
		{
			if (results == null || results.Length == 0)
			{
				LogError(new NothingFoundError(td));
				return false;
			}
			else if (results.Length > 1)
			{
				var r = new List<ISemantic>();
				foreach (var res in results)
					r.Add(res);

				LogError(new AmbiguityError(td, r));
				return false;
			}

			return results[0] != null;
		}

		public void LogError(ResolutionError err)
		{
			ResolutionErrors.Add(err);
		}

		public void LogError(ISyntaxRegion syntaxObj, string msg)
		{
			ResolutionErrors.Add(new ResolutionError(syntaxObj,msg));
		}
	}
}
