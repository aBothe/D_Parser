//
// GlobalParseCache.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2013 Alexander Bothe
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using D_Parser.Dom;
using D_Parser.Parser;
using System.Linq;

namespace D_Parser.Misc
{
	#region Event meta info
	public class ParsingFinishedEventArgs
	{
		public readonly string Directory;
		public readonly RootPackage Package;
		/// <summary>
		/// Milliseconds.
		/// </summary>
		public readonly long Duration;
		public readonly long ParseDuration;
		public readonly int FileAmount;

		public double FileDuration {
			get {
				if (FileAmount < 1)
					return 0;

				return Duration / FileAmount;
			}
		}

		public double FileParseDuration {
			get {
				if (FileAmount < 1)
					return 0;

				return ParseDuration / FileAmount;
			}
		}

		public ParsingFinishedEventArgs (string dir, RootPackage pack, long duration, long parseDuration, int fileCount)
		{
			Directory = dir;
			Package = pack;
			Duration = duration;
			ParseDuration = parseDuration;
			FileAmount = fileCount;
		}
	}
	public delegate void ParseFinishedHandler (ParsingFinishedEventArgs ea);
	#endregion
	/// <summary>
	/// Threadsafe global parse cache.
	/// Central storage of scanned directories.
	/// </summary>
	public static class GlobalParseCache
	{
		#region Properties

		public static readonly int NumThreads = Environment.ProcessorCount - 1 > 0 ? (Environment.ProcessorCount - 1 <= 3 ? Environment.ProcessorCount - 1 : 3) : 1;
		static Thread preparationThread;
		static readonly AutoResetEvent preparationThreadStartEvent = new AutoResetEvent (false);
		static readonly ManualResetEvent parseThreadStartEvent = new ManualResetEvent (false);
		static readonly Thread[] threads = new Thread[NumThreads];

		static readonly ConcurrentStack<ParseStatisticsHandle> basePathQueue = new ConcurrentStack<ParseStatisticsHandle> ();
		static readonly ConcurrentStack<FileParseQueueEntry> queue = new ConcurrentStack<FileParseQueueEntry> ();

		/// <summary>
		/// Contains phys path -> RootPackage relationships.
		/// </summary>
		static readonly ConcurrentDictionary<string, RootPackage> ParsedDirectories = new ConcurrentDictionary<string, RootPackage> ();
		/// <summary>
		/// Lookup that is used for fast filename-AST lookup. Do NOT modify, it'll be done inside the ModulePackage instances.
		/// </summary>
		internal readonly static ConditionalWeakTable<string, DModule> fileLookup = new ConditionalWeakTable<string, DModule> ();
		public static ParseFinishedHandler ParseTaskFinished;

		#endregion

		#region Parsing
		static AutoResetEvent launchEvent = new AutoResetEvent(true);
		static AutoResetEvent parseThreadLaunchevent = new AutoResetEvent(true);

		static void LaunchPreparationThread()
		{
			if(launchEvent.WaitOne(0)){
				if (preparationThread == null || !preparationThread.IsAlive) {
					preparationThread = new Thread (preparationTh) {
						IsBackground = true,
						Name = "Preparation thread",
						Priority = ThreadPriority.BelowNormal
					};
					preparationThread.Start ();
				}
				launchEvent.Set ();
			}
		}

		static void LaunchParseThreads()
		{
			if (parseThreadLaunchevent.WaitOne (0)) {

				for (int i= NumThreads -1 ; i>=0; i--) {
					if (threads [i] == null || !threads [i].IsAlive) {
						(threads [i] = new Thread (parseTh) {
							IsBackground = true,
							Name = "Parse thread #" + i.ToString (),
							Priority = ThreadPriority.Lowest
						}).Start();
					}
				}

				parseThreadLaunchevent.Set ();
			}
		}

