using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using D_Parser.Dom;
using D_Parser.Parser;
using System.Collections.Concurrent;

namespace D_Parser.Misc
{
	/// <summary>
	/// Helper class which scans through source directories.
	/// For better performance, this will be done using at least one thread.
	/// </summary>
	public class ThreadedDirectoryParser
	{
		#region Properties
		public static int numThreads = Environment.ProcessorCount;

		public Exception LastException;
		string baseDirectory;
		bool stillQueuing = false;
		int fileCount = 0;
		long totalMSecs = 0;
		bool skipFunctionBodies = true;//!Debugger.IsAttached;
		ConcurrentStack<Tuple<string, ModulePackage>> queue = new ConcurrentStack<Tuple<string, ModulePackage>>();
		#endregion

		public static ParsePerformanceData Parse (string directory, RootPackage rootPackage, bool sync = false)
		{
			var ppd = new ParsePerformanceData { BaseDirectory = directory };

			if (!Directory.Exists (directory))
				return ppd;

			var tpd = new ThreadedDirectoryParser
			{
				baseDirectory = directory,
				stillQueuing = true
			};

			Stopwatch sw;

			if (sync) {
				tpd.PrepareQueue (rootPackage);
				sw = new Stopwatch ();
				sw.Start();
				tpd.ParseThread();
				sw.Stop();
			} else {
				var threads = new Thread[numThreads];
				for (int i = 0; i < numThreads; i++) {
					var th = threads [i] = new Thread (tpd.ParseThread)
				{
					IsBackground = true,
					Priority = ThreadPriority.Lowest,
					Name = "Parser thread #" + i + " (" + directory + ")"
				};
					th.Start ();
				}

				sw = new Stopwatch ();
				sw.Start ();

				tpd.PrepareQueue (rootPackage);
			
				for (int i = 0; i < numThreads; i++)
					if (threads [i].IsAlive)
						threads [i].Join (10000);

				sw.Stop ();
			}

			ppd.AmountFiles = tpd.fileCount;
			ppd.TotalDuration = (double)tpd.totalMSecs/1000d;//sw.Elapsed.TotalSeconds;

			return ppd;
		}

		void PrepareQueue(RootPackage root)
		{
			if (!Directory.Exists(baseDirectory))
			{
				stillQueuing = false;
				return; 
			}

			totalMSecs = 0;
			fileCount = 0;
			stillQueuing = true;

			//ISSUE: wild card character ? seems to behave differently across platforms
			// msdn: -> Exactly zero or one character.
			// monodocs: -> Exactly one character.
			var dFiles = Directory.GetFiles(baseDirectory, "*.d", SearchOption.AllDirectories);
			var diFiles = Directory.GetFiles(baseDirectory, "*.di", SearchOption.AllDirectories);
			var files = new string[dFiles.Length + diFiles.Length];
			if(files.Length==0)
			{
				stillQueuing = false;
				return;
			}
			Array.Copy(dFiles, 0, files, 0, dFiles.Length);
			Array.Copy(diFiles, 0, files, dFiles.Length, diFiles.Length);

			var lastPack = (ModulePackage)root;
			var lastDir = baseDirectory;

			bool isPhobosRoot = this.baseDirectory.EndsWith(Path.DirectorySeparatorChar + "phobos");

			foreach (var file in files)
			{
				var modulePath = DModule.GetModuleName(baseDirectory, file);

				if (lastDir != (lastDir = Path.GetDirectoryName(file)))
				{
					isPhobosRoot = this.baseDirectory.EndsWith(Path.DirectorySeparatorChar + "phobos");

					var packName = ModuleNameHelper.ExtractPackageName(modulePath);
					lastPack = root.GetOrCreateSubPackage(packName, true);
				}

				// Skip index.d (D2) || phobos.d (D2|D1)
				if (isPhobosRoot && (file.EndsWith("index.d") || file.EndsWith("phobos.d")))
					continue;

				fileCount++;
				queue.Push(new Tuple<string, ModulePackage>(file, lastPack));
			}

			stillQueuing = false;
		}

		void ParseThread()
		{
			var sw = new Stopwatch();

			while (queue.Count != 0 || stillQueuing)
			{
				Tuple<string, ModulePackage> kv;
				if(!queue.TryPop(out kv))
				{
					if(stillQueuing)
					{
						Thread.Sleep(1);
						continue;
					}
					break;
				}

				var file = kv.Item1;
				var pack = kv.Item2;

				var code = File.ReadAllText(file);


				sw.Start();
				IAbstractSyntaxTree ast=null;
				try
				{
					// If no debugger attached, save time + memory by skipping function bodies
					ast = DParser.ParseString(code, skipFunctionBodies);
					code = null;
				}
				catch (Exception ex)
				{
					ast = new DModule{ ParseErrors = new System.Collections.ObjectModel.ReadOnlyCollection<ParserError>(
						new List<ParserError>{
							new ParserError(false, ex.Message + "\n\n" + ex.StackTrace, DTokens.Invariant, CodeLocation.Empty)
					       }) };
					LastException = ex;
				}
				finally
				{
					ast.FileName = file;
				}
				sw.Stop();
				
				if(!string.IsNullOrEmpty(ast.ModuleName))
				{
					if (pack is RootPackage)
						ast.ModuleName = Path.GetFileNameWithoutExtension(file);
					else
						ast.ModuleName = pack.Path + "." + Path.GetFileNameWithoutExtension(file);
				}

				ast.FileName = file;
				lock(pack)
					pack.AddModule(ast);
			}

			totalMSecs+=sw.ElapsedMilliseconds;
		}
	}
}
