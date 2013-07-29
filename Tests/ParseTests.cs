using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Misc.Mangling;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using NUnit.Framework;

namespace Tests
{
	[TestFixture]
	public class ParseTests
	{
		[Test]
		public void RealLiterals()
		{
			var e = DParser.ParseExpression("0x1.921fb54442d18469898cc51701b84p+1L");
			
			Assert.That(e, Is.TypeOf(typeof(IdentifierExpression)));
			var id = e as IdentifierExpression;
			Assert.That(Math.Abs((decimal)id.Value - (decimal)Math.PI), Is.LessThan(0.1M));
		}
		
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
		public void TestSyntaxError2()
		{
			var s = "class Foo( if(is(T==float) {} class someThingElse {}";
			var mod = DParser.ParseString (s);

			Assert.GreaterOrEqual(mod.ParseErrors.Count,1);
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
		
		[Test]
		public void AccessExpression1()
		{
			var e1 = DParser.ParseExpression("a.b!c");
			var e2 = DParser.ParseExpression("a.b");
			
			Assert.That(e1, Is.TypeOf(typeof(PostfixExpression_Access)));
			Assert.That(e2, Is.TypeOf(typeof(PostfixExpression_Access)));
		}

		[Test]
		public void Expr1()
		{
			var m = DParser.ParseString(@"module A;
void main() {
	if(*p == 0)
	{}
}");

			Assert.That(m.ParseErrors.Count, Is.EqualTo(0));
		}
		
		[Test]
		public void LexingPerformance()
		{
			var f = File.ReadAllText(Environment.OSVersion.Platform == PlatformID.Win32NT ? @"D:\D\dmd2\src\phobos\std\string.d" : "/usr/include/d/std/string.d");
			
			var lx = new Lexer(new StringReader(f));
			var sw = new Stopwatch();
			sw.Start();
			
			while(true)
			{
				lx.NextToken();
				if(lx.IsEOF)
					break;
			}
			
			sw.Stop();
			Console.WriteLine(sw.ElapsedMilliseconds);
		}

		//[Test]
		public void TestPhobos()
		{
			var pc = ParsePhobos();
			bool hadErrors = false;
			var sb = new StringBuilder();
			foreach (var root in pc)
			{
				foreach(var mod in root)
					if (mod.ParseErrors.Count != 0)
					{
						sb.AppendLine(mod.FileName);

						foreach (var err in mod.ParseErrors)
							sb.AppendLine("\t"+err.Location.ToString() + "\t" + err.Message);

						hadErrors = true;
					}
			}
			Console.WriteLine (sb.ToString ());
			Assert.That(sb.ToString(), Is.Empty);
		}

		public static ParseCacheView ParsePhobos(bool ufcs=true)
		{
			var dir = "/usr/include/d";

			GlobalParseCache.ParseTaskFinished += pc_FinishedParsing;
			UFCSCache.SingleThreaded = true;
			UFCSCache.AnyAnalysisFinished+=(ea) => 
				Trace.WriteLine(string.Format("Finished UFCS analysis: {0} resolutions in {1}s; ~{2}ms/resolution", ea.UfcsCache.MethodCacheCount,
				                              ea.UfcsCache.CachingDuration.TotalSeconds, Math.Round(ea.UfcsCache.CachingDuration.TotalMilliseconds/ea.UfcsCache.MethodCacheCount,3)), "ParserTests");

			GlobalParseCache.BeginAddOrUpdatePaths (new[] { dir }, false);

			GlobalParseCache.WaitForFinish();

			var pc = new ParseCacheView(new[]{dir});

			foreach (var root in pc)
				root.UfcsCache.BeginUpdate (pc);

			return pc;
		}

		static void pc_FinishedParsing(ParsingFinishedEventArgs ppd)
		{
			Trace.WriteLine(string.Format("Parsed {0} files in {1}; {2}ms/file", ppd.FileAmount, ppd.Directory, ppd.FileDuration), "ParserTests");
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
		public void ExpressionHierarchySearch()
		{
			var s = "writeln(foo!int(";
			TestExpressionEnd<PostfixExpression_MethodCall>(s,true);
			TestExpressionEnd<TokenExpression>(s,false);
			
			TestExpressionEnd<TemplateInstanceExpression>("tokenize!");
			TestExpressionEnd<IdentifierExpression>("tokenize!Token");
			TestExpressionEnd<PostfixExpression_MethodCall>("tokenize!Token(",true);
			TestExpressionEnd<PostfixExpression_MethodCall>("Lexer.tokenize!Token(",true);
			TestExpressionEnd<PostfixExpression_MethodCall>("tokenize!Token(12, true, ",true);
			TestExpressionEnd<TokenExpression>("tokenize!Token(12, true, ",false);
			TestExpressionEnd<PostfixExpression_MethodCall>("Lexer.tokenize!Token(12, true, ",true);
			TestExpressionEnd<PostfixExpression_MethodCall>("Lexer!HeyHo.tokenize!Token(",true);
			
			TestExpressionEnd<PostfixExpression_MethodCall>("Lexer.tokenize!Token(12, true",true);
			TestExpressionEnd<TokenExpression>("Lexer.tokenize!Token(12, true",false);
		}
		
		void TestExpressionEnd<T>(string expression, bool watchForParamExpressions = false)
		{
			var ex = DParser.ParseExpression(expression);
			
			var x = ExpressionHelper.SearchExpressionDeeply(ex, ex.EndLocation, watchForParamExpressions);
			Assert.That(x, Is.TypeOf(typeof(T)));
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
			var m = DParser.ParseString(@"alias str = immutable(char)[];");

			Assert.AreEqual(0, m.ParseErrors.Count);
		}
		
		[Test]
		public void Demangling()
		{
			ITypeDeclaration q;
			bool isCFun;
			var t = Demangler.Demangle("_D3std5stdio35__T7writelnTC3std6stream4FileTAAyaZ7writelnFC3std6stream4FileAAyaZv", out q, out isCFun);
		}
	}
}
