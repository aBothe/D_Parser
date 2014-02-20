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
using D_Parser;

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

			Assert.AreEqual("a\nlolol", (e as IdentifierExpression).StringValue);
		}

		public void ParseEmptyCharLiteral()
		{
			var e = DParser.ParseExpression("['': false, '&': true, '=': true]");

			Assert.IsInstanceOfType(typeof(AssocArrayExpression),e);
			var aa = (AssocArrayExpression)e;

			Assert.AreEqual(3, aa.Elements.Count);
		}

		[Test]
		public void RefOnlyMethodDecl()
		{
			var mod = DParser.ParseString(@"ref foo(auto const ref char c) {}");
			Assert.That (mod.ParseErrors.Count, Is.EqualTo (0));
			var dm = mod ["foo"].First () as DMethod;
			Assert.That (dm, Is.TypeOf (typeof(DMethod)));
			Assert.That (dm.Parameters [0].Name, Is.EqualTo ("c"));
		}

		[Test]
		public void Unicode1()
		{
			var lex = new Lexer (new StringReader ("'ߞ'"));

			lex.NextToken ();

			Assert.That (lex.LexerErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void TestSyntaxError4()
		{
			var mod = DParser.ParseString (@"module A;
class A {
    private {
        int a;
    }
}");
			Assert.That (mod.ParseErrors.Count, Is.EqualTo (0));
		}

		[Test]
		public void TestSyntaxError5()
		{
			var mod = DParser.ParseString (@"module A;
void main(){
	if (extra * extra * 2 < y.length * y.length)
		int derp;
}
");
			Assert.That (mod.ParseErrors.Count, Is.EqualTo (0));
		}

		[Test]
		public void TestSyntaxError3()
		{
			DModule mod;

			mod = DParser.ParseString (@"enum a = __traits(compiles, zip((S[5]).init[]));");
			Assert.That (mod.ParseErrors.Count, Is.EqualTo (0));

			mod = DParser.ParseString ("enum a = (new E[sizes[0]]).ptr;");
			Assert.That (mod.ParseErrors.Count, Is.EqualTo (0));

			mod = DParser.ParseString (@"
			enum ptrdiff_t findCovariantFunction =
            is(typeof((             Source).init.opDispatch!(finfo.name)(Params.init))) ||
            is(typeof((       const Source).init.opDispatch!(finfo.name)(Params.init))) ||
            is(typeof((   immutable Source).init.opDispatch!(finfo.name)(Params.init))) ||
            is(typeof((      shared Source).init.opDispatch!(finfo.name)(Params.init))) ||
            is(typeof((shared const Source).init.opDispatch!(finfo.name)(Params.init)));");
			Assert.That (mod.ParseErrors.Count, Is.EqualTo (0));
		}

		[Test]
		public void TestSyntaxError1()
		{
			DModule mod;

			mod = DParser.ParseString (@"
void main()
{
    align(1)
    struct X1 { ubyte b; int n; }
}
class T : typeof(new A) { }
#line 123
# line 123
#line ""hurrdurr""
			enum E11554;
static assert(is(E11554 == enum));
			enum bool isNumeric(T) = is(NumericTypeOf!T) && !isAggregateType!T; alias Pointify(T) = void*;");
			Assert.That (mod.ParseErrors.Count, Is.EqualTo (0));

			var e = DParser.ParseExpression("new ubyte[size]");

			var s = DParser.ParseBlockStatement(@"long neIdx = find(pResult, ""{/loop");

			mod = DParser.ParseString(@"void foo() {}

//* one line
void bar();");

			Assert.AreEqual(2, mod.Children.Count);
			Assert.IsInstanceOfType(typeof(DMethod),mod["bar"].First());
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

			var a = n["a"].First() as DVariable;
			Assert.IsNotNull(a);
			Assert.AreEqual(1, a.Attributes.Count);

			var attr = a.Attributes[0] as Modifier;
			Assert.AreEqual(DTokens.Align, attr.Token);
			Assert.That(attr.LiteralContent == null || attr.LiteralContent as string == string.Empty);

			n = DParser.ParseString("private public int a;");
			a = n["a"].First() as DVariable;
			Assert.IsNotNull(a);
			Assert.AreEqual(1, a.Attributes.Count);

			n = DParser.ParseString(@"private:
public int a;
int b;");
			a = n["a"].First() as DVariable;
			Assert.IsNotNull(a);
			Assert.AreEqual(1, a.Attributes.Count);
			Assert.AreEqual(DTokens.Public,((Modifier)a.Attributes[0]).Token);

			a = n["b"].First() as DVariable;
			Assert.IsNotNull(a);
			Assert.AreEqual(1, a.Attributes.Count);
			Assert.AreEqual(DTokens.Private, ((Modifier)a.Attributes[0]).Token);
		}

		[Test]
		public void Attributes2()
		{
			var m = DParser.ParseString(@"
int foo() if(is(T==string)) {}
debug(1) private int a;
int b;

debug
	version = Cust;

debug
	int A;
else version(D)
	int B;
else int C;");
			var a = m["a"].First() as DVariable;
			var b = m["b"].First() as DVariable;

			Assert.AreEqual(2,a.Attributes.Count);
			Assert.That (a.ContainsAttribute(DTokens.Private));
			Assert.AreEqual(0,b.Attributes.Count);

			var A = m["A"].First() as DVariable;
			var B = m["B"].First() as DVariable;
			var C = m["C"].First() as DVariable;

			Assert.AreEqual(1, A.Attributes.Count);
			Assert.AreEqual(2,B.Attributes.Count);
			Assert.AreEqual(2, C.Attributes.Count);

			Assert.That ((m["foo"].First() as DMethod).TemplateConstraint, Is.TypeOf(typeof(IsExpression)));
		}

		[Test]
		public void DeclConditionElseSection()
		{
			var m = DParser.ParseString (@"module A;
version(VersA)
	int a;
else:

int b;
");
			Assert.That (m.ParseErrors.Count, Is.EqualTo (0));
			DNode dn;

			dn = m ["a"].First () as DNode;
			Assert.That (dn.Attributes.Count, Is.EqualTo (1));
			Assert.That (dn.Attributes [0], Is.TypeOf (typeof(VersionCondition)));

			dn = m ["b"].First () as DNode;
			Assert.That (dn.Attributes.Count, Is.EqualTo (1));
			Assert.That (dn.Attributes [0], Is.TypeOf (typeof(NegatedDeclarationCondition)));
			Assert.That ((dn.Attributes [0] as NegatedDeclarationCondition).FirstCondition, Is.TypeOf (typeof(VersionCondition)));
		}

		[Test]
		public void Attributes3()
		{
			var m = DParser.ParseString (@"module A;
final class Class
{
	public:
		static void statFoo(int i, ref double d) { int local; }
		static void statBar(int i, ref double d) nothrow { int local; }
	private:
		int priv;
}

int ii;
");
			Assert.That (m.ParseErrors.Count, Is.EqualTo (0));
			DNode dn;
			DMethod dm;

			var Class = m ["Class"].First() as DClassLike;
			Assert.That (Class.ContainsAttribute (DTokens.Final));
			dn = Class ["statFoo"].First () as DNode;
			Assert.That (dn.Attributes.Count, Is.EqualTo (2));
			Assert.That (dn.ContainsAttribute(DTokens.Public));
			Assert.That (dn.ContainsAttribute(DTokens.Static));

			dm = dn as DMethod;
			dn = dm.Parameters [0] as DNode;
			Assert.That (dn.Attributes.Count, Is.EqualTo(0));
			Assert.That (dn.IsPublic);

			dn = dm.Parameters [1] as DNode;
			Assert.That (dn.Attributes.Count, Is.EqualTo(1));
			Assert.That(dn.ContainsAttribute(DTokens.Ref));

			dn = (dm.Body.SubStatements.First() as IDeclarationContainingStatement).Declarations[0] as DNode;
			Assert.That (dn.Attributes.Count, Is.EqualTo (0));



			dn = Class ["statBar"].First () as DNode;
			Assert.That (dn.Attributes.Count, Is.EqualTo (3));
			Assert.That (dn.ContainsAttribute(DTokens.Public));
			Assert.That (dn.ContainsAttribute(DTokens.Static));
			Assert.That (dn.ContainsAttribute(DTokens.Nothrow));

			dm = dn as DMethod;
			dn = dm.Parameters [0] as DNode;
			Assert.That (dn.Attributes.Count, Is.EqualTo(0));
			Assert.That (dn.IsPublic);

			dn = dm.Parameters [1] as DNode;
			Assert.That (dn.Attributes.Count, Is.EqualTo(1));
			Assert.That(dn.ContainsAttribute(DTokens.Ref));

			dn = (dm.Body.SubStatements.First() as IDeclarationContainingStatement).Declarations[0] as DNode;
			Assert.That (dn.Attributes.Count, Is.EqualTo (0));



			dn = Class["priv"].First() as DNode;
			Assert.That (dn.Attributes.Count, Is.EqualTo(1));
			Assert.That(dn.ContainsAttribute(DTokens.Private));

			dn = m["ii"].First() as DNode;
			Assert.That (dn.Attributes.Count, Is.EqualTo(0));
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
		
		//[Test]
		public void LexingPerformance()
		{
			var f = File.ReadAllText(Environment.OSVersion.Platform == PlatformID.Win32NT ? @"D:\D\dmd2\src\phobos\std\string.d" : "/usr/include/dlang/std/string.d");
			
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
			GlobalParseCache.BeginAddOrUpdatePaths (new[] { dir }, false);

			GlobalParseCache.WaitForFinish();

			return new ParseCacheView(new[]{dir});
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
			var main = pcl[0]["modA"]["main"].First() as DMethod;
			Assert.AreEqual(0, (pcl[0]["modA"] as DModule).ParseErrors.Count);
			var s = main.Body.SubStatements.Last() as IExpressionContainingStatement;
			var ctxt = ResolutionContext.Create(pcl, null, main, s);
			//ctxt.ContextIndependentOptions |= ResolutionOptions.StopAfterFirstOverloads | ResolutionOptions.DontResolveBaseClasses | ResolutionOptions.DontResolveBaseTypes;
			var x = s.SubExpressions[0];
			//pc.UfcsCache.Update(pcl);
			sw.Restart();

			var t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

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
			var n = DResolver.SearchBlockAt(m, ((IBlockNode)m["A"].First())["d"].First().Location, out s);
			Assert.AreEqual("A", n.Name);

			var loc = ((IBlockNode)m["C"].First()).BlockStartLocation;
			n = DResolver.SearchBlockAt(m, loc, out s);
			Assert.AreEqual("C", n.Name);

			loc = ((IBlockNode)((IBlockNode)m["B"].First())["subB"].First())["c"].First().Location;
			n = DResolver.SearchBlockAt(m, loc, out s);
			Assert.AreEqual("subB", n.Name);

			n = DResolver.SearchBlockAt(m, new CodeLocation(1, 10), out s);
			Assert.AreEqual(m,n);

			loc = (((DMethod)m["main"].First()).Body.First() as IDeclarationContainingStatement).Declarations[0].EndLocation;
			n = DResolver.SearchBlockAt(m, loc, out s);
			Assert.AreEqual("main", n.Name);
		}

		[Test]
		public void StaticIf()
		{
			var m = DParser.ParseString(@"
static assert(true);
void foo()
{
	static if(true)
		writeln();
}");

			Assert.AreEqual(0, m.ParseErrors.Count);

			var foo = m ["foo"].First () as DMethod;
			Assert.That (foo.Attributes.Count == 0);

			Assert.That (m.StaticStatements.Count, Is.EqualTo(1));
			Assert.That (m.StaticStatements[0], Is.TypeOf(typeof(StaticAssertStatement)));
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

			Assert.IsFalse (isCFun);
		}

		[Test]
		public void EponymousTemplates()
		{
			var m = DParser.ParseString(@"
enum mixinStuff = q{import C;};
enum isIntOrFloat(T) = is(T == int) || is(T == float);
alias isInt(T) = is(T == int);

enum
    allSatisfy(alias pred, TL...) =
        TL.length == 0 || (pred!(TL[0]) && allSatisfy!(pred, TL[1..$])),
    anySatisfy(alias pred, TL...) =
        TL.length != 0 && (pred!(TL[0]) || anySatisfy!(pred, TL[1..$])) || false;
");

			Assert.AreEqual(0, m.ParseErrors.Count);
			TemplateParameter tp;

			var dc = m.Children ["isIntOrFloat"].First () as EponymousTemplate;
			Assert.That (dc, Is.Not.Null);
			Assert.That (dc.TryGetTemplateParameter("T".GetHashCode(), out tp), Is.True);

			dc = m.Children ["isInt"].First () as EponymousTemplate;
			Assert.That (dc, Is.Not.Null);
			Assert.That (dc.TryGetTemplateParameter("T".GetHashCode(), out tp), Is.True);
		}

		[Test]
		/// <summary>
		/// Since 2.064. new Server(args).run(); is allowed
		/// </summary>
		public void PostNewExpressions()
		{
			var x = DParser.ParseExpression ("new Server(args).run()") as PostfixExpression;

			Assert.That (x, Is.TypeOf(typeof(PostfixExpression_MethodCall)));
			Assert.That ((x.PostfixForeExpression as PostfixExpression_Access).PostfixForeExpression, Is.TypeOf(typeof(NewExpression)));
		}

		[Test]
		public void SpecialTokenSequences()
		{
			var m = DParser.ParseString(@"module A;
#line 1
#line 0x123 ""ohyeah/asd.d""");

			Assert.AreEqual(0, m.ParseErrors.Count);
		}

		[Test]
		public void MetaBlocks()
		{
			var dn = DParser.ParseString (@"module A;
private:
			static if(a == 0)
{
	int aa;
}

debug = 2;
version = ASDF;
int dbg;
");
			Assert.That (dn.ParseErrors.Count, Is.EqualTo(0));
			Assert.That (dn.MetaBlocks.Count, Is.EqualTo(2));

			Assert.That (dn.MetaBlocks[0], Is.TypeOf(typeof(AttributeMetaDeclarationSection)));
			var attr = (dn.MetaBlocks [0] as AttributeMetaDeclarationSection).AttributeOrCondition [0];
			Assert.That (attr, Is.TypeOf (typeof(Modifier)));
			Assert.That ((attr as Modifier).Token, Is.EqualTo(DTokens.Private));

			Assert.That (dn.MetaBlocks[1], Is.TypeOf(typeof(AttributeMetaDeclarationBlock)));
			var attr2 = (dn.MetaBlocks [1] as AttributeMetaDeclarationBlock).AttributeOrCondition [0];
			Assert.That (attr2, Is.TypeOf(typeof(StaticIfCondition)));

			var aa = dn ["aa"].First () as DNode;
			Assert.That (aa is DVariable);
			Assert.That (aa.Attributes.Count == 2);
			Assert.That (aa.Attributes [0] == attr2);
			Assert.That (aa.Attributes[1] == attr);

			Assert.That (dn.StaticStatements.Count, Is.EqualTo(3));
			Assert.That (dn.StaticStatements[1], Is.TypeOf(typeof(DebugSpecification)));
			Assert.That (dn.StaticStatements[2], Is.TypeOf(typeof(VersionSpecification)));
			aa = dn["dbg"].First() as DNode;
			Assert.That (aa.Attributes.Count, Is.EqualTo(1));
			Assert.That (aa.Attributes[0], Is.SameAs(attr));
		}

		[Test]
		public void RelativeOffsetCalculation()
		{
			var code = @"asdfghij";

			Assert.That (DocumentHelper.GetOffsetByRelativeLocation (code, new CodeLocation (4, 1), 3, new CodeLocation (8, 1)), Is.EqualTo (7));
			Assert.That (DocumentHelper.GetOffsetByRelativeLocation (code, new CodeLocation (4, 1), 3, new CodeLocation (2, 1)), Is.EqualTo (1));

			code = @"a\nb\nc";

			Assert.That (DocumentHelper.GetOffsetByRelativeLocation (code, new CodeLocation (1, 1), 0, new CodeLocation (2, 3)), Is.EqualTo (code.Length));
			Assert.That (DocumentHelper.GetOffsetByRelativeLocation (code, new CodeLocation (2, 3), code.Length, new CodeLocation (1, 1)), Is.EqualTo (0));
		}

		#region DDoc

		public void DDocMacros()
		{
			var frstParam = "a\"s\"";
			var scndParam = " ((d)('\\'')) ";
			var thirdParam = " hehe";

			var ddoc = "asdf $(NAME) yeah $(D "+frstParam+","+scndParam+","+thirdParam+")";

			int macroStart;
			int macroLength;
			string macroName;
			Dictionary<string,string> parameters;

			DDocParser.FindNextMacro(ddoc, 0, out macroStart, out macroLength, out macroName, out parameters);

			Assert.That (macroStart, Is.EqualTo(5));
			Assert.That (macroLength, Is.EqualTo(7));
			Assert.That (macroName, Is.EqualTo("NAME"));
			Assert.That (parameters, Is.Null);

			DDocParser.FindNextMacro(ddoc, macroStart + macroLength, out macroStart, out macroLength, out macroName, out parameters);

			Assert.That (macroName, Is.EqualTo("D"));
			Assert.That (parameters, Is.Not.Null);

			Assert.That (parameters["$0"], Is.EqualTo(frstParam+","+scndParam+","+thirdParam));
			Assert.That (parameters["$1"], Is.EqualTo(frstParam));
			Assert.That (parameters["$2"], Is.EqualTo(scndParam));
			Assert.That (parameters["$3"], Is.EqualTo(thirdParam));
			Assert.That (parameters["$+"], Is.EqualTo(scndParam+","+thirdParam));
		}

		#endregion
	}
}
