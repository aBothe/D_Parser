using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using D_Parser.Dom;
using D_Parser.Parser;

namespace D_Parser.Misc
{
	/// <summary>
	/// Helper class which scans through source directories.
	/// For better performance, this will be done using at least one thread.
	/// </summary>
	public class ThreadedDirectoryParser
	{
		#region Properties
		public static int numThreads = Debugger.IsAttached ? 1 : Environment.ProcessorCount;

		public Exception LastException;
		string baseDirectory;
		bool stillQueuing = false;
		int fileCount = 0;
		bool skipFunctionBodies = !Debugger.IsAttached;
		Stack<Tuple<string, ModulePackage>> queue = new Stack<Tuple<string, ModulePackage>>(16);
		#endregion

		public static ParsePerformanceData Parse(string directory, RootPackage rootPackage)
		{
			var ppd = new ParsePerformanceData { BaseDirectory = directory };

			if (!Directory.Exists(directory))
				return ppd;

			var tpd = new ThreadedDirectoryParser
			{
				baseDirectory = directory,
				stillQueuing = true
			};

			var threads = new Thread[numThreads];
			for (int i = 0; i < numThreads; i++)
			{
				var th = threads[i] = new Thread(tpd.ParseThread)
				{
					IsBackground = true,
					Priority = ThreadPriority.Lowest,
					Name = "Parser thread #" + i + " (" + directory + ")"
				};
				th.Start();
			}

			var sw = new Stopwatch();
			sw.Start();

			tpd.PrepareQueue(rootPackage);
			
			for (int i = 0; i < numThreads; i++)
				if (threads[i].IsAlive)
					threads[i].Join(10000);

			sw.Stop();

			ppd.AmountFiles = tpd.fileCount;
			ppd.TotalDuration = sw.Elapsed.TotalSeconds;

			return ppd;
		}

		void PrepareQueue(RootPackage root)
		{
			if (!Directory.Exists(baseDirectory))
			{
				stillQueuing = false;
				return; 
			}

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
				lock(queue)
					queue.Push(new Tuple<string, ModulePackage>(file, lastPack));
			}

			stillQueuing = false;
		}

		void ParseThread()
		{
			var file = "";
			ModulePackage pack = null;

			while (queue.Count != 0 || stillQueuing)
			{
				lock (queue)
				{
					if (queue.Count == 0)
					{
						if(stillQueuing)
						{
							Thread.Sleep(1);
							continue;
						}
						return;
					}

					var kv = queue.Pop();
					file = kv.Item1;
					pack = kv.Item2;
				}

				IAbstractSyntaxTree ast;
				try
				{
					// If no debugger attached, save time + memory by skipping function bodies
					ast = DParser.ParseFile(file, skipFunctionBodies);
				}
				catch (Exception ex)
				{
					ast = new DModule{ ParseErrors = new System.Collections.ObjectModel.ReadOnlyCollection<ParserError>(
						new List<ParserError>{
							new ParserError(false, ex.Message + "\n\n" + ex.StackTrace, DTokens.Invariant, CodeLocation.Empty)
					       }) };
					LastException = ex;
				}
				
				if (!(pack is RootPackage))
					ast.ModuleName = pack.Path + "." + Path.GetFileNameWithoutExtension(file);
				else if(string.IsNullOrEmpty(ast.ModuleName))
					ast.ModuleName = Path.GetFileNameWithoutExtension(file);

				ast.FileName = file;
				pack.Modules[ModuleNameHelper.ExtractModuleName(ast.ModuleName)] = ast;
			}
		}
	}
}
