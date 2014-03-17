using System.Collections.Generic;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using System.Diagnostics;
using D_Parser.Resolver.TypeResolution;
using System;
using System.Threading;

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
		public CancellationToken Cancel;

		public ResolutionOptions Options
		{
			[DebuggerStepThrough]
			get { return ContextIndependentOptions | CurrentContext.ContextDependentOptions | (Cancel.IsCancellationRequested ? (ResolutionOptions.DontResolveBaseTypes | ResolutionOptions.DontResolveBaseClasses | ResolutionOptions.NoTemplateParameterDeduction | ResolutionOptions.DontResolveAliases | ResolutionOptions.IgnoreDeclarationConditions) : 0); }
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
			return new ResolutionContext(editor.ParseCache, globalConditions ?? new ConditionalCompilationFlags(editor),
										 DResolver.SearchBlockAt(editor.SyntaxTree, editor.CaretLocation) ?? editor.SyntaxTree,
										 editor.CaretLocation);
		}

		public static ResolutionContext Create(ParseCacheView pcl, ConditionalCompilationFlags globalConditions, IBlockNode scopedBlock)
		{
			return new ResolutionContext(pcl, globalConditions, scopedBlock, CodeLocation.Empty);
		}

		public static ResolutionContext Create(ParseCacheView pcl, ConditionalCompilationFlags globalConditions, IBlockNode scopedBlock, CodeLocation caret)
		{
			return new ResolutionContext(pcl, globalConditions, scopedBlock, caret);
		}

		public ResolutionContext(ParseCacheView parseCache, ConditionalCompilationFlags gFlags, IBlockNode bn)
			: this(parseCache, gFlags, bn, CodeLocation.Empty) { }

		public ResolutionContext(ParseCacheView parseCache, ConditionalCompilationFlags gFlags, IBlockNode bn, CodeLocation caret)
		{
			this.CompilationEnvironment = gFlags;
			this.ParseCache = parseCache;
			
			new ContextFrame(this, bn, caret);
		}
		#endregion

#if DEBUG
		~ResolutionContext()
		{
			if (Debugger.IsLogging ()) {
				var c = CurrentContext;
				if (c != null && c.ScopedBlock != null && ResolutionErrors.Count > 0)
				{
					Debugger.Log(0, "Resolution Error", c.ToString() + "\n");
					int i = 100;
					foreach (var err in ResolutionErrors)
					{
						if (--i < 1)
							break;
						if (err.SyntacticalContext != null)
							Debugger.Log(0, "Resolution Error", err.SyntacticalContext.ToString() + "\n");
						Debugger.Log(0, "Resolution Error", err.Message + "\n");
					}
				}
			}
		}
#endif
		
		#region ContextFrame stacking
		public ContextFrame Pop()
		{
			if(stack.Count>0)
				return stack.Pop();
			return null;
		}

		class PopDisposable : IDisposable
		{
			public readonly ResolutionContext ctxt;
			public readonly DSymbol ds;

			public PopDisposable(ResolutionContext ctxt, DSymbol ds = null)
			{
				this.ctxt = ctxt;
				this.ds = ds;
			}

			public void Dispose()
			{
				if (ds != null)
					ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(ds);
				else
					ctxt.Pop();
			}
		}

		bool Push_(INode newScope, CodeLocation caret)
		{
			while (newScope != null && !(newScope is IBlockNode))
				newScope = newScope.Parent as DNode;

			var pop = newScope != null && ScopedBlock != newScope;

			if (pop)
				PushNewScope(newScope as IBlockNode, caret);
			
			return pop;
		}

		public IDisposable Push(DSymbol ds)
		{
			return ds != null ? Push(ds, ds.Definition.Location) : null;
		}

		public IDisposable Push(DSymbol ds, CodeLocation caret)
		{
			if (ds == null)
				return null;

			var pop = Push_(ds.Definition, caret);

			CurrentContext.IntroduceTemplateParameterTypes(ds);

			return new PopDisposable(this, pop ? null : ds);
		}

		public IDisposable Push(INode newScope)
		{
			return newScope != null ? Push(newScope, newScope.Location) : null;
		}

		public IDisposable Push(INode newScope, CodeLocation caret)
		{
			return Push_(newScope, caret) ? new PopDisposable(this) : null;
		}
		
		public void Push(ContextFrame frm)
		{
			stack.Push(frm);
		}

		void PushNewScope(IBlockNode scope, CodeLocation caret)
		{
			new ContextFrame(this, scope, caret);
		}

		void PushNewScope(IBlockNode scope)
		{
			new ContextFrame(this, scope, scope.BlockStartLocation);
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

				foreach (var kv in cur.DeducedTemplateParameters)
					if (kv.Key.NameHash == idHash) {
						tps = kv.Value;
						ret = true;
						break;
					}

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
		public bool CheckForSingleResult<T>(IEnumerable<T> results, ISyntaxRegion td) where T : ISemantic
		{
			IEnumerator<T> en;
			if (results == null || !(en = results.GetEnumerator()).MoveNext() || en.Current == null) {
				LogError (new NothingFoundError (td));
				return false;
			}

			if (en.MoveNext()) {
				LogError (new AmbiguityError (td, results as IEnumerable<ISemantic>));
				return false;
			}

			return true;
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
