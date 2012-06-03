using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using D_Parser.Misc;
using D_Parser.Parser;
using System.Diagnostics;

namespace DParser2.Unittest
{
	[TestClass]
	public class ParseTests
	{
		[TestMethod]
		public void ParsePhobos()
		{
			var dmdBase = @"A:\D\dmd2\src";

			var pc = new ParseCache();
			pc.FinishedParsing += new ParseCache.ParseFinishedHandler(pc_FinishedParsing);
			pc.FinishedUfcsCaching += new Action(() =>
			{
				Trace.WriteLine(string.Format("Finished UFCS analysis: {0} resolutions in {1}s; ~{2}ms/resolution", pc.UfcsCache.CachedMethods.Count,
					pc.UfcsCache.CachingDuration.TotalSeconds, Math.Round(pc.UfcsCache.CachingDuration.TotalMilliseconds/pc.UfcsCache.CachedMethods.Count,3)), "ParserTests");
			});

			pc.BeginParse(new[] { 
				dmdBase+@"\phobos",
				dmdBase+@"\druntime\import"
			},dmdBase+@"\phobos");

			pc.WaitForParserFinish();
			
			foreach (var mod in pc)
			{
				Assert.AreEqual(mod.ParseErrors.Count, 0);
			}
		}

		void pc_FinishedParsing(ParsePerformanceData[] PerformanceData)
		{
			foreach (var ppd in PerformanceData)
				Trace.WriteLine(string.Format("Parsed {0} files in {1}; {2}ms/file", ppd.AmountFiles, ppd.BaseDirectory, ppd.FileDuration), "ParserTests");
		}
	}
}
