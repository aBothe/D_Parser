using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using D_Parser;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
	[TestClass]
	public class ParseTests
	{
		[TestMethod]
		public void RealLiterals()
		{
			var e = DParser.ParseExpression("0x1.921fb54442d18469898cc51701b84p+1L");
			
			Assert.IsInstanceOfType(e, typeof(ScalarConstantExpression));
			var id = e as ScalarConstantExpression;
			Assert.IsTrue(Math.Abs((decimal)id.Value - (decimal)Math.PI) < 0.1M);
		}
		
		[TestMethod]
		public void ParseEscapeLiterals()
		{
			var e = DParser.ParseExpression(@"`a`\n""lolol""");

			Assert.AreEqual("a\nlolol", (e as StringLiteralExpression).Value);
		}

		[TestMethod]
		public void ParseEmptyCharLiteral()
		{
			var e = DParser.ParseExpression("['': false, '&': true, '=': true]");

			Assert.IsInstanceOfType(e, typeof(AssocArrayExpression));
			var aa = (AssocArrayExpression)e;

			Assert.AreEqual(3, aa.Elements.Count);
		}

		[TestMethod]
		public void RefOnlyMethodDecl()
		{
			var mod = DParser.ParseString(@"ref foo(auto const ref char c) {}");
			Assert.AreEqual(0, mod.ParseErrors.Count);
			var dm = mod ["foo"].First () as DMethod;
			Assert.IsInstanceOfType(dm, typeof(DMethod));
			Assert.AreEqual("c", dm.Parameters [0].Name);
		}

		[TestMethod]
		public void CharLiteralBug()
		{
			string code = "'@@'";
			var sr = new StringReader (code);

			var lex = new Lexer (sr);
			lex.NextToken ();

			Assert.AreEqual(1, lex.LexerErrors.Count);
			Assert.AreEqual(DTokens.Literal, lex.LookAhead.Kind);
			Assert.AreEqual(LiteralFormat.CharLiteral, lex.LookAhead.LiteralFormat);
		}

		[TestMethod]
		public void Unicode1()
		{
			var lex = new Lexer (new StringReader ("'ߞ'"));

			lex.NextToken ();

			Assert.AreEqual(0, lex.LexerErrors.Count);
		}

		[TestMethod]
		public void Unicode2()
		{
			var lex = new Lexer(new StringReader("'😃'")); // 3 byte UTF8, not single word UTF16

			lex.NextToken();

			Assert.AreEqual(0, lex.LexerErrors.Count);
			Assert.AreEqual(DTokens.Literal, lex.LookAhead.Kind);
			Assert.AreEqual(LiteralFormat.CharLiteral, lex.LookAhead.LiteralFormat);
			Assert.AreEqual("😃", lex.LookAhead.ToString());
		}

		[TestMethod]
		public void TestSyntaxError4()
		{
			var mod = DParser.ParseString (@"module A;
class A {
    private {
        int a;
    }
}");
			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void TestSyntaxError5()
		{
			var mod = DParser.ParseString (@"module A;
void main(){
	if (extra * extra * 2 < y.length * y.length)
		int derp;
}
");
			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void TestSyntaxError6()
		{
			var mod = DParser.ParseString(@"module A;
void main(){
	assert(!__traits(compiles, immutable(S44)(3, &i)));
}
");
			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void TestSyntaxError7()
		{
			var mod = DParser.ParseString(@"module A;
auto sourceCode = q{~this(){}}c;
");
			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void TestSyntaxError8()
		{
			var mod = DParser.ParseString(@"static if (is(__traits(parent, A) : __traits(parent, B))) {}");
			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void TestSyntaxError3()
		{
			DModule mod;

			mod = DParser.ParseString (@"enum a = __traits(compiles, zip((S[5]).init[]));");
			Assert.AreEqual(0, mod.ParseErrors.Count);

			mod = DParser.ParseString ("enum a = (new E[sizes[0]]).ptr;");
			Assert.AreEqual(0, mod.ParseErrors.Count);

			mod = DParser.ParseString (@"
			enum ptrdiff_t findCovariantFunction =
            is(typeof((             Source).init.opDispatch!(finfo.name)(Params.init))) ||
            is(typeof((       const Source).init.opDispatch!(finfo.name)(Params.init))) ||
            is(typeof((   immutable Source).init.opDispatch!(finfo.name)(Params.init))) ||
            is(typeof((      shared Source).init.opDispatch!(finfo.name)(Params.init))) ||
            is(typeof((shared const Source).init.opDispatch!(finfo.name)(Params.init)));");
			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void TestIsVector()
		{
			var mod = DParser.ParseString(@"alias T = int; static if (is(T == __vector) ) {}");
			Assert.AreEqual(0, mod.ParseErrors.Count);

			var mod2 = DParser.ParseString(@"alias T = int; static if (is(T == __parameters) ) {}");
			Assert.AreEqual(0, mod2.ParseErrors.Count);
		}

		[TestMethod]
		public void TestAsmStorageClasses()
		{
			var mod = DParser.ParseString (@"void foo() {  asm @nogc nothrow { naked; } }");
			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void TestAsmOperands()
		{
			var mod = DParser.ParseString (@"int foo(int x, int y)
{
    asm
    {
db 5,6,0x83;   // insert bytes 0x05, 0x06, and 0x83 into code
ds 0x1234;     // insert bytes 0x34, 0x12
di 0x1234;     // insert bytes 0x34, 0x12, 0x00, 0x00
dl 0x1234;     // insert bytes 0x34, 0x12, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
df 1.234;      // insert float 1.234
dd 1.234;      // insert double 1.234
de 1.234;      // insert real 1.234
db ""abc"";      // insert bytes 0x61, 0x62, and 0x63
ds ""abc"";      // insert bytes 0x61, 0x00, 0x62, 0x00, 0x63, 0x00

mov EAX,x << 2;
mov y, EAX + 1;
    }
}");
			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
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
			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
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
			Assert.AreEqual(0, mod.ParseErrors.Count);

			var e = DParser.ParseExpression("new ubyte[size]");

			var s = DParser.ParseBlockStatement(@"long neIdx = find(pResult, ""{/loop");

			mod = DParser.ParseString(@"void foo() {}

//* one line
void bar();");

			Assert.AreEqual(2, mod.Children.Count);
            Assert.IsInstanceOfType(mod["bar"].First(), typeof(DMethod));
		}

		[TestMethod]
		public void Test2_066UCSnytax()
		{
			var s = "auto b = creal(3+4i);";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
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

			Assert.AreEqual(0, mod.ParseErrors.Count);

			var foo = mod ["foo"].First () as DMethod;
			Assert.AreEqual(1, foo.Children.Count);
		}

		[TestMethod]
		public void TokenStrings()
		{
			var s = @"auto ss = q""[<?xml version=""1.0""?>
				<title>XML Developer's Guide</title>]"";";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void SyntaxError_Issue188()
		{
			var s = @"
ref int foo(return ref int a) { return a; }
ref myclass mufunc () return  { }";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
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

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void SyntaxError_Issue185()
		{
			var s = "alias Dg13832 = ref int delegate();";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

        [TestMethod]
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

            Assert.AreEqual(0, mod.ParseErrors.Count);
        }

		[TestMethod]
		public void SyntaxError_Issue178()
		{
			var s = "static assert(!__traits(compiles, v1 * v2));";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
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

            Assert.AreEqual(0, mod.ParseErrors.Count);
		}

        [TestMethod]
        public void SyntaxError_Issue173()
        {
            var s = @"typeof(s).Foo j;";
            var mod = DParser.ParseString(s);

            Assert.AreEqual(0, mod.ParseErrors.Count);
        }

        [TestMethod]
        public void SyntaxError_Issue172()
        {
            var s = @"
static assert(!__traits(compiles, immutable Nullable1([1,2,3])));
static assert( __traits(compiles, immutable Nullable1([1,2,3].idup)));
static assert(!__traits(compiles,    shared Nullable1([1,2,3])));
static assert(!__traits(compiles,    shared Nullable1([1,2,3].idup)));
";
            var mod = DParser.ParseString(s);

            Assert.AreEqual(0, mod.ParseErrors.Count);
        }

        [TestMethod]
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

            Assert.AreEqual(0, mod.ParseErrors.Count);
        }

		[TestMethod]
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

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
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

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void SyntaxError_Issue166()
		{
			var s = "mixin .mix;";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
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

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void SyntaxError_Issue164()
		{
			var s = @"// bug 6584
version(9223372036854775807){}
debug(9223372036854775807){}";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
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

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void SyntaxError_Issue162()
		{
			var s = @"int foo19(alias int a)() { return a; }";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
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

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
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

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void SyntaxError_Issue159()
		{
			var s = @"mixin typeof(b).Def!(int);";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void SyntaxError_Issue158()
		{
			var s = @"int array1[3] = [1:1,2,0:3];";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void SyntaxError_Issue157()
		{
			var s = @"module protection.subpkg.explicit;

package(protection) void commonAncestorFoo();
package(protection.subpkg) void samePkgFoo();";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void SyntaxError_Issue156()
		{
			var s = @"deprecated(""This module will be removed in future release."")
module imports.a12567;";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void SyntaxError_Issue156_()
		{
			var s = @"deprecated(""This module will be removed in future release."")
module imports.a12567;

module asdf;";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(mod.ParseErrors.Count, 1);
		}

		[TestMethod]
		public void SyntaxError_Issue155()
		{
			var s = @"static assert(cast(bool)set.isSet(fd) == cast(bool)(() @trusted => FD_ISSET(fd, fdset))());
enum dg7761 = (int a) pure => 2 * a;";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void SyntaxError_Issue154()
		{
			var s = @"
auto a1 = immutable (Nullable!int)();
auto a2 = immutable (Nullable!int)(1);
auto i = a2.get;";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void SyntaxError_Issue153()
		{
			var s = @"alias fnRtlAllocateHeap = extern(Windows) void* function(void* HeapHandle, uint Flags, size_t Size) nothrow;";
			var mod = DParser.ParseString(s);

			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void TestSyntaxError2()
		{
			var s = "class Foo( if(is(T==float) {} class someThingElse {}";
			var mod = DParser.ParseString (s);

			Assert.IsTrue(mod.ParseErrors.Count >= 1);
		}

		[TestMethod]
		public void LongNumberLiteral()
		{
			var mod = DParser.ParseString(@"
version(9223372036854775807){}
debug(9223372036854775807){}
");
		}

		[TestMethod]
		public void ObsoleteArrayNotation()
		{
			var mod = DParser.ParseString(@"int arr[]; int[] brr;");

			var arr = mod["arr"].First() as DVariable;
			var brr = mod["brr"].First() as DVariable;

			Assert.AreEqual(brr.Type.ToString(), arr.Type.ToString());
		}

		[TestMethod]
		public void Attributes1()
		{
			var n = DParser.ParseString("align(2) align int a;");

			var a = n["a"].First() as DVariable;
			Assert.IsNotNull(a);
			Assert.AreEqual(1, a.Attributes.Count);

			var attr = a.Attributes[0] as Modifier;
			Assert.AreEqual(DTokens.Align, attr.Token);
			Assert.IsTrue(attr.LiteralContent == null || attr.LiteralContent as string == string.Empty);

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

		[TestMethod]
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
			Assert.IsTrue (a.ContainsAnyAttribute(DTokens.Private));
			Assert.AreEqual(0,b.Attributes.Count);
			
			var A = m["A"].First() as DVariable;
			var B = m["B"].First() as DVariable;
			var C = m["C"].First() as DVariable;

			Assert.AreEqual(1, A.Attributes.Count);
			Assert.AreEqual(2,B.Attributes.Count);
			Assert.AreEqual(2, C.Attributes.Count);

			Assert.IsInstanceOfType((m["foo"].First() as DMethod).TemplateConstraint, typeof(IsExpression));
		}

		[TestMethod]
		public void DeclConditionElseSection()
		{
			var m = DParser.ParseString (@"module A;
version(VersA)
	int a;
else:

int b;
");
			Assert.AreEqual(0, m.ParseErrors.Count);
			DNode dn;

			dn = m ["a"].First () as DNode;
			Assert.AreEqual(1, dn.Attributes.Count);
			Assert.IsInstanceOfType(dn.Attributes [0], typeof(VersionCondition));

			dn = m ["b"].First () as DNode;
			Assert.AreEqual(1, dn.Attributes.Count);
			Assert.IsInstanceOfType(dn.Attributes [0], typeof(NegatedDeclarationCondition));
			Assert.IsInstanceOfType((dn.Attributes [0] as NegatedDeclarationCondition).FirstCondition, typeof(VersionCondition));
		}

		[TestMethod]
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
			Assert.AreEqual(0, m.ParseErrors.Count);
			DNode dn;
			DMethod dm;

			var Class = m ["Class"].First() as DClassLike;
			Assert.IsTrue (Class.ContainsAnyAttribute (DTokens.Final));
			dn = Class ["statFoo"].First () as DNode;
			Assert.AreEqual(2, dn.Attributes.Count);
			Assert.IsTrue (dn.ContainsAnyAttribute(DTokens.Public));
			Assert.IsTrue (dn.ContainsAnyAttribute(DTokens.Static));

			dm = dn as DMethod;
			dn = dm.Parameters [0] as DNode;
			Assert.AreEqual(0, dn.Attributes.Count);
			Assert.IsTrue (dn.IsPublic);

			dn = dm.Parameters [1] as DNode;
			Assert.AreEqual(1, dn.Attributes.Count);
			Assert.IsTrue(dn.ContainsAnyAttribute(DTokens.Ref));

			dn = (dm.Body.SubStatements.First() as IDeclarationContainingStatement).Declarations[0] as DNode;
			Assert.AreEqual(0, dn.Attributes.Count);



			dn = Class ["statBar"].First () as DNode;
			Assert.AreEqual(3, dn.Attributes.Count);
			Assert.IsTrue (dn.ContainsAnyAttribute(DTokens.Public));
			Assert.IsTrue (dn.ContainsAnyAttribute(DTokens.Static));
			Assert.IsTrue (dn.ContainsAnyAttribute(DTokens.Nothrow));

			dm = dn as DMethod;
			dn = dm.Parameters [0] as DNode;
			Assert.AreEqual(0, dn.Attributes.Count);
			Assert.IsTrue (dn.IsPublic);

			dn = dm.Parameters [1] as DNode;
			Assert.AreEqual(1, dn.Attributes.Count);
			Assert.IsTrue(dn.ContainsAnyAttribute(DTokens.Ref));

			dn = (dm.Body.SubStatements.First() as IDeclarationContainingStatement).Declarations[0] as DNode;
			Assert.AreEqual(0, dn.Attributes.Count);



			dn = Class["priv"].First() as DNode;
			Assert.AreEqual(1, dn.Attributes.Count);
			Assert.IsTrue(dn.ContainsAnyAttribute(DTokens.Private));

			dn = m["ii"].First() as DNode;
			Assert.AreEqual(0, dn.Attributes.Count);
		}
		
		[TestMethod]
		public void AccessExpression1()
		{
			var e1 = DParser.ParseExpression("a.b!c");
			var e2 = DParser.ParseExpression("a.b");
			
			Assert.IsInstanceOfType(e1, typeof(PostfixExpression_Access));
			Assert.IsInstanceOfType(e2, typeof(PostfixExpression_Access));
		}

		[TestMethod]
		public void Expr1()
		{
			var m = DParser.ParseString(@"module A;
void main() {
	if(*p == 0)
	{}
}");

			Assert.AreEqual(0, m.ParseErrors.Count);
		}
		
		//[TestMethod]
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

		//[TestMethod]
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
			Assert.IsTrue(string.IsNullOrEmpty(sb.ToString()));
			Assert.IsFalse(hadErrors);
		}

		public static LegacyParseCacheView ParsePhobos(bool ufcs=true)
		{
			var dir = "/usr/include/d";

			GlobalParseCache.ParseTaskFinished += pc_FinishedParsing;
			var statHandle = GlobalParseCache.BeginAddOrUpdatePaths (new[] { dir }, false)[0];

			Assert.IsTrue(statHandle.WaitForCompletion(60 * 1000));

			return new LegacyParseCacheView(new[]{dir});
		}

		static void pc_FinishedParsing(ParsingFinishedEventArgs ppd)
		{
			Trace.WriteLine(string.Format("Parsed {0} files in {1}; {2}ms/file", ppd.FileAmount, ppd.Directory, ppd.FileDuration), "ParserTests");
		}

		[TestMethod]
		public void ParsePerformance1()
		{
			//var pc = ParsePhobos(false);
			var pcl = ResolutionTestHelper.CreateCache(out DModule m, @"module modA;

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
			var main = m["main"].First() as DMethod;
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

		[TestMethod]
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

			var n = ASTSearchHelper.SearchBlockAt(m, ((IBlockNode)m["A"].First())["d"].First().Location);
			Assert.AreEqual("A", n.Name);

			var loc = ((IBlockNode)m["C"].First()).BlockStartLocation;
			n = ASTSearchHelper.SearchBlockAt(m, loc);
			Assert.AreEqual("C", n.Name);

			loc = ((IBlockNode)((IBlockNode)m["B"].First())["subB"].First())["c"].First().Location;
			n = ASTSearchHelper.SearchBlockAt(m, loc);
			Assert.AreEqual("subB", n.Name);

			n = ASTSearchHelper.SearchBlockAt(m, new CodeLocation(1, 10));
			Assert.AreEqual(m,n);

			loc = (((DMethod)m["main"].First()).Body.First() as IDeclarationContainingStatement).Declarations[0].EndLocation;
			n = ASTSearchHelper.SearchBlockAt(m, loc);
			Assert.AreEqual("main", n.Name);
		}

		[TestMethod]
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
			Assert.IsTrue (foo.Attributes.Count == 0);

			Assert.AreEqual(1, m.StaticStatements.Count);
			Assert.IsInstanceOfType(m.StaticStatements[0], typeof(StaticAssertStatement));
		}
		
		[TestMethod]
		public void AliasInitializerList()
		{
			var m = DParser.ParseString(@"alias str = immutable(char)[];");

			Assert.AreEqual(0, m.ParseErrors.Count);
		}

		[TestMethod]
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
			Assert.IsNotNull(dc);
			Assert.IsTrue(dc.TryGetTemplateParameter("T".GetHashCode(), out tp));

			dc = m.Children ["isInt"].First () as EponymousTemplate;
			Assert.IsNotNull(dc);
			Assert.IsTrue(dc.TryGetTemplateParameter("T".GetHashCode(), out tp));
		}

		[TestMethod]
		/// <summary>
		/// Since 2.064. new Server(args).run(); is allowed
		/// </summary>
		public void PostNewExpressions()
		{
			var x = DParser.ParseExpression ("new Server(args).run()") as PostfixExpression;

			Assert.IsInstanceOfType(x, typeof(PostfixExpression_MethodCall));
			Assert.IsInstanceOfType((x.PostfixForeExpression as PostfixExpression_Access).PostfixForeExpression, typeof(NewExpression));
		}

		[TestMethod]
		public void SpecialTokenSequences()
		{
			var m = DParser.ParseString(@"module A;
#line 1
#line 0x123 ""ohyeah/asd.d""");

			Assert.AreEqual(0, m.ParseErrors.Count);
		}

		[TestMethod]
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
			Assert.AreEqual(0, dn.ParseErrors.Count);
			Assert.AreEqual(2, dn.MetaBlocks.Count);

			Assert.IsInstanceOfType(dn.MetaBlocks[0], typeof(AttributeMetaDeclarationSection));
			var attr = (dn.MetaBlocks [0] as AttributeMetaDeclarationSection).AttributeOrCondition [0];
			Assert.IsInstanceOfType(attr, typeof(Modifier));
			Assert.AreEqual(DTokens.Private, (attr as Modifier).Token);

			Assert.IsInstanceOfType(dn.MetaBlocks[1], typeof(AttributeMetaDeclarationBlock));
			var attr2 = (dn.MetaBlocks [1] as AttributeMetaDeclarationBlock).AttributeOrCondition [0];
			Assert.IsInstanceOfType(attr2, typeof(StaticIfCondition));

			var aa = dn ["aa"].First () as DNode;
			Assert.IsTrue (aa is DVariable);
			Assert.IsTrue (aa.Attributes.Count == 2);
			Assert.IsTrue (aa.Attributes [1] == attr2);
			Assert.IsTrue (aa.Attributes[0] == attr);

			Assert.AreEqual(3, dn.StaticStatements.Count);
			Assert.IsInstanceOfType(dn.StaticStatements[1], typeof(DebugSpecification));
			Assert.IsInstanceOfType(dn.StaticStatements[2], typeof(VersionSpecification));
			aa = dn["dbg"].First() as DNode;
			Assert.AreEqual(1, aa.Attributes.Count);
			Assert.AreSame(attr, aa.Attributes[0]);
		}

		[TestMethod]
		public void RelativeOffsetCalculation()
		{
			var code = @"asdfghij";

			Assert.AreEqual(7, DocumentHelper.GetOffsetByRelativeLocation (code, new CodeLocation (4, 1), 3, new CodeLocation (8, 1)));
			Assert.AreEqual(1, DocumentHelper.GetOffsetByRelativeLocation (code, new CodeLocation (4, 1), 3, new CodeLocation (2, 1)));

			code = @"a\nb\nc";

			Assert.AreEqual(code.Length, DocumentHelper.GetOffsetByRelativeLocation (code, new CodeLocation (1, 1), 0, new CodeLocation (2, 3)));
			Assert.AreEqual(0, DocumentHelper.GetOffsetByRelativeLocation (code, new CodeLocation (2, 3), code.Length, new CodeLocation (1, 1)));
		}

		[TestMethod]
		public void AnonymousClassDef()
		{
			var code = @"{if(node.members.any!((Node n) {
/*if (auto fn = cast(FieldNode)n)	return fn.isInstanceMember;*/
return false;
})()) {}}";

			var s = DParser.ParseBlockStatement (code);
			Assert.IsNotNull(s);

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
			Assert.AreEqual(0, mod.ParseErrors.Count);
		}

		[TestMethod]
		public void EnumAttributes()
		{
			var m = DParser.ParseString(@"enum E { @disable Dis, deprecated Dep, @1234 uda }");

			Assert.AreEqual(0, m.ParseErrors.Count);

			var E = m["E"].First() as DNode;
			Assert.IsTrue(E is DEnum);

			var Dis = (E as DEnum)["Dis"].First() as DNode;
			Assert.IsTrue(Dis is DEnumValue);
			var attr = Dis.Attributes;
			Assert.IsTrue (attr.Count == 1);
			Assert.IsTrue(attr[0] is BuiltInAtAttribute);
			Assert.AreEqual(BuiltInAtAttribute.BuiltInAttributes.Disable, (attr[0] as BuiltInAtAttribute).Kind);

			var Dep = (E as DEnum)["Dep"].First() as DNode;
			Assert.IsTrue(Dep is DEnumValue);
			attr = Dep.Attributes;
			Assert.IsTrue(attr.Count == 1);
			Assert.IsTrue(attr[0] is DeprecatedAttribute);

			var uda = (E as DEnum)["uda"].First() as DNode;
			Assert.IsTrue(uda is DEnumValue);
			attr = uda.Attributes;
			Assert.IsTrue(attr.Count == 1);
			Assert.IsTrue(attr[0] is AtAttribute);
		}

		[TestMethod]
		public void ParameterUDA()
		{
			var m = DParser.ParseString(@"void example(@(22) string param) {}");

			Assert.AreEqual(0, m.ParseErrors.Count);

			var ex = m["example"].First() as DNode;
			Assert.IsTrue(ex is DMethod);
			var parameters = (ex as DMethod).Parameters;
			Assert.IsTrue(parameters.Count == 1);
			Assert.IsTrue(parameters[0] is DVariable);
			var attr = (parameters[0] as DVariable).Attributes;
			Assert.IsTrue(attr.Count == 1);
			Assert.IsTrue(attr[0] is AtAttribute);
		}

		[TestMethod]
		public void ParseContracts()
		{
			var m = DParser.ParseString(@"module A;
int fun(int x, int y)
in(x > 0)
out(r; r > 0)
in(y > 0, `message`)
out(; x > y, to!string(x))
in { assert(y > 0); }
out(r) { assert(r > 0); }
out { assert(x > y); }
do
{
	return x + y * y;
}");
			Assert.AreEqual(0, m.ParseErrors.Count);
			var fun = m["fun"].First() as DMethod;
			Assert.IsTrue(fun.Contracts.Count == 7);
			Assert.AreEqual("in(x>0)", fun.Contracts[0].ToString());
			Assert.AreEqual("out(r;r>0)", fun.Contracts[1].ToString());
			Assert.AreEqual("in(y>0,r\"message\")", fun.Contracts[2].ToString());
			Assert.AreEqual("out(;x>y,to!string(x))", fun.Contracts[3].ToString());
		}

		#region DDoc

		[TestMethod]
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

			Assert.AreEqual(5, macroStart);
			Assert.AreEqual(7, macroLength);
			Assert.AreEqual("NAME", macroName);
			Assert.IsNull(parameters);

			DDocParser.FindNextMacro(ddoc, macroStart + macroLength, out macroStart, out macroLength, out macroName, out parameters);

			Assert.AreEqual("D", macroName);
			Assert.IsNotNull(parameters);

			Assert.AreEqual(frstParam+","+scndParam+","+thirdParam, parameters["$0"]);
			Assert.AreEqual(frstParam, parameters["$1"]);
			Assert.AreEqual(scndParam, parameters["$2"]);
			Assert.AreEqual(thirdParam, parameters["$3"]);
			Assert.AreEqual(scndParam+","+thirdParam, parameters["$+"]);
		}

		#endregion
	}
}