		internal class ParseSubtaskContainer
		{
			public ParseSubtaskContainer (int remainingPathsToBeParsed, ParseFinishedHandler f,
				CancellationToken cancellationToken)
			{
				RemainingPathsToBeParsed = remainingPathsToBeParsed;
				finishedHandler = f;
				CancellationToken = cancellationToken;
			}

			public readonly ParseFinishedHandler finishedHandler;
			public CancellationToken CancellationToken;
			public int RemainingPathsToBeParsed;
		}

		public class ParseStatisticsHandle
		{
			public readonly bool skipFunctionBodies;
			public readonly string[] TaskTokens;
			public readonly string basePath;
			public int totalFiles { get; internal set; }
			internal int remainingFiles;
			public int RemainingFiles => remainingFiles;
			internal long totalMilliseconds;
			/// <summary>
			/// Theroetical time needed for parsing all files in one sequence.
			/// </summary>
			public long TotalMilliseconds => totalMilliseconds;

			internal readonly ManualResetEvent completed = new ManualResetEvent (false);
			internal readonly Stopwatch sw = new Stopwatch ();
			internal readonly ParseSubtaskContainer pathsParsingHandle;

			internal ParseStatisticsHandle(string basePath, bool skipFunctionBodies,
				string[] taskTokens, ParseSubtaskContainer pathsParsingHandle)
			{
				this.basePath = basePath;
				this.skipFunctionBodies = skipFunctionBodies;
				this.pathsParsingHandle = pathsParsingHandle;
				TaskTokens = taskTokens;
			}

			/// <summary>
			/// The time one had to wait until the parse task finished. Milliseconds.
			/// </summary>
			public long actualTimeNeeded => sw.ElapsedMilliseconds;

			/// <summary>
			/// The time one had to wait until the actual parse process (which excludes file content loading, thread synchronizing etc) finished. Milliseconds.
			/// </summary>
			public long ActualParseTimeNeeded => totalMilliseconds / NumThreads;

			public double ParseProgess => 1.0 - (totalFiles < 1 ? 0.0 : (remainingFiles / totalFiles));

			public override bool Equals (object obj)
			{
				return obj is ParseStatisticsHandle intermediate && intermediate.basePath == basePath;
			}

			public override int GetHashCode ()
			{
				return basePath != null ? basePath.GetHashCode () : base.GetHashCode();
			}

			public bool WaitForCompletion(int millisecondTimeout = -1)
			{
				return millisecondTimeout == -1 ? completed.WaitOne() : completed.WaitOne(millisecondTimeout);
			}
		}

		class FileParseQueueEntry
		{
			public readonly ParseStatisticsHandle Statistics;
			public readonly RootPackage root;
			public readonly string file;

			public FileParseQueueEntry (ParseStatisticsHandle im, RootPackage r, string f)
			{
				this.Statistics = im;
				root = r;
				file = f;
			}
		}

		public static List<ParseStatisticsHandle> BeginAddOrUpdatePaths (params string[] basePaths)
		{
			return BeginAddOrUpdatePaths ((IEnumerable<string>)basePaths);
		}

		public static List<ParseStatisticsHandle> BeginAddOrUpdatePaths (IEnumerable<string> basePaths,
			bool skipFunctionBodies = false, ParseFinishedHandler finishedHandler = null, string[] taskTokens = null,
			CancellationToken? cancellationToken = null)
		{
			if (basePaths == null)
				throw new ArgumentNullException (nameof(basePaths));

			GC.Collect ();

#if TRACE
			Trace.WriteLine("BeginAddOrUpdatePaths: ");
			Trace.Indent();
			foreach (var p in basePaths)
				Trace.WriteLine(p);
			Trace.Unindent();
			Trace.WriteLine("---------");
#endif

			var countObj = new ParseSubtaskContainer (basePaths.Count (), finishedHandler,
				cancellationToken ?? CancellationToken.None);
			var parsedPathsStatistics = new List<ParseStatisticsHandle>();

			foreach (var path in basePaths)
			{
				var handle = new ParseStatisticsHandle(path, skipFunctionBodies, taskTokens, countObj);
				parsedPathsStatistics.Add(handle);
				basePathQueue.Push (handle);
			}

			if (parsedPathsStatistics.Count == 0)
			{
				noticeFinish(new ParseStatisticsHandle(null, false, null, countObj), null);
			}
			else
			{
				preparationThreadStartEvent.Set ();
				LaunchPreparationThread ();
			}

			return parsedPathsStatistics;
		}

