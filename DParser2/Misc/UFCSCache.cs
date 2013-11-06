using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver.TypeResolution;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

namespace D_Parser.Misc
{	
	public delegate void UfcsAnalysisFinishedHandler(RootPackage ea);


	/// <summary>
	/// Contains resolution results of methods.
	/// </summary>
	public class UFCSCache
	{
		#region Properties
		int parsingThreads;
		public readonly RootPackage Root;
		AutoResetEvent processingEvent = new AutoResetEvent (true);
		public static bool SingleThreaded = false;
		//ManualResetEvent completedEvent = new ManualResetEvent (true);
		public event UfcsAnalysisFinishedHandler AnalysisFinished;
		public static event UfcsAnalysisFinishedHandler AnyAnalysisFinished;
		
		ConditionalCompilationFlags gFlags_shared;
		Stopwatch sw = new Stopwatch();
		ConcurrentStack<DMethod> queue = new ConcurrentStack<DMethod>();

		public TimeSpan CachingDuration { get { return sw.Elapsed; } }
		private int methodCount;
		public int MethodCacheCount { get { return methodCount; } }

		public readonly ConcurrentDictionary<DMethod, AbstractType> CachedMethods = new ConcurrentDictionary<DMethod, AbstractType>();
		#endregion

		public UFCSCache(RootPackage root)
		{
			this.Root = root;
		}

		/// <summary>
		/// Returns false if cache is already updating.
		/// </summary>
		public bool BeginUpdate(ParseCacheView pcList, ConditionalCompilationFlags compilationEnvironment = null)
		{
			if (!processingEvent.WaitOne (0)) {
				if (Debugger.IsAttached)
					Console.WriteLine ("ufcs analysis already in progress");
				return false;
			}

			gFlags_shared = compilationEnvironment;

			methodCount = 0;
			queue.Clear();

			// Prepare queue
			foreach (var module in Root)
				PrepareQueue(module);

			sw.Restart ();
			if (queue.Count != 0) {
				//completedEvent.Reset();

				if (SingleThreaded)
					parseThread (pcList);
				else {
					var threads = new Thread[GlobalParseCache.NumThreads];
					for (int i = 0; i < GlobalParseCache.NumThreads; i++) {
						var th = threads [i] = new Thread (parseThread) {
							IsBackground = true,
							Priority = ThreadPriority.Lowest,
							Name = "UFCS Analysis thread #" + i
						};
						th.Start (pcList);
					}
				}
			} else
				noticeFinish ();

			return true;
		}

		void PrepareQueue(DModule module)
		{
			if (module != null)
				foreach (var n in module)
				{
					var dm = n as DMethod;

					// UFCS only allows free function that contain at least one parameter
					if (dm == null || dm.NameHash == 0 || dm.Parameters.Count == 0 || dm.Parameters[0].Type == null)
						continue;

					queue.Push(dm);
				}
		}

		void parseThread(object pcl_shared)
		{
			Interlocked.Increment (ref parsingThreads);
			DMethod dm = null;

			try{
				var ctxt = ResolutionContext.Create((ParseCacheView)pcl_shared, gFlags_shared, null);
				int count=0;
				while (queue.TryPop(out dm))
				{
					ctxt.CurrentContext.Set(dm);

					var firstArg_result = TypeDeclarationResolver.Resolve(dm.Parameters[0].Type, ctxt);

					if (firstArg_result != null && firstArg_result.Length != 0)
					{
						count++;
						CachedMethods[dm]= firstArg_result[0];
					}
				}

				Interlocked.Add (ref methodCount, count);
			}
			catch(Exception ex) {
				if (CompletionOptions.Instance.DumpResolutionErrors) {
					Console.WriteLine ("Exception occurred on analysing method \"" + (dm != null ? dm.ToString (true) : "<no method>") + "\":");
					Console.WriteLine (ex.Message);
					Console.WriteLine ("-------------------------------");
					Console.WriteLine ("Stacktrace");
					Console.WriteLine (ex.StackTrace);
					Console.WriteLine ("-------------------------------");
				}
			}
			finally {
				if (Interlocked.Decrement (ref parsingThreads) < 1)
					noticeFinish ();
			}
		}

