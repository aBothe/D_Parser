using System.Collections.Generic;
using System.IO;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using NUnit.Framework;

namespace Tests.Resolution
{
	[TestFixture]
	public class DeclDefScopingTests : ResolutionTestHelper
	{
		[Test]
		public void BasicResolution0()
		{
			var pcl = CreateCache(
@"module modA; import modC;", // Searching for 'T' will always deliver the definition from A, never from B
@"module modB; import modC, modD;", // Searching for 'T' will result in an ambigous definition, independently of any kinds of restricting constraints!
@"module modC; 
public import modD; 
/** Class 1 */ 
class T{}",

@"module modD; 
/** Class 2 */
class T{ int t2; }",

@"module modE; 
/** Overload 1 */ 
class U{} 
/** Overload 2 */ 
class U{}

class N(X){ 
class X { int m; }
void foo() {}
}",

@"module modF;

void ni() {}

void asdf(int* ni=23) {
	if(t.myMember < 50)
	{
		bool ni = true;
		ni;
	}
}");

			var ctxt = CreateDefCtxt(pcl, pcl.FirstPackage()["modA"]);

			var t = R("T", ctxt);
			Assert.That(t.Count, Is.EqualTo(1));
			Assert.That((t[0] as DSymbol).Definition.Parent, Is.SameAs(pcl.FirstPackage()["modC"]));

			ctxt.CurrentContext.Set(pcl.FirstPackage()["modC"], CodeLocation.Empty);
			t = R("T", ctxt);
			Assert.That(t.Count, Is.EqualTo(1));
			Assert.That((t[0] as DSymbol).Definition.Parent, Is.SameAs(pcl.FirstPackage()["modC"]));

			ctxt.CurrentContext.Set(pcl.FirstPackage()["modB"], CodeLocation.Empty);
			t = R("T", ctxt);
			Assert.That(t.Count, Is.EqualTo(2));

			ctxt.ResolutionErrors.Clear();
			ctxt.CurrentContext.Set(N<D_Parser.Dom.DModule>(ctxt, "modE"), CodeLocation.Empty);
			t = R("U", ctxt);
			Assert.That(t.Count, Is.EqualTo(2));

			ctxt.CurrentContext.Set(N<DMethod>(ctxt, "modE.N.foo"), CodeLocation.Empty);
			t = R("X", ctxt);
			Assert.That(t.Count, Is.EqualTo(1));
			Assert.That(t[0], Is.TypeOf(typeof(ClassType)));

			var f = N<DMethod>(ctxt, "modF.asdf");
			ctxt.CurrentContext.Set(f);
			t = ExpressionTypeEvaluation.GetOverloads(new IdentifierExpression("ni") { Location = S(f, 0, 0, 1).Location }, ctxt);
			Assert.That((t[0] as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(t.Count, Is.EqualTo(1)); // Was 2; Has been changed to 1 because it's only important to take the 'nearest' declaration that occured before the resolved expression

			t = ExpressionTypeEvaluation.FilterOutByResultPriority(ctxt, t);
			Assert.That(t.Count, Is.EqualTo(1));
		}

		[Test]
		public void BasicResolution()
		{
			var ctxt = CreateCtxt("modA", @"module modA;
import B;
class foo : baseFoo {
	
}",
								  @"module B; 
private const int privConst = 1234;

class baseFoo
{
	private static int heyHo = 234;
}");

			var t = RS("foo", ctxt);
			Assert.That(t, Is.TypeOf(typeof(ClassType)));
			Assert.AreEqual("foo", (t as ClassType).Name);

			t = RS("privConst", ctxt);
			Assert.That(t, Is.Null);

			ctxt.CurrentContext.Set(N<DClassLike>(ctxt, "modA.foo"));
			t = RS("heyHo", ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
		}

		[Test]
		public void BasicResolution1()
		{
			var pcl = CreateCache(@"module A;

int globalVar;
enum enumSym = null;

class otherClass {}

class bcl
{
	int baseA;
	static statBase;
	protected int baseB;
}

class cl : bcl
{
	int* a;
	protected int b;
	private int c;
	static int statVar;
	
	static void bar(){}
}

void foo()
{
	auto o = new cl();
	o.a;
	o.b;
	o.c;
	o.baseA;
	o.statVar;
	o.statBase;
	o.baseB;
}", @"module B; import A; cl inst;");
			var foo = pcl.FirstPackage()["A"]["foo"].First() as DMethod;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body.Location);
			var subSt = foo.Body.SubStatements as List<IStatement>;

			var t = ExpressionTypeEvaluation.EvaluateType((subSt[1] as ExpressionStatement).Expression, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.InstanceOf(typeof(PointerType)));

			t = ExpressionTypeEvaluation.EvaluateType((subSt[2] as ExpressionStatement).Expression, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));

			t = ExpressionTypeEvaluation.EvaluateType((subSt[3] as ExpressionStatement).Expression, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.InstanceOf(typeof(PrimitiveType)));

			t = ExpressionTypeEvaluation.EvaluateType((subSt[4] as ExpressionStatement).Expression, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));

			t = ExpressionTypeEvaluation.EvaluateType((subSt[5] as ExpressionStatement).Expression, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));

			t = ExpressionTypeEvaluation.EvaluateType((subSt[6] as ExpressionStatement).Expression, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));

			t = ExpressionTypeEvaluation.EvaluateType((subSt[7] as ExpressionStatement).Expression, ctxt);
			Assert.That(t, Is.Not.Null);

			ctxt.CurrentContext.Set(pcl.FirstPackage()["B"]);

			// test protected across modules
			t = ExpressionTypeEvaluation.EvaluateType((foo.Body.SubStatements.ElementAt(2) as ExpressionStatement).Expression, ctxt);
			Assert.That(t, Is.Null);

			t = ExpressionTypeEvaluation.EvaluateType((foo.Body.SubStatements.ElementAt(7) as ExpressionStatement).Expression, ctxt);
			Assert.That(t, Is.Null);

			var ex = DParser.ParseExpression("inst.b");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.Null);

			ex = DParser.ParseExpression("inst.c");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.Null);

