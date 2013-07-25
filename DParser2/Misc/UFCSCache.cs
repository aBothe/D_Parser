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
		public bool IsProcessing { get; private set; }
		public static bool SingleThreaded = false;
		ManualResetEvent completedEvent = new ManualResetEvent (true);
		public event UfcsAnalysisFinishedHandler AnalysisFinished;
		public static event UfcsAnalysisFinishedHandler AnyAnalysisFinished;
		
		ConditionalCompilationFlags gFlags_shared;
		Stopwatch sw = new Stopwatch();
		ConcurrentStack<DMethod> queue = new ConcurrentStack<DMethod>();

		public TimeSpan CachingDuration { get { return sw.Elapsed; } }
		private int methodCount;
		public int MethodCacheCount { get { return methodCount; } }

		public readonly ConditionalWeakTable<DMethod, AbstractType> CachedMethods = new ConditionalWeakTable<DMethod, AbstractType>();
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
			if (IsProcessing)
				return false;

			try
			{
				IsProcessing = true;
				gFlags_shared = compilationEnvironment;

				methodCount = 0;
				queue.Clear();

				// Prepare queue
				foreach (var module in Root)
					PrepareQueue(module);

				if(queue.Count != 0)
				{
					completedEvent.Reset();
					sw.Restart();
	
					if(SingleThreaded)
						parseThread(pcList);
					else
					{
						var threads = new Thread[GlobalParseCache.NumThreads];
						for (int i = 0; i < GlobalParseCache.NumThreads; i++)
						{
							var th = threads[i] = new Thread(parseThread)
							{
								IsBackground = true,
								Priority = ThreadPriority.BelowNormal,
								Name = "UFCS Analysis thread #" + i
							};
							th.Start(pcList);
						}
					}
	
					sw.Stop();
				}
			}
			finally
			{
				IsProcessing = false;
			}
			return true;
		}

		void PrepareQueue(DModule module)
		{
			if (module != null)
				foreach (var n in module)
				{
					var dm = n as DMethod;

					// UFCS only allows free function that contain at least one parameter
					if (dm == null || dm.Parameters.Count == 0 || dm.Parameters[0].Type == null)
						continue;

					queue.Push(dm);
				}
		}

		void parseThread(object pcl_shared)
		{
			Interlocked.Increment (ref parsingThreads);
			DMethod dm;
			var ctxt = ResolutionContext.Create((ParseCacheView)pcl_shared, gFlags_shared, null);
			int count=0;
			while (queue.TryPop(out dm))
			{
				ctxt.CurrentContext.Set(dm);

				var firstArg_result = TypeDeclarationResolver.Resolve(dm.Parameters[0].Type, ctxt);

				if (firstArg_result != null && firstArg_result.Length != 0)
				{
					count++;
					CachedMethods.Remove (dm);
					CachedMethods.Add(dm, firstArg_result[0]);
				}
			}

			Interlocked.Add (ref methodCount, count);
			if (Interlocked.Decrement (ref parsingThreads) < 1) {
				completedEvent.Set ();
				if (AnalysisFinished != null)
					AnalysisFinished (Root);
				if (AnyAnalysisFinished != null)
					AnyAnalysisFinished (Root);
			}
		}

		public bool WaitForFinish(int millisecondsToWait = -1)
		{
			if(millisecondsToWait < 0)
				return completedEvent.WaitOne();
			return completedEvent.WaitOne(millisecondsToWait);
		}

		public void CacheModuleMethods(DModule ast, ResolutionContext ctxt)
		{
			foreach (var m in ast)
				if (m is DMethod)
				{
					var dm = (DMethod)m;

					if (dm.Parameters == null || dm.Parameters.Count == 0 || dm.Parameters[0].Type == null)
						continue;

					ctxt.PushNewScope(dm);
					var firstArg_result = TypeDeclarationResolver.Resolve(dm.Parameters[0].Type, ctxt);
					ctxt.Pop();

					if (firstArg_result != null && firstArg_result.Length != 0)
					{
						CachedMethods.Remove (dm);
						CachedMethods.Add(dm, firstArg_result[0]);
					}
				}
		}

		public IEnumerable<DMethod> FindFitting(ResolutionContext ctxt, CodeLocation currentLocation, ISemantic firstArgument, string nameFilter = null)
		{
			if (IsProcessing)
				return null;

			if(firstArgument is MemberSymbol)
				firstArgument = DResolver.StripMemberSymbols(firstArgument as AbstractType);

			if (firstArgument is PrimitiveType && 
				(firstArgument as PrimitiveType).TypeToken == D_Parser.Parser.DTokens.Void)
				return null;

			// Then filter out methods which cannot be accessed in the current context 
			// (like when the method is defined in a module that has not been imported)
			var mv = new UfcsMatchScanner(ctxt, CachedMethods, firstArgument, nameFilter);

			mv.IterateThroughScopeLayers(currentLocation);

			return mv.filteredMethods;
		}

		class UfcsMatchScanner : AbstractVisitor
		{
			ConditionalWeakTable<DMethod, AbstractType> cache;
			public List<DMethod> filteredMethods = new List<DMethod>();
			string nameFilter;
			ISemantic firstArgument;

			public UfcsMatchScanner(ResolutionContext ctxt, 
				ConditionalWeakTable<DMethod, AbstractType> CachedMethods, 
				ISemantic firstArgument,
				string nameFilter = null)
				: base(ctxt)
			{
				this.firstArgument = firstArgument;
				this.nameFilter = nameFilter;
				this.cache = CachedMethods;
			}

			public override IEnumerable<INode> PrefilterSubnodes(IBlockNode bn)
			{
				if (bn is DModule)
				{
					foreach (var n in bn)
					{
						if (n is DMethod && (nameFilter == null || nameFilter == n.Name))
							yield return n;
					}
				}

				yield break;
			}

			protected override bool HandleItem(INode n)
			{
				AbstractType t;
				if (n is DMethod && 
					(nameFilter == null || nameFilter == n.Name) && 
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
