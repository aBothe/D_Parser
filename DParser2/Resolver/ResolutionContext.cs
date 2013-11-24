using System.Collections.Generic;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using System.Diagnostics;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Parser;

namespace D_Parser.Resolver
{
	public class TooManyResolutionErrors : System.InvalidOperationException
	{
		public ResolutionError[] Errors;
		public TooManyResolutionErrors(ResolutionError[] errors) : base("Too many resolution errors!")
		{
			Errors = errors;
		}
	}

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
			[DebuggerStepThrough]
			get { return ContextIndependentOptions | CurrentContext.ContextDependentOptions; }
		}

		public ParseCacheView ParseCache;

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
		
		public ContextFrame CurrentContext
		{
			get {
				return stack.Count > 0 ? stack.Peek() : null;
			}
		}
		#endregion

		#region Init/Constructor
		public static ResolutionContext Create(IEditorData editor, ConditionalCompilationFlags globalConditions = null)
		{
			IStatement stmt;
			return new ResolutionContext(editor.ParseCache, globalConditions ?? new ConditionalCompilationFlags(editor),
										 DResolver.SearchBlockAt(editor.SyntaxTree, editor.CaretLocation, out stmt) ?? editor.SyntaxTree,
										 stmt);
		}

		public static ResolutionContext Create(ParseCacheView pcl, ConditionalCompilationFlags globalConditions, IBlockNode scopedBlock, IStatement scopedStatement=null)
		{
			return new ResolutionContext(pcl, globalConditions, scopedBlock, scopedStatement);
		}

		public ResolutionContext(ParseCacheView parseCache, ConditionalCompilationFlags gFlags, IBlockNode bn, IStatement stmt=null)
		{
			this.CompilationEnvironment = gFlags;
			this.ParseCache = parseCache;
			
			var initCtxt = new ContextFrame(this, bn, stmt);
			
			stack.Push(initCtxt);
		}
		#endregion

		~ResolutionContext()
		{
			if (Debugger.IsLogging ()) {
				var c = CurrentContext;
				if(c != null && c.ScopedBlock != null)
					Debugger.Log (0, "Resolution Error", c.ToString());
				foreach (var err in ResolutionErrors) {
					if(err.SyntacticalContext != null)
						Debugger.Log (0, "Resolution Error", err.SyntacticalContext.ToString ());
					Debugger.Log (0, "Resolution Error", err.Message);
				}
			}
		}
		
		#region ContextFrame stacking
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

		public List<TemplateParameterSymbol> DeducedTypesInHierarchy
		{
			get
			{
				var dedTypes = new List<TemplateParameterSymbol>();
				var stk = new Stack<ContextFrame>();

				while (true)
				{
					dedTypes.AddRange(stack.Peek().DeducedTemplateParameters.Values);

					if (!PrevContextIsInSameHierarchy)
						break;

					stk.Push(stack.Pop());
				}

				while (stk.Count != 0)
					stack.Push(stk.Pop());
				return dedTypes;
			}
		}

		public bool GetTemplateParam(int idHash, out TemplateParameterSymbol tps)
		{
			tps = null;
			Stack<ContextFrame> backup = null;
			bool ret = false;

			while (stack.Count != 0) {
				var cur = stack.Peek ();

				if(ret = cur.DeducedTemplateParameters.TryGetValue(idHash, out tps))
					break;

				if (backup == null)
					backup = new Stack<ContextFrame> ();

				backup.Push(stack.Pop ());

				if (cur.ScopedBlock == null || stack.Count == 0 || cur.ScopedBlock.Parent != stack.Peek ().ScopedBlock)
					break;
			}

			if(backup != null)
				while (backup.Count != 0)
					stack.Push (backup.Pop ());

			return ret;
		}

		/// <summary>
		/// Returns true if the currently scoped node block is located somewhere inside the hierarchy of n.
		/// Used for prevention of unnecessary context pushing/popping.
		/// </summary>
		public bool ScopedBlockIsInNodeHierarchy(INode n)
		{
			var t_node_scoped = CurrentContext.ScopedBlock;
			var t_node = n as IBlockNode ?? n.Parent as IBlockNode;

			while (t_node != null)
			{
				if (t_node == t_node_scoped)
					return true;
				t_node = t_node.Parent as IBlockNode;
			}

			return false;
		}
		#endregion

		/// <summary>
		/// Returns true if 'results' only contains one valid item
		/// </summary>
		public bool CheckForSingleResult<T>(T[] results, ISyntaxRegion td) where T : ISemantic
		{
			if (results == null || results.Length == 0) {
				LogError (new NothingFoundError (td));
				return false;
			}
			if (results.Length > 1) {
				var r = new List<ISemantic> ();
				foreach (var res in results)
					r.Add (res);

				LogError (new AmbiguityError (td, r));
				return false;
			}

			return results [0] != null;
		}

		#region Result caching
		public bool TryGetCachedResult(INode n, out AbstractType type, params IExpression[] templateArguments)
		{
			type = null;
			
			return false;
		}
		#endregion
		
		#region Error handling
		const int maxErrorCount = 20;
		public void LogError(ResolutionError err)
		{
			ResolutionErrors.Add(err);/*
			if (ResolutionErrors.Count > maxErrorCount && CompletionOptions.Instance.LimitResolutionErrors)
				throw new TooManyResolutionErrors (ResolutionErrors.ToArray());*/
		}

		public void LogError(ISyntaxRegion syntaxObj, string msg)
		{
			ResolutionErrors.Add(new ResolutionError(syntaxObj,msg));/*
			if (ResolutionErrors.Count > maxErrorCount && CompletionOptions.Instance.LimitResolutionErrors)
				throw new TooManyResolutionErrors (ResolutionErrors.ToArray());*/
		}
		#endregion
	}
}
