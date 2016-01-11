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

			Assert.That(e, Is.TypeOf(typeof(AssocArrayExpression)));
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
		public void CharLiteralBug()
		{
			string code = "'@@'";
			var sr = new StringReader (code);

			var lex = new Lexer (sr);
			lex.NextToken ();

			Assert.That (lex.LexerErrors.Count, Is.EqualTo(1));
			Assert.That (lex.LookAhead.Kind, Is.EqualTo(DTokens.Literal));
			Assert.That (lex.LookAhead.LiteralFormat, Is.EqualTo(LiteralFormat.CharLiteral));
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
		public void TestSyntaxError6()
		{
			var mod = DParser.ParseString(@"module A;
void main(){
	assert(!__traits(compiles, immutable(S44)(3, &i)));
}
");
			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void TestSyntaxError7()
		{
			var mod = DParser.ParseString(@"module A;
auto sourceCode = q{~this(){}}c;
");
			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
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
		public void TestAsmStorageClasses()
		{
			var mod = DParser.ParseString (@"void foo() {  asm @nogc nothrow { naked; } }");
			Assert.That (mod.ParseErrors.Count, Is.EqualTo (0));
		}

		[Test]
		public void TestStaticIfElseSyntaxError()
		{
			var mod = DParser.ParseString (@"
void foo() {
static if (is(ElementType!T == void)){
    static assert(0, ""try wrapping the function to get rid of void[] args"");
} else
    alias getType = ElementType!T;

static if (is(ElementType!T == void)){
    static assert(0, ""try wrapping the function to get rid of void[] args"");
} else static if(durr) {
    alias getType = ElementType!T;
}
else {
	wurr();
}

static if (is(ElementType!T == void))
    static assert(0, ""try wrapping the function to get rid of void[] args"");
else
    alias getType = ElementType!T;
}
");
			Assert.That (mod.ParseErrors.Count, Is.EqualTo (0));
		}

		[Test]
		public void TestSyntaxError1()
		{
			DModule mod;

			mod = DParser.ParseString (@"
__vector(long[2]) q;
class U : int, float, __vector(int[3]) {}
enum __vector(long[2]) v = f();
.Tuple!(int, string, float, double) a;
void main()
{
	__vector(long[2]) q;
	.Tuple!(int, string, float, double) a;
	foreach (inout(Entry)* e; aa.impl.buckets) { }
}
template X(alias pred = function int(x) => x) {}
template X(alias pred = function int(int x) => x) {}
template X(alias pred = x => x) {}
enum bool isCovariantWith(alias f, alias g) = .isCovariantWith!(typeof(f), typeof(g));

class Impl2 : C_Foo, .I_Foo
{
}
interface OutputRange(T...) : OutputRange!(T[0])
    if (T.length > 1)
{
}
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

			Assert.That(mod.Children.Count, Is.EqualTo(2));
            Assert.That(mod["bar"].First(), Is.TypeOf(typeof(DMethod)));
		}

		[Test]
		public void Test2_066UCSnytax()
		{
			var s = "auto b = creal(3+4i);";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void NestedLambdas()
		{
			var s = @"module A;
void foo(){
if (node.members.any!((Node n) {
if (auto fn = cast(FieldNode)n)					return fn.isInstanceMember;
return false;
}))
{}
}";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));

			var foo = mod ["foo"].First () as DMethod;
			Assert.That (foo.Children.Count, Is.EqualTo (1));
		}

		[Test]
		public void TokenStrings()
		{
			var s = @"auto ss = q""[<?xml version=""1.0""?>
				<title>XML Developer's Guide</title>]"";";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void SyntaxError_Issue188()
		{
			var s = @"
ref int foo(return ref int a) { return a; }
ref myclass mufunc () return  { }";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void SyntaxError_Issue186()
		{
			var s = @"
struct S1 { bool opEquals(T : typeof(this))(T) { return false; } }
struct S2 { bool opEquals(T : typeof(this))(T) { return false; } }
struct S3 { bool opEquals(T : typeof(this))(T) { return false; } }
struct S4 { bool opEquals(T : typeof(this))(T) { return false; } }
struct S5 { bool opEquals(T : typeof(this))(T) { return false; } }
struct S6 { bool opEquals(T : typeof(this))(T) { return false; } ~this(){} }
struct S7 { bool opEquals(T : typeof(this))(T) { return false; } }
struct S8 { bool opEquals(T : typeof(this))(T) { return false; } }
struct S9 { bool opEquals(T : typeof(this))(T) { return false; } }

struct S10 { bool opEquals(T : typeof(this))(T) { return false; } }
struct S11 { bool opEquals(T : typeof(this))(T) const { return false; }
             int opCmp(T : typeof(this))(T) const { return 0; }
             size_t toHash() const nothrow @safe { return 0; } }
struct S12 { bool opEquals(T : typeof(this))(T) { return false; } }
struct S13 { bool opEquals(T : typeof(this))(T) { return false; } }
struct S14 { bool opEquals(T : typeof(this))(T) { return false; } }
struct S15 { bool opEquals(T : typeof(this))(T) { return false; } }
struct S16 { bool opEquals(T : typeof(this))(T) { return false; } }
struct S17 { bool opEquals(T : typeof(this))(T) { return false; } }
struct S18 { bool opEquals(T : typeof(this))(T) { return false; } }

void fun()()
{
    { auto a = new S1[1]; }
    { auto p = new S2(); }
    { alias P = S3*; auto p = new P; }
    { S4[int] aa; auto b = (aa == aa); }
    { S5[] a; a.length = 10; }
    { S6[] a; delete a; }
    { S7[] a = []; }
    { S8[] a = [S8.init]; }
    { S9[int] aa = [1:S9.init]; }

    { auto ti = typeid(S10[int]); }
    { auto ti = typeid(int[S11]); }
    { auto ti = typeid(S12[]); }
    { auto ti = typeid(S13*); }
    { auto ti = typeid(S14[3]); }
    { auto ti = typeid(S15 function()); }
    { auto ti = typeid(S16 delegate()); }
    { auto ti = typeid(void function(S17)); }   // TypeInfo_Function doesn't have parameter types
    { auto ti = typeid(void delegate(S18)); }   // ditto
}

struct B12146
{
    bool opCmp(ubyte val) { return false; }
}
";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void SyntaxError_Issue185()
		{
			var s = "alias Dg13832 = ref int delegate();";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

        [Test]
        public void SyntaxError_Issue175()
        {
            var s = @"void test4()
{    
     printf(""main() : (-128 >= 0)=%s, (-128 <= 0)=%s\n"", 
              cast(char*)(-128 >= 0 ? ""true"" : ""false""),
              cast(char*)(-128 <= 0 ?  ""true"" : ""false""));     

     printf(""main() : (128 >= 0)=%s, (128 <= 0)=%s\n"", 
              cast(char*)(128 >= 0 ? ""true"" : ""false""),
              cast(char*)(128 <= 0 ?  ""true"" : ""false""));

     assert((-128 >= 0 ? ""true"" : ""false"") == ""false""),
     assert((-128 <= 0 ? ""true"" : ""false"") == ""true"");
     assert((+128 >= 0 ? ""true"" : ""false"") == ""true""),
     assert((+128 <= 0 ? ""true"" : ""false"") == ""false"");     
}";
            var mod = DParser.ParseString(s);

            Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
        }

		[Test]
		public void SyntaxError_Issue178()
		{
			var s = "static assert(!__traits(compiles, v1 * v2));";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void SyntaxError_Issue177()
		{
			var s = @"static assert( s1[20..30, 10]           == tuple("" []"", [0, 20, 30], 10));
static assert( s1[10, 10..$, $-4, $..2,] == tuple("" []"", 10, [1,10,99], 99-4, [3,99,2]));
static assert(+s1[20..30, 10,]           == tuple(""+[]"", [0, 20, 30], 10));
static assert(-s1[10, 10..$, $-4, $..2] == tuple(""-[]"", 10, [1,10,99], 99-4, [3,99,2]));
static assert((s1[20..30, 10]           =""x"") == tuple(""[] ="", ""x"", [0, 20, 30], 10));
static assert((s1[10, 10..$, $-4, $..2] =""x"") == tuple(""[] ="", ""x"", 10, [1,10,99], 99-4, [3,99,2]));
static assert((s1[20..30, 10]          +=""x"") == tuple(""[]+="", ""x"", [0, 20, 30], 10));
static assert((s1[10, 10..$, $-4, $..2]-=""x"") == tuple(""[]-="", ""x"", 10, [1,10,99], 99-4, [3,99,2]));";
			var mod = DParser.ParseString(s);

            Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

        [Test]
        public void SyntaxError_Issue173()
        {
            var s = @"typeof(s).Foo j;";
            var mod = DParser.ParseString(s);

            Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
        }

        [Test]
        public void SyntaxError_Issue172()
        {
            var s = @"
static assert(!__traits(compiles, immutable Nullable1([1,2,3])));
static assert( __traits(compiles, immutable Nullable1([1,2,3].idup)));
static assert(!__traits(compiles,    shared Nullable1([1,2,3])));
static assert(!__traits(compiles,    shared Nullable1([1,2,3].idup)));
";
            var mod = DParser.ParseString(s);

            Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
        }

        [Test]
        public void SyntaxError_Issue171()
        {
            var s = @"
void foo() { 
s = q""myID schwing const ref byte myID"";
s = q{{foo}""}""};
s = q""(( HURR DURR const token ref uint 'c'  ))"";
assert(s == ""{foo}\""}\"""");
}
";
            var mod = DParser.ParseString(s);

            Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
        }

		[Test]
		public void SyntaxError_Issue169()
		{
			var s = @"
void test52()
{
    size_t vsize = void.sizeof;
    assert(vsize == 1);
}
";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void SyntaxError_Issue168()
		{
			var s = @"
class C7 {
    public this(){
    }
}

interface I7 {
    void fnc();
}

void test7()
{
    char[][] t;
    foreach( char[] c; t ){
        new class( c ) C7, I7 {
            public this( char[] c ){
                super();
            }
            void fnc(){
            }
        };
    }
}
";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void SyntaxError_Issue166()
		{
			var s = "mixin .mix;";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void SyntaxError_Issue165()
		{
			var s = @"void test56()
{
    assert('\&sup2;'==178);
    assert('\&sup3;'==179);
    assert('\&sup1;'==185);
    assert('\&frac14;'==188);
    assert('\&frac12;'==189);
    assert('\&frac34;'==190);
    assert('\&there4;'==8756);
}";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void SyntaxError_Issue164()
		{
			var s = @"// bug 6584
version(9223372036854775807){}
debug(9223372036854775807){}";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void SyntaxError_Issue163()
		{
			var s = @"class B2540 : A2540
{
    int b;
    override super.X foo() { return 1; }

    alias this athis;
    alias this.b thisb;
    alias super.a supera;
    alias super.foo superfoo;
    alias this.foo thisfoo;
}";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void SyntaxError_Issue162()
		{
			var s = @"int foo19(alias int a)() { return a; }";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void SyntaxError_Issue161()
		{
			var s = @"
mixin template node9026()
{
    static if (is(this == struct))
        alias typeof(this)* E;
    else
        alias typeof(this) E;
    E prev, next;
}";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void SyntaxError_Issue160()
		{
			var s = @"
struct Bar5832(alias v) {}

template isBar5832a(T)
{
    static if (is(T _ : Bar5832!(v), alias v))
        enum isBar5832a = true;
    else
        enum isBar5832a = false;
}
template isBar5832b(T)
{
    static if (is(T _ : Bar5832!(v), alias int v))
        enum isBar5832b = true;
    else
        enum isBar5832b = false;
}
template isBar5832c(T)
{
    static if (is(T _ : Bar5832!(v), alias string v))
        enum isBar5832c = true;
    else
        enum isBar5832c = false;
}
static assert( isBar5832a!(Bar5832!1234));
static assert( isBar5832b!(Bar5832!1234));
static assert(!isBar5832c!(Bar5832!1234));";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void SyntaxError_Issue159()
		{
			var s = @"mixin typeof(b).Def!(int);";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void SyntaxError_Issue158()
		{
			var s = @"int array1[3] = [1:1,2,0:3];";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void SyntaxError_Issue157()
		{
			var s = @"module protection.subpkg.explicit;

package(protection) void commonAncestorFoo();
package(protection.subpkg) void samePkgFoo();";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void SyntaxError_Issue156()
		{
			var s = @"deprecated(""This module will be removed in future release."")
module imports.a12567;";
			var mod = DParser.ParseString(s);

			Assert.That(mod.ParseErrors.Count, Is.EqualTo(0));
		}

		[Test]
		public void SyntaxError_Issue156_()
		{
			var s = @"deprecated(""This module will be removed in future release."")
module imports.a12567;

module asdf;";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(mod.ParseErrors.Count, 1);
		}

		[Test]
		public void SyntaxError_Issue155()
		{
			var s = @"static assert(cast(bool)set.isSet(fd) == cast(bool)(() @trusted => FD_ISSET(fd, fdset))());
enum dg7761 = (int a) pure => 2 * a;";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[Test]
		public void SyntaxError_Issue154()
		{
			var s = @"
auto a1 = immutable (Nullable!int)();
auto a2 = immutable (Nullable!int)(1);
auto i = a2.get;";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[Test]
		public void SyntaxError_Issue153()
		{
			var s = @"alias fnRtlAllocateHeap = extern(Windows) void* function(void* HeapHandle, uint Flags, size_t Size) nothrow;";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[Test]
		public void TestSyntaxError2()
		{
			var s = "class Foo( if(is(T==float) {} class someThingElse {}";
			var mod = DParser.ParseString (s);

			Assert.GreaterOrEqual(mod.ParseErrors.Count,1);
		}

		[Test]
		public void LongNumberLiteral()
		{
			var mod = DParser.ParseString(@"
version(9223372036854775807){}
debug(9223372036854775807){}
");
		}

		[Test]
		public void ObsoleteArrayNotation()
		{
			var mod = DParser.ParseString(@"int arr[]; int[] brr;");

			var arr = mod["arr"].First() as DVariable;
			var brr = mod["brr"].First() as DVariable;

			Assert.That(arr.Type.ToString(), Is.EqualTo(brr.Type.ToString()));
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
			foreach (var root in pc.EnumRootPackagesSurroundingModule(null))
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

		public static LegacyParseCacheView ParsePhobos(bool ufcs=true)
		{
			var dir = "/usr/include/d";

			GlobalParseCache.ParseTaskFinished += pc_FinishedParsing;
			GlobalParseCache.BeginAddOrUpdatePaths (new[] { dir }, false);

			GlobalParseCache.WaitForFinish();

			return new LegacyParseCacheView(new[]{dir});
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
			var main = pcl.FirstPackage()["modA"]["main"].First() as DMethod;
			Assert.AreEqual(0, (pcl.FirstPackage()["modA"] as DModule).ParseErrors.Count);
			var s = main.Body.SubStatements.Last() as IExpressionContainingStatement;
			var ctxt = ResolutionContext.Create(pcl, null, main, s.Location);
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

			var n = DResolver.SearchBlockAt(m, ((IBlockNode)m["A"].First())["d"].First().Location);
			Assert.AreEqual("A", n.Name);

			var loc = ((IBlockNode)m["C"].First()).BlockStartLocation;
			n = DResolver.SearchBlockAt(m, loc);
			Assert.AreEqual("C", n.Name);

			loc = ((IBlockNode)((IBlockNode)m["B"].First())["subB"].First())["c"].First().Location;
			n = DResolver.SearchBlockAt(m, loc);
			Assert.AreEqual("subB", n.Name);

			n = DResolver.SearchBlockAt(m, new CodeLocation(1, 10));
			Assert.AreEqual(m,n);

			loc = (((DMethod)m["main"].First()).Body.First() as IDeclarationContainingStatement).Declarations[0].EndLocation;
			n = DResolver.SearchBlockAt(m, loc);
			Assert.AreEqual("main", n.Name);
		}

		[Test]
		public void StaticIf()
		{
			var m = DParser.ParseString(@"
static assert(true);
void foo()
{
	static assert(st.a==34);
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
			var ctxt = ResolutionTests.CreateCtxt ("std.stdio", @"module std.stdio;
			void writeln() {}");
			bool isCFun;
			var t = Demangler.Demangle("_D3std5stdio35__T7writelnTC3std6stream4FileTAAyaZ7writelnFC3std6stream4FileAAyaZv", ctxt, out q, out isCFun);

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
			Assert.That (aa.Attributes [1] == attr2);
			Assert.That (aa.Attributes[0] == attr);

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

		[Test]
		public void AnonymousClassDef()
		{
			var code = @"{if(node.members.any!((Node n) {
/*if (auto fn = cast(FieldNode)n)	return fn.isInstanceMember;*/
return false;
})()) {}}";

			var s = DParser.ParseBlockStatement (code);
			Assert.That (s, Is.Not.Null);

			//return;
			code = @"
void anonClassFoo()
{
	size_t embodiedClassCount = void;
	auto vis2 = new class NodeVisitor
	{
		override void visit(ClassNode node)
		{
			import std.array;
			import std.algorithm : any;
			
			if (node.members.any!((Node n) {
				if (auto fn = cast(FieldNode)n)					return fn.isInstanceMember;
				return false;
			})())
			{
				embodiedClassCount++;
				embodiedClasses[node.fullName] = true;
			}
			else if (auto p = node.baseClass in embodiedClasses)
			{
				embodiedClassCount++;
				embodiedClasses[node.fullName] = *p;
			}
		}
	};

	do
	{
		embodiedClassCount = 0;
		
		foreach (ref n; nodes)
			n.visit(vis2);
	} while (embodiedClassCount);
}
";

			var mod = DParser.ParseString (code);
			Assert.That (mod.ParseErrors.Count, Is.EqualTo (0));
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
