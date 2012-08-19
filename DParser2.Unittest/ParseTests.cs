using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using D_Parser.Misc;
using D_Parser.Parser;
using System.Diagnostics;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;

namespace DParser2.Unittest
{
	[TestClass]
	public class ParseTests
	{
		[TestMethod]
		public void ParseEscapeLiterals()
		{
			var e = DParser.ParseExpression(@"`a`\n""lolol""");

			Assert.AreEqual((e as IdentifierExpression).Value, "a\nlolol");
		}

		public void ParseEmptyCharLiteral()
		{
			var e = DParser.ParseExpression("['': false, '&': true, '=': true]");

			Assert.IsInstanceOfType(e, typeof(AssocArrayExpression));
			var aa = (AssocArrayExpression)e;

			Assert.AreEqual(3, aa.Elements.Count);
		}

		[TestMethod]
		public void TestSyntaxError1()
		{
			var e = DParser.ParseExpression("new ubyte[size]");

			var s = DParser.ParseBlockStatement(@"long neIdx = find(pResult, ""{/loop");
		}

		[TestMethod]
		public void Attributes1()
		{
			var n = DParser.ParseString("align(2) align int a;");

			var a = n["a"] as DVariable;
			Assert.IsNotNull(a);
			Assert.AreEqual(1, a.Attributes.Count);

			var attr = a.Attributes[0];
			Assert.AreEqual(DTokens.Align, attr.Token);
			Assert.AreEqual(null, attr.LiteralContent);

			n = DParser.ParseString("private public int a;");
			a = n["a"] as DVariable;
			Assert.IsNotNull(a);
			Assert.AreEqual(1, a.Attributes.Count);

			n = DParser.ParseString(@"private:
public int a;
int b;");
			a = n["a"] as DVariable;
			Assert.IsNotNull(a);
			Assert.AreEqual(1, a.Attributes.Count);
			Assert.AreEqual(DTokens.Public,a.Attributes[0].Token);

			a = n["b"] as DVariable;
			Assert.IsNotNull(a);
			Assert.AreEqual(1, a.Attributes.Count);
			Assert.AreEqual(DTokens.Private, a.Attributes[0].Token);
		}

		//[TestMethod]
		public void TestPhobos()
		{
			var pc = ParsePhobos();

			foreach (var mod in pc)
				Assert.AreEqual(mod.ParseErrors.Count, 0);
		}

		public static ParseCache ParsePhobos()
		{
			var dmdBase = @"A:\D\dmd2\src";

			var pc = new ParseCache();
			pc.FinishedParsing += new ParseCache.ParseFinishedHandler(pc_FinishedParsing);
			pc.FinishedUfcsCaching += new Action(() =>
			{
				Trace.WriteLine(string.Format("Finished UFCS analysis: {0} resolutions in {1}s; ~{2}ms/resolution", pc.UfcsCache.CachedMethods.Count,
					pc.UfcsCache.CachingDuration.TotalSeconds, Math.Round(pc.UfcsCache.CachingDuration.TotalMilliseconds/pc.UfcsCache.CachedMethods.Count,3)), "ParserTests");
				//compareUfcsResults(pc.UfcsCache);
			});

			pc.BeginParse(new[] { 
				dmdBase+@"\druntime\import",
				dmdBase+@"\phobos",
				//@"A:\Projects\fxLib\src"
				//@"A:\D\tango-d2\tngo"
			},dmdBase+@"\phobos");

			pc.WaitForParserFinish();
			
			return pc;
		}

		static void pc_FinishedParsing(ParsePerformanceData[] PerformanceData)
		{
			foreach (var ppd in PerformanceData)
				Trace.WriteLine(string.Format("Parsed {0} files in {1}; {2}ms/file", ppd.AmountFiles, ppd.BaseDirectory, ppd.FileDuration), "ParserTests");
		}

		[TestMethod]
		public void ParsePerformance1()
		{
			var pc = ParsePhobos();

			
		}

		/*void compareUfcsResults(UFCSCache u)
		{
			var l1 = new List<string>();
			var diff=new List<string>();


			foreach (var m in u.CachedMethods)
				l1.Add(m.Key.ToString(true));

			var l2 = System.IO.File.ReadAllLines(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\ufcs.txt");

			foreach(var l in l2)
				if(!l1.Remove(l) && !l.Contains("terminator") && l!=")")
				{
					Trace.WriteLine(l);
					diff.Add(l);
				}

			
		}*/
	}
}
