using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D_Parser.Misc;
using D_Parser.Parser;
using System.Diagnostics;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.TypeResolution;

namespace Tests
{
	[TestFixture]
	public class ParseTests
	{
		[Test]
		public void ParseEscapeLiterals()
		{
			var e = DParser.ParseExpression(@"`a`\n""lolol""");

			Assert.AreEqual("a\nlolol", (e as IdentifierExpression).Value);
		}

		public void ParseEmptyCharLiteral()
		{
			var e = DParser.ParseExpression("['': false, '&': true, '=': true]");

			Assert.IsInstanceOfType(typeof(AssocArrayExpression),e);
			var aa = (AssocArrayExpression)e;

			Assert.AreEqual(3, aa.Elements.Count);
		}

		[Test]
		public void TestSyntaxError1()
		{
			var e = DParser.ParseExpression("new ubyte[size]");

			var s = DParser.ParseBlockStatement(@"long neIdx = find(pResult, ""{/loop");

			var mod = DParser.ParseString(@"void foo() {}

//* one line
void bar();");

			Assert.AreEqual(2, mod.Children.Count);
			Assert.IsInstanceOfType(typeof(DMethod),mod["bar"][0]);
		}

		[Test]
		public void Attributes1()
		{
			var n = DParser.ParseString("align(2) align int a;");

			var a = n["a"][0] as DVariable;
			Assert.IsNotNull(a);
			Assert.AreEqual(1, a.Attributes.Count);

			var attr = a.Attributes[0] as Modifier;
			Assert.AreEqual(DTokens.Align, attr.Token);
			Assert.AreEqual(null, attr.LiteralContent);

			n = DParser.ParseString("private public int a;");
			a = n["a"][0] as DVariable;
			Assert.IsNotNull(a);
			Assert.AreEqual(1, a.Attributes.Count);

			n = DParser.ParseString(@"private:
public int a;
int b;");
			a = n["a"][0] as DVariable;
			Assert.IsNotNull(a);
			Assert.AreEqual(1, a.Attributes.Count);
			Assert.AreEqual(DTokens.Public,((Modifier)a.Attributes[0]).Token);

			a = n["b"][0] as DVariable;
			Assert.IsNotNull(a);
			Assert.AreEqual(1, a.Attributes.Count);
			Assert.AreEqual(DTokens.Private, ((Modifier)a.Attributes[0]).Token);
		}

		[Test]
		public void Attributes2()
		{
			var m = DParser.ParseString(@"
debug(1) private int a;
int b;

debug
	version = Cust;

debug
	int A;
else version(D)
	int B;
else int C;");
			var a = m["a"][0] as DVariable;
			var b = m["b"][0] as DVariable;

			Assert.AreEqual(2,a.Attributes.Count);
			Assert.AreEqual(0,b.Attributes.Count);

			var A = m["A"][0] as DVariable;
			var B = m["B"][0] as DVariable;
			var C = m["C"][0] as DVariable;

			Assert.AreEqual(1, A.Attributes.Count);
			Assert.AreEqual(2,B.Attributes.Count);
			Assert.AreEqual(2, C.Attributes.Count);
		}

		//[Test]
		public void TestPhobos()
		{
			var pc = ParsePhobos();
			bool hadErrors = false;
			foreach (var mod in pc)
			{
				if (mod.ParseErrors.Count != 0)
				{
					Trace.WriteLine(mod.FileName);
					Trace.Indent();

					foreach (var err in mod.ParseErrors)
						Trace.WriteLine(err.Location.ToString() + "\t" + err.Message);

					Trace.Unindent();
					hadErrors = true;
				}
			}
			if(hadErrors)
				Assert.Fail("Parse process failed!");
		}

		public static ParseCache ParsePhobos(bool ufcs=true)
		{
			var dmdBase = @"A:\D\dmd2\src";

			var pc = new ParseCache();
			pc.EnableUfcsCaching = ufcs;
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

		[Test]
		public void ParsePerformance1()
		{
			//var pc = ParsePhobos(false);
			var pcl = ResolutionTests.CreateCache(@"module modA;

import std.stdio, std.array;

class lol{
	
	static int Object;
	
	int inc(int i, int k)
	{
		
		return i+k;	
	}

	void derp() {}
	
	const void lolBar(this T)() {
		
		auto l=123.inc!int();
		lol.Object;
		writeln(typeid(T));
		
		Object=1;
	}
}

void main()
{
	immutable(char)[] arr;
	destroy(arr);
	for(int i=0;i<10;i++)
		writeln(i);
	return;
	
	auto st=new STest();
	auto tt = new ModClass!int();
	writeln(st.a);
	
	static assert(st.a==34);
	
	int i = delegate int() { return 123; }();	
	//int j= 234++;
	writeln(i);
	writeln(di,123,123);

	lol o = new lol();
	o.derp();
}

");
			//pcl.Add(pc);

			var sw = new Stopwatch();
			var main = pcl[0]["modA"]["main"][0] as DMethod;
			Assert.AreEqual(0, (pcl[0]["modA"] as DModule).ParseErrors.Count);
			var s = main.Body.SubStatements[main.Body.SubStatements.Length - 1] as IExpressionContainingStatement;
			var ctxt = ResolutionContext.Create(pcl, null, main, s);
			//ctxt.ContextIndependentOptions |= ResolutionOptions.StopAfterFirstOverloads | ResolutionOptions.DontResolveBaseClasses | ResolutionOptions.DontResolveBaseTypes;
			var x = s.SubExpressions[0];
			//pc.UfcsCache.Update(pcl);
			sw.Restart();

			var t = Evaluation.EvaluateType(x, ctxt);

			sw.Stop();
			Trace.WriteLine("Took " + sw.Elapsed.TotalMilliseconds + "ms to resolve " + x);
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

		[Test]
		public void DeepBlockSearch()
		{
			var m = DParser.ParseString(@"module modA;

class A
{
	int a;
	int b;
	int c;
	int d;
}

class B
{
	class subB
	{
		int a;
		int b;
		int c;	
	}
}

void main()
{
	int a;

}

class C
{

}");

			IStatement s;
			var n = DResolver.SearchBlockAt(m, ((IBlockNode)m["A"][0])["d"][0].Location, out s);
			Assert.AreEqual("A", n.Name);

			var loc = ((IBlockNode)m["C"][0]).BlockStartLocation;
			n = DResolver.SearchBlockAt(m, loc, out s);
			Assert.AreEqual("C", n.Name);

			loc = ((IBlockNode)((IBlockNode)m["B"][0])["subB"][0])["c"][0].Location;
			n = DResolver.SearchBlockAt(m, loc, out s);
			Assert.AreEqual("subB", n.Name);

			n = DResolver.SearchBlockAt(m, new CodeLocation(1, 10), out s);
			Assert.AreEqual(m,n);

			loc = ((IBlockNode)m["main"][0])["a"][0].EndLocation;
			n = DResolver.SearchBlockAt(m, loc, out s);
			Assert.AreEqual("main", n.Name);
		}

		[Test]
		public void StaticIf()
		{
			var m = DParser.ParseString(@"module m;
void foo()
{
	static if(true)
		writeln();
}");

			Assert.AreEqual(0, m.ParseErrors.Count);
		}
		
		[Test]
		public void AliasInitializerList()
		{
			var m = DParser.ParseString(@"alias str = immutable(char)[]");

			Assert.AreEqual(0, m.ParseErrors.Count);
		}
	}
}