		const int ThreadWaitTimeout = 5000;

		static void preparationTh ()
		{
			for(;;) {
				if (basePathQueue.IsEmpty && !preparationThreadStartEvent.WaitOne (ThreadWaitTimeout))
					return;

				while (basePathQueue.TryPop(out var statIm)) {
					var path = statIm.basePath;
					if (!Directory.Exists(path))
					{
						noticeFinish (statIm, null);
						continue;
					}

					// Check if it's necessary to reparse the directory
					if (ParsedDirectories.TryGetValue (path, out var oldRoot) &&
						oldRoot.LastParseTime >= Directory.GetLastWriteTimeUtc (path)) {
						noticeFinish (statIm, oldRoot);
						continue;
					}

					statIm.sw.Restart ();

					string[] files, ifiles;
					try
					{
						//ISSUE: wild card character ? seems to	behave differently across platforms
						// msdn: ->	Exactly	zero or	one	character.
						// monodocs: ->	Exactly	one	character.
						files =	Directory.GetFiles(path, "*.d",	SearchOption.AllDirectories);
						ifiles = Directory.GetFiles(path, "*.di", SearchOption.AllDirectories);
					}
					catch (UnauthorizedAccessException)
					{
						files = new string[0];
						ifiles = new string[0];
					}

					statIm.totalFiles = statIm.remainingFiles = files.Length + ifiles.Length;

					var newRoot = new RootPackage { LastParseTime = Directory.GetLastWriteTimeUtc(path)};
					
					if (statIm.totalFiles == 0)
					{
						noticeFinish (statIm, newRoot);
						continue;
					}
					
					parseThreadStartEvent.Set();

					var entriesToPush = new FileParseQueueEntry[files.Length + ifiles.Length];
					var nextEntryToSet = 0;
					foreach (var file in files)
						entriesToPush[nextEntryToSet++] = new FileParseQueueEntry(statIm, newRoot, file);
					foreach (var ifile in ifiles)
						entriesToPush[nextEntryToSet++] = new FileParseQueueEntry(statIm, newRoot, ifile);
					queue.PushRange(entriesToPush);

					LaunchParseThreads();

					parseThreadStartEvent.Reset ();
				}
			}
		}

		static readonly string phobosDFile = Path.DirectorySeparatorChar + "phobos.d";
		static readonly string indexDFile = Path.DirectorySeparatorChar + "index.d";

		static void parseTh ()
		{
			var someFileParseEntries = new FileParseQueueEntry[5];
			for(;;) {
				if (queue.IsEmpty && !parseThreadStartEvent.WaitOne (ThreadWaitTimeout))
					return;

				for (int readEntries = queue.TryPopRange(someFileParseEntries); readEntries > 0; readEntries--)
				{
					var p = someFileParseEntries[readEntries - 1];

					ParseSingleFileQueueEntry(p);

					if (Interlocked.Decrement (ref p.Statistics.remainingFiles) <= 0)
						noticeFinish (p.Statistics, p.root);
				}
			}
		}

