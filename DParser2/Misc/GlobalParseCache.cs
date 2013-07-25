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
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
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
		public readonly int FileAmount;

		public double FileDuration
		{
			get
			{
				if (FileAmount < 1)
					return 0;

				return Duration / FileAmount;
			}
		}

		public ParsingFinishedEventArgs(string dir, RootPackage pack, long duration, int fileCount)
		{
			Directory = dir;
			Package = pack;
			Duration = duration;
			FileAmount = fileCount;
		}
	}
	
	public delegate void ParseFinishedHandler(ParsingFinishedEventArgs ea);
	#endregion

	/// <summary>
	/// Threadsafe global parse cache.
	/// Central storage of scanned directories.
	/// </summary>
	public sealed class GlobalParseCache
	{
		#region Properties
		public static int NumThreads = Environment.ProcessorCount;
		static Thread preparationThread;
		static AutoResetEvent preparationThreadStartEvent = new AutoResetEvent(false);
		static ManualResetEvent parseThreadStartEvent = new ManualResetEvent(false);
		static Thread[] threads;
		static ConcurrentStack<Tuple<string, bool>> basePathQueue = new ConcurrentStack<Tuple<string, bool>>();
		static ConcurrentStack<ParseIntermediate> queue = new ConcurrentStack<ParseIntermediate>();
		static int parsingThreads = 0;
		static ManualResetEvent parseCompletedEvent = new ManualResetEvent(true);
		static bool stopParsing;

		public static bool IsParsing
		{
			get{ return parsingThreads > 0; }
		}

		/// <summary>
		/// Contains phys path -> RootPackage relationships.
		/// </summary>
		static readonly ConcurrentDictionary<string, RootPackage> ParsedDirectories
			= new ConcurrentDictionary<string, RootPackage>();
		static readonly Dictionary<string, StatIntermediate> ParseStatistics
			= new Dictionary<string, StatIntermediate>();

		/// <summary>
		/// Lookup that is used for fast filename-AST lookup. Do NOT modify, it'll be done inside the ModulePackage instances.
		/// </summary>
		internal readonly static ConcurrentDictionary<string, DModule> fileLookup = new ConcurrentDictionary<string, DModule>();

		public static ParseFinishedHandler ParseTaskFinished;

		private GlobalParseCache (){}
		#endregion

		#region Init/Ctor
		static GlobalParseCache()
		{
			preparationThread = new Thread(preparationTh);
			preparationThread.IsBackground = true;
			preparationThread.Name = "Preparation thread";
			preparationThread.Start ();

			threads  = new Thread[NumThreads];
			for(int i=0; i< NumThreads;i++)
			{
				var th = threads[i] = new Thread(parseTh);
				th.IsBackground = true;
				th.Name = "Parse thread #"+i.ToString();
				th.Start(i);
			}
		}

		#endregion

		#region Parsing
		public class StatIntermediate
		{
			public bool skipFunctionBodies;
			public string basePath;

			public int totalFiles;
			public int remainingFiles;
			/// <summary>
			/// Theroetical time needed for parsing all files in one sequence.
			/// </summary>
			public long totalMilliseconds;

			internal ManualResetEvent completed = new ManualResetEvent(false);
			internal System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
			/// <summary>
			/// The time one had to wait until the parse task finished. Milliseconds.
			/// </summary>
			public long actualTimeNeeded{
				get{ return sw.ElapsedMilliseconds; }
			}
		}

		class ParseIntermediate
		{
			public readonly StatIntermediate im;
			public readonly RootPackage root;
			public readonly string file;

			public ParseIntermediate(StatIntermediate im,RootPackage r, string f)
			{
				this.im = im;
				root = r;
				file = f;
			}
		}

		public static void BeginAddOrUpdatePaths(params string[] basePaths)
		{
			BeginAddOrUpdatePaths((IEnumerable<string>)basePaths);
		}

		public static void BeginAddOrUpdatePaths(IEnumerable<string> basePaths, bool skipFunctionBodies = false)
		{
			if (basePaths == null)
				throw new ArgumentNullException ("basePaths");

			GC.Collect();

			parseCompletedEvent.Reset();
			stopParsing = false;

			foreach (var path in basePaths) {
				Interlocked.Increment(ref parsingThreads);
				basePathQueue.Push (new Tuple<string, bool> (path, skipFunctionBodies));
			}
			preparationThreadStartEvent.Set ();
		}

		static void preparationTh()
		{
			while(true)
			{
				if(basePathQueue.IsEmpty)
					preparationThreadStartEvent.WaitOne();

				Tuple<string, bool> tup;
				while(basePathQueue.TryPop(out tup)) {

					if (stopParsing)
						break;

					var path = tup.Item1;
					if (!Directory.Exists (path))
						continue;

					var newRoot = new RootPackage();
					var statIm = new StatIntermediate{ 
						skipFunctionBodies =  tup.Item2, 
						basePath = path
					};
					statIm.sw.Restart();

					lock (ParseStatistics)
						ParseStatistics [path] = statIm;

					try{
					//ISSUE: wild card character ? seems to behave differently across platforms
					// msdn: -> Exactly zero or one character.
					// monodocs: -> Exactly one character.
					var files = Directory.GetFiles (path, "*.d", SearchOption.AllDirectories);
					var ifiles= Directory.GetFiles (path, "*.di", SearchOption.AllDirectories);

					statIm.totalFiles = statIm.remainingFiles = files.Length + ifiles.Length;
					
					if (statIm.totalFiles == 0) {
						noticeFinish(new ParseIntermediate(statIm, newRoot, string.Empty));
						continue;
					}
					
					parseThreadStartEvent.Set();

					if (files.Length != 0)
						for(int i = 0; i < files.Length;i++)
							queue.Push(new ParseIntermediate(statIm,newRoot,files[i]));

					if (ifiles.Length != 0)
						for(int i = 0; i < ifiles.Length;i++)
							queue.Push(new ParseIntermediate(statIm,newRoot,ifiles[i]));
					}catch(Exception ex) {
						Console.WriteLine (ex.Message);
					}

					parseThreadStartEvent.Reset();
				}
			}
		}

		static readonly string phobosDFile = Path.DirectorySeparatorChar + "phobos.d";
		static readonly string indexDFile = Path.DirectorySeparatorChar + "index.d";

		static void parseTh(object s)
		{
			int i = (int)s;
			while (true) {
				if (queue.IsEmpty) {
					//Console.WriteLine("queue empty..waiting (Thread #"+i+")");
					parseThreadStartEvent.WaitOne ();
					//Console.WriteLine("parsethreadstartevent set! (Thread #"+i+")");
				}

				//if(!queue.IsEmpty)	Console.WriteLine("queue not empty..working (Thread #"+i+")");

				ParseIntermediate p;
				while(queue.TryPop(out p))
				{
					if (stopParsing)
						break;

					var im = p.im;

					if (p.file.EndsWith (phobosDFile) || p.file.EndsWith (indexDFile)) {
						if (Interlocked.Decrement(ref im.remainingFiles) <= 0)
							noticeFinish (p);
						continue;
					}

					var sw = new System.Diagnostics.Stopwatch ();

					var code = File.ReadAllText (p.file);

					sw.Start ();

					// If no debugger attached, save time + memory by skipping function bodies
					var ast = DParser.ParseString (code, im.skipFunctionBodies);

					sw.Stop ();

					Interlocked.Add(ref im.totalMilliseconds, sw.ElapsedMilliseconds);

					code = null;

					if (ast != null)
						ast.FileName = p.file;

					if (string.IsNullOrEmpty (ast.ModuleName))
						ast.ModuleName = DModule.GetModuleName (im.basePath, p.file);

					fileLookup[p.file] = ast;

					p.root.GetOrCreateSubPackage (ModuleNameHelper.ExtractPackageName (ast.ModuleName), true)
						.AddModule (ast);

					if (Interlocked.Decrement(ref im.remainingFiles) <= 0)
						noticeFinish (p);
				}

			}
		}

		static void noticeFinish(ParseIntermediate p)
		{
			p.im.sw.Stop ();
			p.im.completed.Set ();
			ParsedDirectories [p.im.basePath] = p.root;
			p.root.TryPreResolveCommonTypes();

			if(ParseTaskFinished!=null)
				ParseTaskFinished(new ParsingFinishedEventArgs(p.im.basePath, p.root, p.im.actualTimeNeeded, p.im.totalFiles));

			if(Interlocked.Decrement(ref parsingThreads) <= 0)
				parseCompletedEvent.Set();
		}

		public static bool WaitForFinish(int millisecondsToWait = -1)
		{
			if(millisecondsToWait < 0)
				return parseCompletedEvent.WaitOne();
			return parseCompletedEvent.WaitOne(millisecondsToWait);
		}

		public static bool WaitForFinish(string basePath,int millisecondsToWait = -1)
		{
			StatIntermediate im;
			if (!ParseStatistics.TryGetValue (basePath, out im))
				return false;

			if(millisecondsToWait < 0)
				return im.completed.WaitOne();
			return im.completed.WaitOne(millisecondsToWait);
		}

		/// <summary>
		/// Stops the scanning. If discardProgress is true, caches will be cleared.
		/// </summary>
		public static void StopScanning(bool discardProgress = false)
		{
			stopParsing = true;
			basePathQueue.Clear();
			queue.Clear ();
		}

		/// <summary>
		/// Returns the parse progress factor (from 0.0 to 1.0); NaN if there's no such directory being scanned.
		/// </summary>
		public static double GetParseProgress(string basePath)
		{
			StatIntermediate im;
			if (!ParseStatistics.TryGetValue (basePath, out im))
				return double.NaN;

			return 1.0 - (im.totalFiles < 1 ? 0.0 : (im.remainingFiles / im.totalFiles));
		}

		public static StatIntermediate GetParseStatistics(string basePath)
		{
			StatIntermediate im;
			ParseStatistics.TryGetValue (basePath, out im);
			return im;
		}

		public static bool UpdateRequired(string basePath)
		{
			return true;
		}
		#endregion

		#region Lookup
		/// <param name="path">Path which may contains a part of some root package's path</param>
		public static RootPackage GetRootPackage(string path)
		{
			foreach (var kv in ParsedDirectories)
				if (path.StartsWith (kv.Key))
					return kv.Value;

			return null;
		}

		public static ModulePackage GetPackage(string physicalRootPath, string subPackageName=null)
		{
			if (string.IsNullOrEmpty (physicalRootPath))
				throw new ArgumentNullException ("physicalRootPath");

			var pack = ParsedDirectories.GetOrAdd (physicalRootPath, (RootPackage)null) as ModulePackage;

			if (pack != null)
				pack = pack.GetOrCreateSubPackage (subPackageName);

			return pack;
		}

		public static ModulePackage GetOrCreatePackageByDirectory(string physicalPackagePath, bool create = false)
		{
			RootPackage root = null;
			foreach (var kv in ParsedDirectories) {
				if (!physicalPackagePath.StartsWith (kv.Key))
					continue;

				root = kv.Value;
				physicalPackagePath = physicalPackagePath.Substring(kv.Key.Length);
				break;
			}

			if (root == null)
				return null;
			
			physicalPackagePath = physicalPackagePath.Trim(Path.DirectorySeparatorChar);

			if (physicalPackagePath == string.Empty)
				return root;

			return root.GetOrCreateSubPackage(physicalPackagePath.Replace(Path.DirectorySeparatorChar,'.'), create);
		}

		public static ModulePackage GetPackage(DModule module, bool create = false)
		{
			var root = GetRootPackage (module.FileName);

			if (root == null)
				return null;

			return root.GetOrCreateSubPackage(ModuleNameHelper.ExtractPackageName(module.ModuleName), create);
		}

		public static DModule GetModule(string basePath, string moduleName, out ModulePackage pack)
		{
			pack = GetPackage (basePath, ModuleNameHelper.ExtractPackageName(moduleName));

			if (pack == null)
				return null;

			return pack.GetModule(ModuleNameHelper.ExtractModuleName(moduleName));
		}

		public static DModule GetModule(string basePath, string moduleName)
		{
			var pack = GetPackage (basePath, ModuleNameHelper.ExtractPackageName(moduleName));

			if (pack == null)
				return null;

			return pack.GetModule(ModuleNameHelper.ExtractModuleName(moduleName));
		}

		public static DModule GetModule(string file)
		{
			DModule ret;
			if (!fileLookup.TryGetValue (file, out ret)) {
				foreach (var kv in ParsedDirectories) {
					if (!file.StartsWith (kv.Key))
						continue;

					var modName = DModule.GetModuleName(kv.Key, file);
					var pack = kv.Value.GetOrCreateSubPackage(ModuleNameHelper.ExtractPackageName(modName));

					if(pack != null)
						ret = pack.GetModule(modName);
					break;
				}
			}

			return ret;
		}

		public static IEnumerable<ModulePackage> EnumPackages(params string[] basePaths)
		{
			return null;
		}

		public static IEnumerable<DModule> EnumModules(string basePath, string packageName)
		{
			return null;
		}
		#endregion

		#region Module management
		public static void AddOrUpdateModule(DModule module)
		{
			if (module == null || string.IsNullOrEmpty(module.ModuleName))
				return;

			var pack = GetPackage (module, true);
			var file = module.FileName;

			// Check if a module is already in the file lookup
			DModule oldMod;
			if (file != null && fileLookup.TryRemove(file, out oldMod))
			{
				if (oldMod.FileName != module.FileName)
					RemoveModule (oldMod);
				else if (oldMod.ModuleName != module.ModuleName)
					RemoveModule (oldMod, pack);
				oldMod = null;
			}

			pack.AddModule (module);
			fileLookup[file] = module;
		}

		public static bool RemoveModule(string file)
		{
			return RemoveModule (GetModule (file));
		}

		public static bool RemoveModule(string basePath, string moduleName)
		{
			ModulePackage pack;
			return RemoveModule (GetModule(basePath, moduleName, out pack), pack);
		}

		public static bool RemoveModule(DModule module)
		{
			return RemoveModule (module, GetPackage (module));
		}

		internal static bool RemoveModule(DModule ast, ModulePackage pack)
		{
			if (ast == null || pack == null)
				return false;

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

