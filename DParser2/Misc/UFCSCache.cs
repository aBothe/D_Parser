using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver.TypeResolution;
using System.Runtime.CompilerServices;

namespace D_Parser.Misc
{
	/// <summary>
	/// Contains resolution results of methods.
	/// </summary>
	public class UFCSCache
	{
		#region Properties
		public bool IsProcessing { get; private set; }
		public static bool SingleThreaded = false;
		
		ConditionalCompilationFlags gFlags_shared;
		Stack<DMethod> queue = new Stack<DMethod>();
		public readonly ConditionalWeakTable<DMethod, AbstractType> CachedMethods = new ConditionalWeakTable<DMethod, AbstractType>();
		/// <summary>
		/// Returns time span needed to resolve all first parameters.
		/// </summary>
		public TimeSpan CachingDuration { get; private set; }
		private int methodCount;
		/// <summary>
		/// 
		/// </summary>
		public int MethodCacheCount { get { return methodCount; } }
		#endregion

		/// <summary>
		/// Returns false if cache is already updating.
		/// </summary>
		public bool Update(ParseCacheList pcList, ConditionalCompilationFlags compilationEnvironment = null, ParseCache subCacheToUpdate = null)
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
				if (subCacheToUpdate != null)
					foreach (var module in subCacheToUpdate)
						PrepareQueue(module);
				else
					foreach (var pc in pcList)
						foreach (var module in pc)
							PrepareQueue(module);

				if(queue.Count != 0)
				{
					var sw = new Stopwatch();
					sw.Start();
	
					if(SingleThreaded)
						parseThread(pcList);
					else
					{
						var threads = new Thread[ThreadedDirectoryParser.numThreads];
						for (int i = 0; i < ThreadedDirectoryParser.numThreads; i++)
						{
							var th = threads[i] = new Thread(parseThread)
							{
								IsBackground = true,
								Priority = ThreadPriority.Lowest,
								Name = "UFCS Analysis thread #" + i
							};
							th.Start(pcList);
						}
		
						for (int i = 0; i < ThreadedDirectoryParser.numThreads; i++)
							if (threads[i].IsAlive)
								threads[i].Join(10000);
					}
	
					sw.Stop();
					CachingDuration = sw.Elapsed;
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
			DMethod dm = null;
			var ctxt = ResolutionContext.Create((ParseCacheList)pcl_shared, gFlags_shared, null);

			while (queue.Count != 0)
			{
				lock (queue)
				{
					if (queue.Count == 0)
						return;

					dm = queue.Pop();
				}

				ctxt.CurrentContext.Set(dm);

				var firstArg_result = TypeDeclarationResolver.Resolve(dm.Parameters[0].Type, ctxt);

				if (firstArg_result != null && firstArg_result.Length != 0)
				{
					methodCount++;
					CachedMethods.Remove(dm);
					CachedMethods.Add(dm, firstArg_result[0]);
				}
			}
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
						CachedMethods.Remove(dm);
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