		void noticeFinish(){
			sw.Stop ();
			//completedEvent.Set ();
			if (AnalysisFinished != null)
				AnalysisFinished (Root);
			if (AnyAnalysisFinished != null)
				AnyAnalysisFinished (Root);
			processingEvent.Set ();
		}

		/*
		public bool WaitForFinish(int millisecondsToWait = -1)
		{
			if(millisecondsToWait < 0)
				return completedEvent.WaitOne();
			return completedEvent.WaitOne(millisecondsToWait);
		}*/

		public void CacheModuleMethods(DModule ast, ResolutionContext ctxt)
		{
			foreach (var m in ast)
				if (m is DMethod)
				{
					var dm = (DMethod)m;

					if (dm.Parameters == null || dm.NameHash == 0 || dm.Parameters.Count == 0 || dm.Parameters[0].Type == null)
						continue;

					ctxt.PushNewScope(dm);
					var firstArg_result = TypeDeclarationResolver.Resolve(dm.Parameters[0].Type, ctxt);
					ctxt.Pop();

					if (firstArg_result != null && firstArg_result.Length != 0)
						CachedMethods[dm] = firstArg_result[0];
				}
		}

		public void RemoveModuleMethods(DModule ast)
		{
			AbstractType t;
			if (ast != null)
				foreach (var m in ast)
					if (m is DMethod)
						CachedMethods.TryRemove (m as DMethod, out t);
			t = null;
		}

		public IEnumerable<DMethod> FindFitting(ResolutionContext ctxt, CodeLocation currentLocation, ISemantic firstArgument, int nameFilterHash = 0)
		{
			if(firstArgument is MemberSymbol)
				firstArgument = DResolver.StripMemberSymbols(firstArgument as AbstractType);

			if (firstArgument is PrimitiveType && 
				(firstArgument as PrimitiveType).TypeToken == D_Parser.Parser.DTokens.Void)
				return null;

			// Then filter out methods which cannot be accessed in the current context 
			// (like when the method is defined in a module that has not been imported)
			var mv = new UfcsMatchScanner(ctxt, CachedMethods, firstArgument, nameFilterHash);

			mv.IterateThroughScopeLayers(currentLocation);

			return mv.filteredMethods;
		}

		class UfcsMatchScanner : AbstractVisitor
		{
			ConcurrentDictionary<DMethod, AbstractType> cache;
			public List<DMethod> filteredMethods = new List<DMethod>();
			int nameFilterHash;
			ISemantic firstArgument;

			public UfcsMatchScanner(ResolutionContext ctxt, 
			    ConcurrentDictionary<DMethod, AbstractType> CachedMethods, 
				ISemantic firstArgument,
				int nameFilterHash = 0)
				: base(ctxt)
			{
				this.firstArgument = firstArgument;
				this.nameFilterHash = nameFilterHash;
				this.cache = CachedMethods;
			}

			public override IEnumerable<INode> PrefilterSubnodes(IBlockNode bn)
			{
				if (bn is DModule)
				{
					if (nameFilterHash == 0)
						return bn.Children;
					return bn [nameFilterHash];
				}
				return null;
			}

			protected override bool HandleItem(INode n)
			{
				AbstractType t;
				if (n is DMethod && 
					(nameFilterHash == 0 || n.NameHash == nameFilterHash) && 
					cache.TryGetValue(n as DMethod, out t) &&
					ResultComparer.IsImplicitlyConvertible(firstArgument, t, ctxt))
					filteredMethods.Add(n as DMethod);

				return false;
			}

			protected override bool HandleItem(PackageSymbol pack)
			{
				return false;
			}
		}
	}
}