		private static void ParseSingleFileQueueEntry(FileParseQueueEntry p)
		{
			if (p.file.EndsWith(phobosDFile) || p.file.EndsWith(indexDFile))
				return;

			var im = p.Statistics;
			if (im.pathsParsingHandle.CancellationToken.IsCancellationRequested)
				return;

			var sw = new Stopwatch();
			DModule ast;
			try
			{
				var code = File.ReadAllText(p.file);

				sw.Restart();

				// If no debugger attached, save time + memory by skipping function bodies
				ast = DParser.ParseString(code, im.skipFunctionBodies, true, im.TaskTokens);
			}
			catch (Exception ex)
			{
				ast = null;

				Console.WriteLine("Exception occurred on \"" + p.file + "\":");
				Console.WriteLine(ex.Message);
				Console.WriteLine("-------------------------------");
				Console.WriteLine("Stacktrace");
				Console.WriteLine(ex.StackTrace);
				Console.WriteLine("-------------------------------");
			}

			sw.Stop();

			Interlocked.Add(ref im.totalMilliseconds, sw.ElapsedMilliseconds);

			if (ast != null)
			{
				AddParsedModuleToParseIntermediate(p, im, ast);
			}
		}

		private static void AddParsedModuleToParseIntermediate(FileParseQueueEntry p, ParseStatisticsHandle im, DModule ast)
		{
			ast.FileName = p.file;

			if (string.IsNullOrEmpty(ast.ModuleName))
				ast.ModuleName = DModule.GetModuleName(im.basePath, p.file);

			fileLookup.Remove(p.file);
			fileLookup.Add(p.file, ast);

			p.root.GetOrCreateSubPackage(ModuleNameHelper.ExtractPackageName(ast.ModuleName), true)
				.AddModule(ast);
		}

		static void noticeFinish (ParseStatisticsHandle statisticsHandle, RootPackage newRootPackage)
		{
			var im = statisticsHandle;

			im.sw.Stop ();

			if (!string.IsNullOrEmpty (im.basePath) && newRootPackage != null) {
				ParsedDirectories [im.basePath] = newRootPackage;
			}
			im.completed.Set();

			var pf = new ParsingFinishedEventArgs (im.basePath, newRootPackage,
				im.actualTimeNeeded, im.ActualParseTimeNeeded, im.totalFiles);
			ParseTaskFinished?.Invoke(pf);

			if (Interlocked.Decrement (ref im.pathsParsingHandle.RemainingPathsToBeParsed) < 1)
				im.pathsParsingHandle.finishedHandler?.Invoke (pf);
		}

		public static bool RemoveRoot (string basePath)
		{
			return ParsedDirectories.TryRemove (basePath, out _);
		}

		#endregion

		#region Lookup

		/// <param name="path">Path which may contains a part of some root package's path</param>
		public static RootPackage GetRootPackage (string path)
		{
			if (path == null)
				return null;

			foreach (var kv in ParsedDirectories)
				if (path.StartsWith (kv.Key))
					return kv.Value;

			return null;
		}

		public static ModulePackage GetPackage (string physicalRootPath, string subPackageName=null)
		{
			if (string.IsNullOrEmpty (physicalRootPath))
				throw new ArgumentNullException (nameof(physicalRootPath));

			var pack = ParsedDirectories.GetOrAdd (physicalRootPath, (RootPackage)null) as ModulePackage;

			return pack?.GetOrCreateSubPackage (subPackageName);
		}

		public static ModulePackage GetOrCreatePackageByDirectory (string physicalPackagePath, bool create = false)
		{
			RootPackage root = null;
			foreach (var kv in ParsedDirectories) {
				if (!physicalPackagePath.StartsWith (kv.Key))
					continue;

				root = kv.Value;
				physicalPackagePath = physicalPackagePath.Substring (kv.Key.Length);
				break;
			}

			if (root == null)
				return null;
			
			physicalPackagePath = physicalPackagePath.Trim (Path.DirectorySeparatorChar);

			if (physicalPackagePath == string.Empty)
				return root;

			return root.GetOrCreateSubPackage (physicalPackagePath.Replace (Path.DirectorySeparatorChar, '.'), create);
		}

		public static ModulePackage GetPackage (DModule module, bool create = false)
		{
			if (module == null)
				return null;

			var root = GetRootPackage (module.FileName);

			return root?.GetOrCreateSubPackage (ModuleNameHelper.ExtractPackageName (module.ModuleName), create);
		}

