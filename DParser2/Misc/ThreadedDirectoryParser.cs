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
		int fileCount = 0;
		long totalMSecs = 0;
		bool skipFunctionBodies = true;//!Debugger.IsAttached;
		ConcurrentStack<string> queue = new ConcurrentStack<string>();
		#endregion

		public static ParsePerformanceData Parse (string directory, RootPackage rootPackage, bool sync = false)
		{
			var ppd = new ParsePerformanceData { BaseDirectory = directory };

			if (!Directory.Exists (directory))
				return ppd;

			var tpd = new ThreadedDirectoryParser{ baseDirectory = directory };

			tpd.PrepareQueue();

			if (tpd.queue.Count == 0)
				return ppd;

			if (sync || tpd.queue.Count == 1)
				tpd.ParseThread(rootPackage);
			else 
			{
				var threads = new Thread[Math.Min(tpd.queue.Count, numThreads)];
				for (int i = 0; i < threads.Length; i++) 
				{
					(threads [i] = new Thread (tpd.ParseThread)	{
						IsBackground = true,
						Priority = ThreadPriority.BelowNormal,
						Name = "Parser thread #" + i + " (" + directory + ")"
					}).Start (rootPackage);
				}

				for (int i = 0; i < threads.Length; i++)
					if (threads [i].IsAlive)
						threads [i].Join (10000);
			}

			ppd.AmountFiles = tpd.fileCount;
			ppd.TotalDuration = (double)tpd.totalMSecs/1000d;//sw.Elapsed.TotalSeconds;

			return ppd;
		}

		void PrepareQueue ()
		{
			if (!Directory.Exists (baseDirectory)) {
				return; 
			}

			totalMSecs = 0;

			//ISSUE: wild card character ? seems to behave differently across platforms
			// msdn: -> Exactly zero or one character.
			// monodocs: -> Exactly one character.
			var files = Directory.GetFiles (baseDirectory, "*.d", SearchOption.AllDirectories);
			if (files.Length != 0) {
				if(Environment.OSVersion.Platform == PlatformID.Win32Windows)
					queue.PushRange (files);
				else
				{
					for(int i = 0; i < files.Length;i++)
						queue.Push(files[i]);
				}
			}
			files = Directory.GetFiles(baseDirectory, "*.di", SearchOption.AllDirectories);
			if (files.Length != 0) {
				if(Environment.OSVersion.Platform == PlatformID.Win32Windows)
					queue.PushRange (files);
				else
				{
					for(int i = 0; i < files.Length;i++)
						queue.Push(files[i]);
				}
			}

			fileCount = queue.Count;
		}

		static string phobosDFile = Path.DirectorySeparatorChar + "phobos" + Path.DirectorySeparatorChar + "phobos.d";
		static string indexDFile = Path.DirectorySeparatorChar + "phobos" + Path.DirectorySeparatorChar + "index.d";

		void ParseThread(Object ro)
		{
			var root = ro as RootPackage;
			var sw = new Stopwatch();
			string file;
			string code;
			IAbstractSyntaxTree ast = null;
			ModulePackage pack;

			while (true)
			{
				if (!queue.TryPop(out file))
				{
					break;
				}

				if (file.EndsWith(phobosDFile) || file.EndsWith(indexDFile))
				{
					fileCount--; // Shouldn't cause any race-conditions
					continue;
				}

				code = File.ReadAllText(file);

				sw.Start();
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
					if(ast != null)
						ast.FileName = file;
				}
				sw.Stop();

				if (string.IsNullOrEmpty(ast.ModuleName))
					ast.ModuleName = DModule.GetModuleName(baseDirectory, file);

				pack = root.GetOrCreateSubPackage(ModuleNameHelper.ExtractPackageName(ast.ModuleName),true);
				
				lock(pack)
					pack.AddModule(ast);
			}

			totalMSecs+=sw.ElapsedMilliseconds;
		}
	}
}