			ctxt.CurrentContext.Set(N<DMethod>(ctxt, "A.cl.bar"));

			ex = DParser.ParseExpression("statVar");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));

			ex = DParser.ParseExpression("a");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.Null);

			ex = DParser.ParseExpression("b");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.Null);

			ex = DParser.ParseExpression("c");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.Null);

			ex = DParser.ParseExpression("statBase");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));

			ex = DParser.ParseExpression("baseA");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.Null);

			ex = DParser.ParseExpression("otherClass");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(ClassType)));

			ex = DParser.ParseExpression("globalVar");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));

			ex = DParser.ParseExpression("enumSym");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));
		}

		[Test]
		public void AnonymousNestedStructs()
		{
			var ctxt = CreateCtxt("A", @"module A;

class MyClass { union { string strA; struct { uint numA; uint numB; } } }
MyClass mc;
");
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("mc.numA");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void InMethodDeclScopes()
		{
			var ctxt = CreateCtxt("A", @"module A;

void main()
{
    enum PixelFlags : uint
    {
        AlphaPixels = 0x01,
        Alpha = 0x02,
        FourCC = 0x04,
        RGB = 0x40,
        YUV = 0x200,
        Luminance = 0x20000,
    }

    enum FourCC : uint
    {
        DXT1,
        DXT2,
        DXT3,
        DXT4,
        DXT5,
        DX10,
    }

    static struct DDS_PixelFormat
    {
    align(1):
        uint size;
        PixelFlags flags;
        FourCC fourCC;
        uint rgbBitCount;
        uint rBitMask;
        uint gBitMask;
        uint bBitMask;
        uint aBitMask;
    }

    static struct DDS_Header
    {
    align(1):
        uint size;
        uint flags;
        uint height;
        uint width;
        uint pitchOrLinearSize;
        uint depth;
        uint mipMapCount;
        uint[11] reserved1;
        DDS_PixelFormat pixFormat;
        uint caps;
        uint caps2;
        uint caps3;
        uint caps4;
        uint reserved2;
    }
}
");

			var A = ctxt.MainPackage()["A"];
			var main = A["main"].First() as DMethod;
			var DDS_Header = main["DDS_Header"].First() as DClassLike;
			var pixFormat = DDS_Header["pixFormat"].First() as DVariable;

			ctxt.CurrentContext.Set(DDS_Header);

			AbstractType t;

			t = RS(pixFormat.Type, ctxt);
			Assert.That(t, Is.TypeOf(typeof(StructType)));
		}

		/// <summary>
		/// Accessing a non-static field without a this reference is only allowed in certain contexts:
		/// 		Accessing non-static fields used to be allowed in many contexts, but is now limited to only a few:
		/// 		- offsetof, init, and other built-in properties are allowed:
		/// </summary>
		[Test]
		public void NonStaticVariableAccessing()
		{
			var ctxt = CreateCtxt("a", @"module a;
struct S { int field; }

struct Foo
{
    static struct Bar
    {
        static int get() { return 0; }
    }

    Bar bar;
	alias bar this;
}
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("S.field.max"); // ok, statically known
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(StaticProperty)));
			Assert.That((t as StaticProperty).Base, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("S.field"); // disallowed, no `this` reference
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			//Assert.That (t, Is.Null); // I think it's still okay if it's getting resolved as long as it's not shown in completion

			x = DParser.ParseExpression("Foo.bar.get()"); // ok, equivalent to `typeof(Foo.bar).get()'
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("Foo.get()"); // ok, equivalent to 'typeof(Foo.bar).get()'
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void Imports2()
		{
			var pcl = CreateCache(@"module A; import B;", @"module B; import C;", @"module C; public import D;", @"module D; void foo(){}",
								 @"module E; import F;", @"module F; public import C;");

			var ctxt = CreateDefCtxt(pcl, pcl.FirstPackage()["A"]);

			var t = R("foo", ctxt);
			Assert.That(t.Count, Is.EqualTo(0));

			ctxt.CurrentContext.Set(pcl.FirstPackage()["E"]);
			t = R("foo", ctxt);
			Assert.That(t.Count, Is.EqualTo(1));
		}

		[Test]
		public void ImportAliases()
		{
			var ctxt = CreateCtxt("A", @"module A;
class Class{
	static import b = B;
}", "module B;");

			var A = ctxt.MainPackage()["A"];
			var Class = A["Class"].First() as DClassLike;
			var B = ctxt.MainPackage()["B"];

			ctxt.CurrentContext.Set(Class);

			var t = RS("b", ctxt);
			Assert.That(t, Is.TypeOf(typeof(ModuleSymbol)));
			Assert.That((t as ModuleSymbol).Definition, Is.SameAs(B));
		}

		[Test]
		public void SelectiveImports()
		{
			var ctxt = CreateCtxt("B", @"module A;
int foo() {}
float* foo(int i) {}",
@"module B; import A:foo;");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("foo(123)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PointerType)));
		}

		[Test]
		public void SelectiveImports2()
		{
			var ctxt = CreateCtxt("B", @"module A;
int foo() {}
float* foo(int i) {}",
@"module B; import A:foo;",
@"module C;
void main()
{
	import A:foo;
	int i;
	i.foo;
}");

			IExpression x;
			AbstractType t;

			var C = ctxt.MainPackage()["C"];
			var main = C["main"].First() as DMethod;
			var i_foo_stmt = main.Body.SubStatements.ElementAt(2) as ExpressionStatement;
			ctxt.CurrentContext.Set(main, i_foo_stmt.Expression.Location);

			t = ExpressionTypeEvaluation.EvaluateType(i_foo_stmt.Expression, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PointerType)));
		}

		[Test]
		public void ExplicitModuleNames()
		{
			var pcl = CreateCache(@"module A; void aFoo();", @"module std.B; void bFoo();", @"module C;");

			var ctxt = CreateDefCtxt(pcl, pcl.FirstPackage()["C"]);

			DToken tk;
			var id = DParser.ParseBasicType("A.aFoo", out tk);
			var t = RS(id, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
		}

		[Test]
		public void PackageModuleImport()
		{
			var ctxt = CreateCtxt("test",
						  @"module libweb.client; void runClient() { }",
						  @"module libweb.server; void runServer() { }",
						  @"module libweb; public import libweb.client; public import libweb.server;",
						  @"module test; import libweb;");
			var ch = ctxt.MainPackage();

			ch.GetSubModule("libweb.client").FileName = Path.Combine("libweb", "client.d");
			ch.GetSubModule("libweb.server").FileName = Path.Combine("libweb", "server.d");
			ch["libweb"].FileName = Path.Combine("libweb", "package.d");
			ch["test"].FileName = Path.Combine("test.d");

			var t = RS("runServer", ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));

			t = RS("runClient", ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
		}

		[Test]
		public void TestMultiModuleResolution1()
		{
			var pcl = CreateCache(
				@"module modC;
				class C { void fooC(); }",

				@"module modB;
				import modC;
				class B:C{}",

				@"module modA;
				import modB;
			
				class A:B{	
						void bar() {
							fooC(); // Note that modC wasn't imported publically! Anyway, we're still able to access this method!
							// So, the resolver must know that there is a class C.
						}
				}");

			var A = pcl.FirstPackage()["modA"]["A"].First() as DClassLike;
			var bar = A["bar"].First() as DMethod;
			var call_fooC = bar.Body.SubStatements.First();

			Assert.That(call_fooC, Is.TypeOf(typeof(ExpressionStatement)));

			var ctxt = CreateDefCtxt(pcl, bar, call_fooC);

			var call = ((ExpressionStatement)call_fooC).Expression;
			var methodName = ((PostfixExpression_MethodCall)call).PostfixForeExpression;

			var res = ExpressionTypeEvaluation.EvaluateType(methodName, ctxt, false);

			Assert.IsTrue(res != null, "Resolve() returned no result!");
			Assert.That(res, Is.TypeOf(typeof(MemberSymbol)));

			var mr = (MemberSymbol)res;

			Assert.That(mr.Definition, Is.TypeOf(typeof(DMethod)));
			Assert.AreEqual(mr.Name, "fooC");
		}

		[Test]
		public void TestProtectedNestedType()
		{
			var pcl = CreateCache(
				@"module packA.modA;
				class C { private class B { int a; } }",

				@"module modB;
				import packA.modA;
				A ca;
				class A:C{	
					B b;
void foo(ref B bf) {
	x;
}
				}");

			var A = pcl.FirstPackage()["modB"];
			var foo = (A["A"].First() as DClassLike)["foo"].First() as DMethod;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body.SubStatements.ElementAt(0).Location);

			var x = DParser.ParseExpression("bf");

			var t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			var ms = t as DSymbol;
			Assert.That(ms.Base, Is.TypeOf(typeof(ClassType)));
			ms = ms.Base as DSymbol;
			Assert.That(ms.Name, Is.EqualTo("B"));
			Assert.That(ms.NonStaticAccess, Is.True);
		}

		[Test]
		public void SwitchLocals()
		{
			var pcl = CreateCache(@"module A;
void foo()
{
	int i=0;
	switch(i)
	{
		case 0:
			break;
		case 1:
			int col;
			col;
	}
}");

			var A = pcl.FirstPackage()["A"];
			var foo = A["foo"].First() as DMethod;
			var case1 = ((foo.Body.SubStatements.ElementAt(1) as SwitchStatement).ScopedStatement as BlockStatement).SubStatements.ElementAt(1) as SwitchStatement.CaseStatement;
			var colStmt = case1.SubStatements.ElementAt(1) as ExpressionStatement;

			var ctxt = CreateDefCtxt(pcl, foo, foo.Body.Location);

			var t = ExpressionTypeEvaluation.EvaluateType(colStmt.Expression, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void StaticForeach_UpperAggregate()
		{
			var ctxt = CreateDefCtxt(@"module modA;
static foreach(i; 97 .. 100) {
	mixin(`enum var` ~ i ~ ` = 0;`);
}
");

			for (int i = 97; i <= 100; i++)
			{
				var td = DParser.ParseBasicType("var" + (char)i);
				var t = TypeDeclarationResolver.ResolveSingle(td, ctxt);

				Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			}
		}

		[Test]
		public void StaticForeach_UpperAggregate_IteratorAsDefInitializer()
		{
			var ctxt = CreateDefCtxt(@"module modA;
static foreach(i; 97 .. 100) {
	mixin(`enum var` ~ i ~ ` = i;`);
}
");

			for (int i = 97; i <= 100; i++)
			{
				var x = DParser.ParseExpression("var" + (char)i);
				var v = Evaluation.EvaluateValue(x, ctxt);

				Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)), () => v?.ToString());
				Assert.That((v as PrimitiveValue).Value, Is.EqualTo(i));
			}
		}

		[Test]
		public void StaticForeach_ArrayAggregate()
		{
			var ctxt = CreateDefCtxt(@"module modA;
enum staticArray = ['0','1','2','3','4','5'];
static foreach(i; staticArray) {
	mixin(`enum var` ~ i ~ ` = 0;`);
}
");

			for (int i = 0; i <= 5; i++)
			{
				var td = DParser.ParseBasicType("var" + i);
				var t = TypeDeclarationResolver.ResolveSingle(td, ctxt);

				Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			}
		}

		[Test]
		public void StaticForeach_ArrayAggregate_UsingKeyIterator()
		{
			var ctxt = CreateDefCtxt(@"module modA;
enum staticArray = ['a','b'];
static foreach(index, value; staticArray) {
	mixin(`enum var` ~ value ~ ` = index;`);
}
");
			IExpression x;
			ISymbolValue v;

			x = DParser.ParseExpression("vara");
			v = Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)), () => v?.ToString());
			Assert.That((v as PrimitiveValue).Value, Is.EqualTo(0));

			x = DParser.ParseExpression("varb");
			v = Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)), () => v?.ToString());
			Assert.That((v as PrimitiveValue).Value, Is.EqualTo(1));
		}

		[Test]
		public void StaticForeach_AssocArrayAggregate()
		{
			var ctxt = CreateDefCtxt(@"module modA;
static foreach(index, value; ['a':'0','b':'1','c':'2','d':'3','e':'4','f':'5']) {
	mixin(`enum var` ~ value ~ ` = index;`);
}
");

			for (int i = 0; i <= 5; i++)
			{
				var x = DParser.ParseExpression("var" + i);
				var v = Evaluation.EvaluateValue(x, ctxt);

				Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)), () => v?.ToString());
				Assert.That((v as PrimitiveValue).Value, Is.EqualTo('a'+i));
			}
		}

		[Test]
		public void StaticForeach_AliasUsingTupleOf()
		{
			var ctxt = CreateDefCtxt(@"module modA;
struct S1
{
	int a;
	int b;
}

struct S2
{
	static foreach(alias x; S1.tupleof)
		mixin(`long ` ~ x.stringof ~ `;`);
}

S2 s2;
");

			var x = DParser.ParseExpression("s2.a");
			var t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
		}
	}
}
