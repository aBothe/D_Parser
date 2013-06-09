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
using D_Parser.Dom;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace D_Parser
{
	#region Event meta info
	public class ParsingFinishedEventArgs
	{
		public readonly string Directory;
		public readonly RootPackage Package;
		public readonly TimeSpan Duration;
		public readonly int FileAmount;

		public ParsingFinishedEventArgs(string dir, RootPackage pack, TimeSpan duration, int fileCount)
		{
			Directory = dir;
			Package = pack;
			Duration = duration;
			FileAmount = fileCount;
		}
	}

	public class UfcsAnalysisFinishedEventArgs
	{
		public readonly RootPackage Package;
		public readonly TimeSpan Duration;
		public readonly int MethodCount;

		public UfcsAnalysisFinishedEventArgs(RootPackage pack, TimeSpan duration, int methodCount)
		{
			Package = pack;
			Duration = duration;
			MethodCount = methodCount;
		}
	}

	public delegate void ParseFinishedHandler(ParsingFinishedEventArgs ea);
	public delegate void UfcsAnalysisFinishedHandler(UfcsAnalysisFinishedEventArgs ea);
	#endregion

	public class GlobalParseCache
	{
		#region Properties
		internal static readonly ConcurrentDictionary<string, RootPackage> GlobalPackages
			= new ConcurrentDictionary<string, RootPackage>();

		/// <summary>
		/// Lookup that is used for fast filename-AST lookup. Do NOT modify, it'll be done inside the ModulePackage instances.
		/// </summary>
		internal readonly static ConcurrentDictionary<string, DModule> fileLookup = new ConcurrentDictionary<string, DModule>();

		/// <summary>
		/// True if parse progresses shall be captured. Only then, GetParseProgress will return a valid number.
		/// </summary>
		public static bool CaptureParseProgress = false;
		static readonly ConcurrentDictionary<string, double> ParseProgresses = new ConcurrentDictionary<string, double>();
		public static ParseFinishedHandler ParseTaskFinished;
		public static UfcsAnalysisFinishedHandler UfcsAnalysisFinished;

		private GlobalParseCache (){}
		#endregion

		#region Parsing
		public static void AddOrUpdatePathAsync(string basePath)
		{
			if (string.IsNullOrEmpty (basePath))
				throw new ArgumentNullException ("basePath must not be null");


		}

		public static double GetParseProgress(string basePath)
		{
			double p;
			ParseProgresses.TryGetValue(basePath, out p);
			return p;
		}
		#endregion

		#region Lookup
		public static ModulePackage GetPackage(string basePath, string subPackage=null)
		{
			if (string.IsNullOrEmpty (basePath))
				throw new ArgumentNullException ("basePath must not be null");

			var pack = GlobalPackages.GetOrAdd (basePath, (RootPackage)null) as ModulePackage;

			if (pack != null)
				pack = pack.GetOrCreateSubPackage (subPackage);

			return pack;
		}

		public static DModule GetModule(string file)
		{
			return fileLookup.GetOrAdd (file, (DModule)null);
		}

		public static IEnumerable<RootPackage> GetPackages(params string[] basePaths)
		{
			RootPackage pkg;
			foreach (var p in basePaths)
				if (GlobalPackages.TryGetValue (p, out pkg))
					yield return pkg;
		}
		#endregion
	}
}

