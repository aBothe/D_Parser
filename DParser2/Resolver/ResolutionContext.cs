﻿using System.Collections.Generic;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using System.Diagnostics;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver.ASTScanner;
using System;
using System.Threading;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.ExpressionSemantics.Exceptions;

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

	public sealed class ResolutionContext
	{
		#region Properties
		public CompletionOptions CompletionOptions { get; }

		/// <summary>
		/// Stores global compilation parameters.
		/// Used by BuildConditionSet() as global flags for ConditionSet instances.
		/// </summary>
		public readonly ConditionalCompilationFlags CompilationEnvironment;
		readonly List<ContextFrame> stack = new List<ContextFrame>();
		/// <summary>
		/// Note: Enumerates from top to first scope, so vice-versa the usual direction.
		/// </summary>
		public IEnumerable<ContextFrame> ContextStack {
			get{ 
				for (var i = stack.Count - 1; i >= 0; i--)
					yield return stack [i];
			}
		}
		private int evaluationCallStackDepth;
		const int MAX_CALLSTACK_DEPTH = 30;

		readonly ThreadLocal<ResolutionOptions> contextIndependentOptions = new ThreadLocal<ResolutionOptions> (() => ResolutionOptions.Default);
		public ResolutionOptions ContextIndependentOptions { 
			get{ return contextIndependentOptions.Value; } 
			set{ contextIndependentOptions.Value = value; }
		}

		public readonly List<ResolutionError> ResolutionErrors = new List<ResolutionError>();

		public CancellationToken CancellationToken { get; set; }

		const ResolutionOptions AvoidEverything = ResolutionOptions.DontResolveBaseTypes
					| ResolutionOptions.DontResolveBaseClasses
					| ResolutionOptions.NoTemplateParameterDeduction
					| ResolutionOptions.DontResolveAliases
					| ResolutionOptions.IgnoreDeclarationConditions;

		public ResolutionOptions Options
		{
			[DebuggerStepThrough]
			get {
				if (!CancellationToken.IsCancellationRequested)
					return ContextIndependentOptions | (CurrentContext != null ? CurrentContext.ContextDependentOptions : 0);
				return AvoidEverything;
			}
		}

		internal readonly ResolutionCache<MixinAnalysis.MixinCacheItem> MixinCache;
		internal readonly ResolutionCache<AbstractType> Cache;
		//internal readonly ResolutionCache<ISymbolValue> ValueCache;
		public readonly ParseCacheView ParseCache;
		internal readonly ResolutionCache<NameScan> NameScanCache;

		public void ClearCaches()
		{
			MixinCache.Clear ();
			Cache.Clear();
			NameScanCache.Clear();
		}

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
				return stack.Count > 0 ? stack[stack.Count-1] : null;
			}
		}
		#endregion

		#region Init/Constructor
		public static ResolutionContext Create(IEditorData editor, bool pushFirstScope, ConditionalCompilationFlags globalConditions = null)
		{
			var ctxt = Create(editor.ParseCache, globalConditions ?? new ConditionalCompilationFlags(editor));
			if (pushFirstScope)
				ctxt.Push(editor);
			return ctxt;
		}

		public static ResolutionContext Create(ParseCacheView pcl, ConditionalCompilationFlags globalConditions)
		{
			return Create(pcl, globalConditions, null, CodeLocation.Empty);
		}

		public static ResolutionContext Create(ParseCacheView pcl, ConditionalCompilationFlags globalConditions, IBlockNode scopedBlock)
		{
			return Create(pcl, globalConditions, scopedBlock, CodeLocation.Empty);
		}

		public static ResolutionContext Create(ParseCacheView pcl, ConditionalCompilationFlags globalConditions, IBlockNode scopedBlock, CodeLocation caret)
		{
			return new ResolutionContext(pcl, globalConditions, scopedBlock, caret);
		}

		ResolutionContext(ParseCacheView parseCache, ConditionalCompilationFlags gFlags, IBlockNode bn, CodeLocation caret)
		{
			this.CompletionOptions = CompletionOptions.Instance;
			this.CompilationEnvironment = gFlags;
			this.ParseCache = parseCache;
			Cache = new ResolutionCache<AbstractType>(this);
			//ValueCache = new ResolutionCache<ISymbolValue>(this);
			MixinCache = new ResolutionCache<MixinAnalysis.MixinCacheItem>(this);
			NameScanCache = new ResolutionCache<NameScan>(this);

			if (bn != null) {
				PushNewScope (bn, caret);
			}
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
		public void Pop()
		{
			if (stack.Count > 0)
				stack.RemoveAt(stack.Count-1);
		}

		public void PopAll()
		{
			stack.Clear();
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

		public IDisposable Push(DSymbol ds, bool keepDeducedTemplateParams = false)
		{
			return ds != null ? Push(ds, ds.Definition.Location, keepDeducedTemplateParams) : null;
		}
 
		public IDisposable Push(DSymbol ds, CodeLocation caret, bool keepDeducedTemplateParams = false)
		{
			if (ds == null)
				return null;

			var pop = Push_(ds.Definition, caret, keepDeducedTemplateParams);

			CurrentContext.IntroduceTemplateParameterTypes(ds);

			return new PopDisposable(this, pop ? null : ds);
		}

		public IDisposable Push(IEditorData editor)
		{
			return Push(ASTSearchHelper.SearchBlockAt(editor.SyntaxTree, editor.CaretLocation) ?? editor.SyntaxTree, editor.CaretLocation);
		}

		public IDisposable Push(INode newScope, bool keepDeducedTemplateParams = false)
		{
			return newScope != null ? Push(newScope, newScope.Location, keepDeducedTemplateParams) : null;
		}

		public IDisposable Push(INode newScope, CodeLocation caret, bool keepDeducedTemplateParams = false)
		{
			return Push_(newScope, caret, keepDeducedTemplateParams) ? new PopDisposable(this) : null;
		}

		bool Push_(INode newScope, CodeLocation caret, bool keepDeducedTemplateParams = false)
		{
			while (newScope != null && !(newScope is IBlockNode))
				newScope = newScope.Parent as DNode;

			var pop = newScope != null && ScopedBlock != newScope;

			if (pop)
				PushNewScope(newScope as IBlockNode, caret, keepDeducedTemplateParams);

			return pop;
		}

		void PushNewScope(IBlockNode scope, CodeLocation caret, bool keepDeducedTemplateParams = false)
		{
			var cf = new ContextFrame(this);
			IEnumerable<TemplateParameterSymbol> tpsToKeep;

			keepDeducedTemplateParams = keepDeducedTemplateParams && !ScopedBlockIsInNodeHierarchy (scope);

			if (keepDeducedTemplateParams)
				tpsToKeep = DeducedTypesInHierarchy;
			else
				tpsToKeep = null;				

			Push(cf);

			if (keepDeducedTemplateParams) {
				CurrentContext.DeducedTemplateParameters.Add (tpsToKeep);
			}

			cf.Set(scope, caret);
		}

		public void Push(ContextFrame frm)
		{
			stack.Add(frm);
		}

		public List<TemplateParameterSymbol> DeducedTypesInHierarchy
		{
			get
			{
				var parameterSymbols = new List<TemplateParameterSymbol>();
				foreach (var contextFrame in stack)
					parameterSymbols.AddRange(contextFrame.DeducedTemplateParameters);
				return parameterSymbols;
			}
		}

		public bool ContainsDeducedTemplateParameter(TemplateParameter templateParameter)
		{
			foreach (var contextFrame in stack)
				if(contextFrame.DeducedTemplateParameters.ContainsKey(templateParameter))
					return true;
			return false;
		}

		public bool GetTemplateParam(int idHash, out TemplateParameterSymbol tps)
		{
			for (var i = stack.Count - 1; i >= 0; i--)
				foreach (var kv in stack[i].DeducedTemplateParameters)
					if (kv.Key.NameHash == idHash) {
						tps = kv.Value;
						return true;
					}

			tps = null;
			return false;
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

		private class CallStackDisposable : IDisposable
		{
			private readonly ResolutionContext _state;

			public CallStackDisposable(ResolutionContext state) { _state = state; }
			public void Dispose() => _state.evaluationCallStackDepth--;
		}

		public IDisposable PushEvaluationCallStack()
		{
			if (evaluationCallStackDepth >= MAX_CALLSTACK_DEPTH)
				throw new EvaluationStackOverflowException("Stack overflow");

			evaluationCallStackDepth++;
			return new CallStackDisposable(this);
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
			lock(ResolutionErrors)
				ResolutionErrors.Add(err);
			if (ResolutionErrors.Count > maxErrorCount && CompletionOptions.LimitResolutionErrors) {
#if DEBUG
				throw new TooManyResolutionErrors (ResolutionErrors.ToArray ());
#endif
			}
		}

		public void LogError(object syntaxObj, string msg)
		{
			LogError(new ResolutionError(syntaxObj, msg));
		}
		#endregion
	}
}