		public static DModule GetModule (string basePath, string moduleName, out ModulePackage pack)
		{
			pack = GetPackage (basePath, ModuleNameHelper.ExtractPackageName (moduleName));

			return pack?.GetModule (ModuleNameHelper.ExtractModuleName (moduleName));
		}

		public static DModule GetModule (string basePath, string moduleName)
		{
			var pack = GetPackage (basePath, ModuleNameHelper.ExtractPackageName (moduleName));

			return pack?.GetModule (ModuleNameHelper.ExtractModuleName (moduleName));
		}

		public static DModule GetModule (string file)
		{
			if (fileLookup.TryGetValue(file, out var ret))
				return ret;

			foreach (var kv in ParsedDirectories) {
				if (!file.StartsWith (kv.Key))
					continue;

				var modName = DModule.GetModuleName (kv.Key, file);
				var pack = kv.Value.GetOrCreateSubPackage (ModuleNameHelper.ExtractPackageName (modName));

				if (pack != null)
					ret = pack.GetModule(ModuleNameHelper.ExtractModuleName(modName));
				break;
			}

			return ret;
		}

		public static List<ModulePackage> EnumPackagesRecursively (bool includeRoots,params string[] basePaths)
		{
			var l = new List<ModulePackage>();
			foreach (var path in basePaths)
			{
				var root = GetRootPackage(path);
				if (root == null)
					continue;

				if (includeRoots)
					l.Add(root);

				EnumPackagesRecursively(root,l);
			}
			return l;
		}

		public static void EnumPackagesRecursively(ModulePackage pack, List<ModulePackage> list)
		{
			if (pack == null) return;

			foreach (var kv in pack.packages)
			{
				list.Add(kv.Value);
				EnumPackagesRecursively(kv.Value, list);
			}
		}

		public static List<DModule> EnumModulesRecursively (string basePath, string packageName=null)
		{
			var l = new List<DModule>();
			var pack = GetRootPackage(basePath);

			if (pack == null)
				return l;

			EnumModulesRecursively(pack, l);
			return l;
		}

		public static void EnumModulesRecursively(ModulePackage pack, List<DModule> list)
		{
			if (pack != null)
			{
				list.AddRange(pack.modules.Values);
				foreach (var sub in pack.packages)
					EnumModulesRecursively(sub.Value, list);
			}
		}
		#endregion

		#region Module management

		public static bool AddOrUpdateModule (DModule module)
		{
			ModulePackage p;
			return AddOrUpdateModule (module, out p);
		}

		public static bool AddOrUpdateModule (DModule module, out ModulePackage pack)
		{
			pack = null;
			if (module == null || string.IsNullOrEmpty (module.ModuleName))
				return false;

			pack = GetPackage (module, true);

			if (pack == null)
				return false;

			var file = module.FileName;

			// Check if a module is already in the file lookup
			if (file != null && fileLookup.TryGetValue (file, out var oldMod)) {
				RemoveModule (oldMod);
				oldMod = null;
			}

			pack.AddModule (module);
			fileLookup.Add (file, module);
			return true;
		}

		public static bool RemoveModule (string file)
		{
			return RemoveModule (GetModule (file));
		}

		public static bool RemoveModule (string basePath, string moduleName)
		{
			return RemoveModule (GetModule (basePath, moduleName, out var pack), pack);
		}

		public static bool RemoveModule (DModule module)
		{
			return RemoveModule (module, GetPackage (module));
		}

		internal static bool RemoveModule (DModule ast, ModulePackage pack)
		{
			if (ast == null || pack == null)
				return false;

			fileLookup.Remove (ast.FileName);

			if (!pack.RemoveModule (ast.ModuleName ?? ""))
				return false;

			ModulePackage parPack;
			if (pack.IsEmpty && (parPack = pack.Parent) != null)
				parPack.RemovePackage (pack.Name);

			return true;
		}

		#endregion

	}
}

