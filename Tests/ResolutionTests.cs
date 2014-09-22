using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.Templates;
using D_Parser.Resolver.TypeResolution;
using NUnit.Framework;
using System.IO;
using D_Parser.Completion;

namespace Tests
{
	[TestFixture]
	public class ResolutionTests
	{
		public static DModule objMod = DParser.ParseString(@"module object;
						alias immutable(char)[] string;
						alias immutable(wchar)[] wstring;
						alias immutable(dchar)[] dstring;
						class Object { string toString(); }
						alias int size_t;
						class Exception { string msg; }");

		public static ParseCacheView CreateCache(params string[] moduleCodes)
		{
			var r = new MutableRootPackage (objMod);

			foreach (var code in moduleCodes)
				r.AddModule(DParser.ParseString(code));

			return new ParseCacheView (new [] { r });
		}

		public static ResolutionContext CreateDefCtxt(ParseCacheView pcl, IBlockNode scope, IStatement stmt = null)
		{
			CodeLocation loc = CodeLocation.Empty;

			if (stmt != null)
				loc = stmt.Location;
			else if (scope is DModule)
				loc = scope.EndLocation;
			else if (scope != null)
				loc = scope.Location;

			return CreateDefCtxt(pcl, scope, loc);
		}

		public static ResolutionContext CreateDefCtxt(ParseCacheView pcl, IBlockNode scope, CodeLocation caret)
		{
			var r = ResolutionContext.Create(pcl, new ConditionalCompilationFlags(new[]{"Windows","all"},1,true,null,0), scope, caret);
			CompletionOptions.Instance.DisableMixinAnalysis = false;
			return r;
		}

		public static ResolutionContext CreateDefCtxt(params string[] modules)
		{
			var pcl = CreateCache (modules);
			return CreateDefCtxt (pcl, pcl[0].GetModules()[0]);
		}

		public static ResolutionContext CreateCtxt(string scopedModule,params string[] modules)
		{
			var pcl = CreateCache (modules);
			return CreateDefCtxt (pcl, pcl [0] [scopedModule]);
		}

		public static ResolutionContext CreateDefCtxt(string scopedModule,out DModule mod, params string[] modules)
		{
			var pcl = CreateCache (modules);
			mod = pcl [0] [scopedModule];

			return CreateDefCtxt (pcl, mod);
		}

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

			var ctxt = CreateDefCtxt(pcl, pcl[0]["modA"]);

			var t = TypeDeclarationResolver.ResolveIdentifier("T", ctxt, null);
			Assert.That(t.Length, Is.EqualTo(1));
			Assert.That((t[0] as DSymbol).Definition.Parent, Is.SameAs(pcl[0]["modC"]));

			ctxt.CurrentContext.Set(pcl[0]["modC"],CodeLocation.Empty);
			t = TypeDeclarationResolver.ResolveIdentifier("T", ctxt, null);
			Assert.That(t.Length, Is.EqualTo(1));
			Assert.That((t[0] as DSymbol).Definition.Parent, Is.SameAs(pcl[0]["modC"]));

			ctxt.CurrentContext.Set(pcl[0]["modB"], CodeLocation.Empty);
			t = TypeDeclarationResolver.ResolveIdentifier("T", ctxt, null);
			Assert.That(t.Length, Is.EqualTo(2));

			ctxt.ResolutionErrors.Clear();
			var mod = pcl[0]["modE"];
			ctxt.CurrentContext.Set(mod, CodeLocation.Empty);
			t = TypeDeclarationResolver.ResolveIdentifier("U", ctxt, null);
			Assert.That(t.Length, Is.EqualTo(2));

			ctxt.CurrentContext.Set((mod["N"].First() as DClassLike)["foo"].First() as DMethod, CodeLocation.Empty);
			t = TypeDeclarationResolver.ResolveIdentifier("X",ctxt,null);
			Assert.That(t.Length, Is.EqualTo(1));
			Assert.That(t[0], Is.TypeOf(typeof(ClassType)));

			mod = pcl[0]["modF"];
			var f = mod["asdf"].First() as DMethod;
			ctxt.CurrentContext.Set(f, CodeLocation.Empty);
			t = TypeDeclarationResolver.ResolveIdentifier("ni", ctxt, ((f.Body.SubStatements.First() as IfStatement).ThenStatement as BlockStatement).SubStatements.ElementAt(1));
			Assert.That((t[0] as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(t.Length, Is.EqualTo(1)); // Was 2; Has been changed to 1 because it's only important to take the 'nearest' declaration that occured before the resolved expression

			t = DResolver.FilterOutByResultPriority(ctxt, t).ToArray();
			Assert.That(t.Length, Is.EqualTo(1));
		}

		[Test]
		public void BasicResolution()
		{
			var pcl = CreateCache(@"module modA;
import B;
class foo : baseFoo {
	
}",
			                      @"module B; 
private const int privConst = 1234;

class baseFoo
{
	private static int heyHo = 234;
}");

			var modA = pcl[0]["modA"];
			var ctxt = CreateDefCtxt(pcl, modA);

			var t = TypeDeclarationResolver.ResolveSingle(new IdentifierDeclaration("foo"), ctxt);
			Assert.That(t, Is.TypeOf(typeof(ClassType)));
			Assert.AreEqual("foo", (t as ClassType).Name);
			
			t = TypeDeclarationResolver.ResolveSingle("privConst", ctxt, null);
			Assert.That(t, Is.Null);
			
			ctxt.CurrentContext.Set(modA["foo"].First() as IBlockNode);
			t = TypeDeclarationResolver.ResolveSingle("heyHo", ctxt, null);
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
			var foo = pcl[0]["A"]["foo"].First() as DMethod;
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
			
			ctxt.CurrentContext.Set(pcl[0]["B"]);

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
			
			ctxt.CurrentContext.Set((pcl[0]["A"]["cl"].First() as DClassLike)["bar"].First() as DMethod);
			
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
		public void BasicResolution2()
		{
			var pcl = CreateCache(@"module A;
struct Thing(T)
{
	public T property;
}

alias Thing!(int) IntThing;");
			
			var ctxt = CreateDefCtxt(pcl, pcl[0]["A"]);
			
			var ex = DParser.ParseExpression("Thing!int");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(StructType)));
			
			ex = DParser.ParseExpression("IntThing");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(StructType)));
			
			ex = DParser.ParseExpression("new Thing!int");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt,false);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol))); // Returns the empty ctor
			var ctors = AmbiguousType.TryDissolve(t).ToArray();
			Assert.That(((DSymbol)ctors[0]).Name, Is.EqualTo(DMethod.ConstructorIdentifier));
			
			ex = DParser.ParseExpression("new IntThing");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(StructType)));
		}
		
		[Test]
		public void BasicResolution3()
		{
			var pcl = CreateCache(@"module A;
class Blupp : Blah!(Blupp) {}
class Blah(T){ T b; }");
			
			var ctxt = CreateDefCtxt(pcl, pcl[0]["A"]);
			
			var ex = DParser.ParseExpression("Blah!Blupp");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(ClassType)));
		}

		[Test]
		public void BasicResolution4()
		{
			var pcl = CreateCache(@"module modA;");
			var ctxt = CreateDefCtxt(pcl, pcl[0]["modA"]);

			var ts = TypeDeclarationResolver.ResolveSingle(new IdentifierDeclaration("string"), ctxt);
			Assert.That(ts, Is.TypeOf(typeof(ArrayType)));

			var x = DParser.ParseExpression(@"(new Object).toString()");
			var t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(ArrayType)));
		}

		[Test]
		public void ForeachIteratorType()
		{
			var ctxt = CreateCtxt("A", @"module A;
void foo() { 
foreach(c; cl) 
	c.a;
}

class Cl{ int a; }
Cl** cl;
");
			var A = ctxt.ParseCache[0]["A"];
			var foo = A["foo"].First() as DMethod;
			var c_a = ((foo.Body.First() as ForeachStatement).ScopedStatement as ExpressionStatement).Expression;
			ctxt.CurrentContext.Set(foo, c_a.Location);

			AbstractType t;

			t = ExpressionTypeEvaluation.EvaluateType(c_a, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as DerivedDataType).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void Test2_066UCSnytax()
		{
			var x = DParser.ParseExpression("creal(3)");
			var t = ExpressionTypeEvaluation.EvaluateType(x, CreateDefCtxt());

			Assert.That(t, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That((t as PrimitiveType).TypeToken, Is.EqualTo(DTokens.Creal));
		}

		[Test]
		public void TypePointerInstanceAccessing()
		{
			var ctxt = CreateCtxt("A", @"module A;
class Cl{ int a; }
Cl* cl;
");
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("cl.a");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
		}

		[Test]
		public void AnonymousClasses()
		{
			var ctxt = CreateCtxt("A",@"module A;

class BaseClass
{
	int a;
}

auto n = new class BaseClass {};
");
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression ("n.a");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void AnonymousNestedStructs()
		{
			var ctxt = CreateCtxt("A",@"module A;

class MyClass { union { string strA; struct { uint numA; uint numB; } } }
MyClass mc;
");
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression ("mc.numA");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
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

			var A = ctxt.ParseCache[0]["A"];
			var main = A["main"].First() as DMethod;
			var DDS_Header = main["DDS_Header"].First() as DClassLike;
			var pixFormat = DDS_Header["pixFormat"].First() as DVariable;

			ctxt.CurrentContext.Set(DDS_Header);

			AbstractType t;
			ITypeDeclaration td;

			t = TypeDeclarationResolver.ResolveSingle(pixFormat.Type, ctxt);
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
			var ctxt = CreateCtxt ("a",@"module a;
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

			x = DParser.ParseExpression ("S.field.max"); // ok, statically known
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf(typeof(StaticProperty)));
			Assert.That((t as StaticProperty).Base,Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression ("S.field"); // disallowed, no `this` reference
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			//Assert.That (t, Is.Null); // I think it's still okay if it's getting resolved as long as it's not shown in completion

			x = DParser.ParseExpression ("Foo.bar.get()"); // ok, equivalent to `typeof(Foo.bar).get()'
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression ("Foo.get()"); // ok, equivalent to 'typeof(Foo.bar).get()'
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void TryCatch()
		{
			var ctxt = CreateCtxt ("A", @"module A;
import exc;
void main(){
try{}
catch(MyException ex){
ex;
}", @"module exc; class MyException { int msg; }");
			var A = ctxt.ParseCache [0] ["A"];
			var main = A ["main"].First () as DMethod;
			var tryStmt = main.Body.SubStatements.ElementAt (0) as TryStatement;
			var catchStmt = tryStmt.Catches [0];

			var exStmt = (catchStmt.ScopedStatement as BlockStatement).SubStatements.ElementAt (0) as ExpressionStatement;
			ctxt.Push (main, exStmt.Location);
			var t = ExpressionTypeEvaluation.EvaluateType (exStmt.Expression, ctxt);

			Assert.That (t, Is.TypeOf (typeof(MemberSymbol)));
			t = (t as MemberSymbol).Base;
			Assert.That (t, Is.TypeOf (typeof(ClassType)));
		}

		[Test]
		public void TryCatch_ImplicitExVarType()
		{
			var ctxt = CreateCtxt ("A", @"module A;
import exc;
void main(){
try{}
catch(ex){
ex;
}");
			var A = ctxt.ParseCache [0] ["A"];
			var main = A ["main"].First () as DMethod;
			var tryStmt = main.Body.SubStatements.ElementAt (0) as TryStatement;
			var catchStmt = tryStmt.Catches [0];

			var exStmt = (catchStmt.ScopedStatement as BlockStatement).SubStatements.ElementAt (0) as ExpressionStatement;
			ctxt.Push (main, exStmt.Location);
			var t = ExpressionTypeEvaluation.EvaluateType (exStmt.Expression, ctxt);

			Assert.That (t, Is.TypeOf (typeof(MemberSymbol)));
			t = (t as MemberSymbol).Base;
			Assert.That (t, Is.TypeOf (typeof(ClassType)));
			var ct = t as ClassType;
			Assert.That (ct.Definition.Name, Is.EqualTo("Exception"));
		}

		[Test]
		public void PtrStaticProp()
		{
			var ctxt = CreateCtxt("A", @"module A; ubyte[] arr;");

			AbstractType t;
			IExpression x;

			x = DParser.ParseExpression("arr.ptr");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(StaticProperty)));
			t = (t as StaticProperty).Base;
			Assert.That(t, Is.TypeOf(typeof(PointerType)));
			Assert.That((t as PointerType).Base, Is.TypeOf(typeof(PrimitiveType)));
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
			
			var A = pcl[0]["A"];
			var foo = A["foo"].First() as DMethod;
			var case1 = ((foo.Body.SubStatements.ElementAt(1) as SwitchStatement).ScopedStatement as BlockStatement).SubStatements.ElementAt(1) as SwitchStatement.CaseStatement;
			var colStmt = case1.SubStatements.ElementAt(1) as ExpressionStatement;
			
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body.Location);
			
			var t = ExpressionTypeEvaluation.EvaluateType(colStmt.Expression, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void AccessUFCS()
		{
			var ctxt = CreateCtxt ("A", @"module A;
template to(T)
{
    T to(A...)(A args)
    {
        return toImpl!T(args);
    }
}
int a;
");
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression ("a.to!string()");
			Assert.That(x, Is.TypeOf(typeof(PostfixExpression_MethodCall)));
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.TypeOf (typeof(TemplateParameterSymbol)));
			Assert.That ((t as TemplateParameterSymbol).Base, Is.TypeOf (typeof(ArrayType)));
		}

		[Test]
		public void StaticProperties_TupleOf()
		{
			var ctxt = CreateCtxt("A", @"module A;
enum mstr = ""int* a; string b;"";

struct S
{
	int c;
	mixin(mstr);
}

S s;
");
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("s.tupleof");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(StaticProperty)));
			Assert.That((t as StaticProperty).Base, Is.TypeOf(typeof(DTuple)));
			var dtuple = (t as StaticProperty).Base as DTuple;
			Assert.That(dtuple.Items.Length, Is.EqualTo(3));
			Assert.That(dtuple.Items[0], Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(dtuple.Items[1], Is.TypeOf(typeof(PointerType)));
			Assert.That(dtuple.Items[2], Is.TypeOf(typeof(ArrayType)));
		}

		[Test]
		public void ParamArgMatching1()
		{
			var ctxt = CreateCtxt ("A", @"module A;
enum mye
{
	a,b,c
}

int foo(string s, mye en);
double* foo(string s, string ss);
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression ("foo(\"derp\",mye.a)");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));

			x = DParser.ParseExpression ("foo(\"derp\",\"yeah\")");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.TypeOf (typeof(PointerType)));

			x = DParser.ParseExpression ("foo(\"derp\",1.2)");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.Null);
		}

		[Test]
		public void ArrayTypes()
		{
			var ctxt = CreateCtxt("A", @"module A;");

			ITypeDeclaration td;
			AssocArrayType aa;
			ArrayType at;

			td = DParser.ParseBasicType("int[int]");
			aa = TypeDeclarationResolver.ResolveSingle(td, ctxt) as AssocArrayType;
			Assert.That(aa, Is.Not.TypeOf(typeof(ArrayType)));
			Assert.That(aa.KeyType, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That((aa.KeyType as PrimitiveType).TypeToken, Is.EqualTo(DTokens.Int));
			Assert.That(aa.ValueType, Is.TypeOf(typeof(PrimitiveType)));

			td = DParser.ParseBasicType("int[short]");
			aa = TypeDeclarationResolver.ResolveSingle(td, ctxt) as AssocArrayType;
			Assert.That(aa, Is.Not.TypeOf(typeof(ArrayType)));
			Assert.That(aa.KeyType, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That((aa.KeyType as PrimitiveType).TypeToken, Is.EqualTo(DTokens.Short));
			Assert.That(aa.ValueType, Is.TypeOf(typeof(PrimitiveType)));

			td = DParser.ParseBasicType("int[string]");
			aa = TypeDeclarationResolver.ResolveSingle(td, ctxt) as AssocArrayType;
			Assert.That(aa, Is.Not.TypeOf(typeof(ArrayType)));
			Assert.That(aa.KeyType, Is.TypeOf(typeof(ArrayType)));
			Assert.That((aa.KeyType as ArrayType).IsString);
			Assert.That(aa.ValueType, Is.TypeOf(typeof(PrimitiveType)));
			aa = null;

			td = DParser.ParseBasicType("byte[3]");
			at = TypeDeclarationResolver.ResolveSingle(td, ctxt) as ArrayType;
			Assert.That(at.FixedLength, Is.EqualTo(3));
			Assert.That(at.KeyType, Is.Null);
			Assert.That(at.ValueType, Is.TypeOf(typeof(PrimitiveType)));

			td = DParser.ParseBasicType("byte[6L]");
			at = TypeDeclarationResolver.ResolveSingle(td, ctxt) as ArrayType;
			Assert.That(at.FixedLength, Is.EqualTo(6));
			Assert.That(at.KeyType, Is.Null);
			Assert.That(at.ValueType, Is.TypeOf(typeof(PrimitiveType)));
		}
		
		[Test]
		public void ArrayIndexer()
		{
			var pcl = CreateCache(@"module A;
class Obj
{
	int myProp;
}

auto arr = new Obj[];
auto o = new Obj();
");
			
			var ctxt = CreateDefCtxt(pcl, pcl[0]["A"]);
			
			var ex = DParser.ParseExpression("arr[0]");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			
			Assert.That(t, Is.TypeOf(typeof(ArrayAccessSymbol)));
			
			ex = DParser.ParseExpression("arr[0].myProp");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			
			ex = DParser.ParseExpression("o.myProp");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void PackageModuleImport()
		{
			var ctxt = CreateCtxt ("test",
				          @"module libweb.client; void runClient() { }", 
				          @"module libweb.server; void runServer() { }",
				          @"module libweb; public import libweb.client; public import libweb.server;",
				          @"module test; import libweb;");
			var ch = ctxt.ParseCache [0];

			ch.GetSubModule("libweb.client").FileName = Path.Combine("libweb","client.d");
			ch.GetSubModule("libweb.server").FileName = Path.Combine("libweb","server.d");
			ch ["libweb"].FileName = Path.Combine("libweb","package.d");
			ch ["test"].FileName = Path.Combine("test.d");

			var t = TypeDeclarationResolver.ResolveSingle ("runServer", ctxt, null);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			
			t = TypeDeclarationResolver.ResolveSingle ("runClient", ctxt, null);
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

			var A = pcl[0]["modA"]["A"].First() as DClassLike;
			var bar = A["bar"].First() as DMethod;
			var call_fooC = bar.Body.SubStatements.First();

			Assert.That(call_fooC, Is.TypeOf(typeof(ExpressionStatement)));

			var ctxt = CreateDefCtxt(pcl, bar, call_fooC);

			var call = ((ExpressionStatement)call_fooC).Expression;
			var methodName = ((PostfixExpression_MethodCall)call).PostfixForeExpression;

			var res=ExpressionTypeEvaluation.EvaluateType(methodName,ctxt, false);

			Assert.IsTrue(res!=null , "Resolve() returned no result!");
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

			var A = pcl[0]["modB"];
			var foo = (A ["A"].First () as DClassLike) ["foo"].First () as DMethod;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body.SubStatements.ElementAt(0).Location);

			var x = DParser.ParseExpression ("bf");

			var t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf(typeof(MemberSymbol)));
			var ms = t as DSymbol;
			Assert.That (ms.Base, Is.TypeOf(typeof(ClassType)));
			ms = ms.Base as DSymbol;
			Assert.That (ms.Name, Is.EqualTo("B"));
			Assert.That (ms.NonStaticAccess, Is.True);
		}
		
		[Test]
		public void Imports2()
		{
			var pcl = CreateCache(@"module A; import B;", @"module B; import C;",@"module C; public import D;",@"module D; void foo(){}",
			                     @"module E; import F;", @"module F; public import C;");
			
			var ctxt = CreateDefCtxt(pcl, pcl[0]["A"]);
			
			var t = TypeDeclarationResolver.ResolveIdentifier("foo",ctxt,null);
			Assert.That(t.Length, Is.EqualTo(0));
			
			ctxt.CurrentContext.Set(pcl[0]["E"]);
			t = TypeDeclarationResolver.ResolveIdentifier("foo",ctxt,null);
			Assert.That(t.Length, Is.EqualTo(1));
		}

		[Test]
		public void ImportAliases()
		{
			var ctxt = CreateCtxt ("A", @"module A;
class Class{
	static import b = B;
}", "module B;");

			var A = ctxt.ParseCache [0] ["A"];
			var Class = A ["Class"].First () as DClassLike;
			var B = ctxt.ParseCache [0] ["B"];

			ctxt.CurrentContext.Set (Class);

			var t = TypeDeclarationResolver.ResolveSingle ("b", ctxt, null);
			Assert.That (t, Is.TypeOf (typeof(ModuleSymbol)));
			Assert.That ((t as ModuleSymbol).Definition, Is.SameAs (B));
		}

		[Test]
		public void SelectiveImports()
		{
			var ctxt = CreateCtxt("B", @"module A;
int foo() {}
float* foo(int i) {}

", @"module B; import A:foo;",@"module C;
void main()
{
	import A:foo;
	int i;
	i.foo;
}");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("foo(123)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PointerType)));

			var C = ctxt.ParseCache[0]["C"];
			var main = C["main"].First() as DMethod;
			var i_foo_stmt = main.Body.SubStatements.ElementAt(2) as ExpressionStatement;
			ctxt.CurrentContext.Set(main, i_foo_stmt.Expression.Location);

			t = ExpressionTypeEvaluation.EvaluateType(i_foo_stmt.Expression, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PointerType)));
		}
		
		[Test]
		public void ExplicitModuleNames()
		{
			var pcl = CreateCache(@"module A; void aFoo();", @"module std.B; void bFoo();",@"module C;");
			
			var ctxt = CreateDefCtxt(pcl, pcl[0]["C"]);
			
			DToken tk;
			var id = DParser.ParseBasicType("A.aFoo", out tk);
			var t = TypeDeclarationResolver.ResolveSingle(id, ctxt);
			
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
		}

		[Test]
		public void TestParamDeduction1()
		{
			var pcl=CreateCache(@"module modA;

//void foo(T:MyClass!E,E)(T t) {}
int foo(Y,T)(Y y, T t) {}
//string[] foo(T)(T t, T u) {}

class A {
	const void aBar(this T)() {}
}
class B:A{}
class C:B{}

class MyClass(T) { T tvar; }
class MyClass(T:A) {}
class MyClass(T:B) {}

class D(int u) {}
class D(int u:1) {}

const int a=3;
int b=4;
");

			var ctxt = CreateDefCtxt(pcl, pcl[0]["modA"]);

			var instanceExpr = DParser.ParseExpression("(new MyClass!int).tvar");

			Assert.IsInstanceOfType(typeof(PostfixExpression_Access),instanceExpr);

			var res = ExpressionTypeEvaluation.EvaluateType(instanceExpr, ctxt);

			Assert.IsInstanceOfType(typeof(MemberSymbol),res);
			var mr = (MemberSymbol)res;

			Assert.IsInstanceOfType( typeof(TemplateParameterSymbol),mr.Base);
			var tps = (TemplateParameterSymbol)mr.Base;
			Assert.IsInstanceOfType( typeof(PrimitiveType),tps.Base);
			var sr = (PrimitiveType)tps.Base;

			Assert.AreEqual(sr.TypeToken, DTokens.Int);
		}

		[Test]
		public void TestParamDeduction2()
		{
			var pcl = CreateCache(@"
module modA;
T foo(T)() {}
");

			var ctxt = CreateDefCtxt(pcl, pcl[0]["modA"]);

			var call = DParser.ParseExpression("foo!int()");
			var bt = ExpressionTypeEvaluation.EvaluateType(call, ctxt);
			
			Assert.IsInstanceOfType(typeof(TemplateParameterSymbol),bt);
			var tps = (TemplateParameterSymbol)bt;
			Assert.IsInstanceOfType(typeof(PrimitiveType),tps.Base, "Resolution returned empty result instead of 'int'");
			var st = (PrimitiveType)tps.Base;
			Assert.IsNotNull(st, "Result must be Static type int");
			Assert.AreEqual(st.TypeToken, DTokens.Int, "Static type must be int");
		}

		[Test]
		public void TestParamDeduction3()
		{
			var pcl = CreateCache(@"module modA;

class A {}
class A2 {}

class B(T){
	class C(T2) : T {} 
}");

			var ctxt = CreateDefCtxt(pcl, pcl[0]["modA"]);

			var inst = DParser.ParseExpression("(new B!A).new C!A2"); // TODO
		}

		[Test]
		public void TestOverloads1()
		{
			var pcl = CreateCache(@"module modA;

int foo(int i) {}

class A
{
	void foo(int k) {}

	void bar()
	{
		
	}
}

");
			var A = pcl[0]["modA"]["A"].First() as DClassLike;
			var bar = A["bar"].First() as DMethod;
			var ctxt = CreateDefCtxt(pcl, bar, bar.Body);

			var e = DParser.ParseExpression("123.foo");

			var t = ExpressionTypeEvaluation.EvaluateType(e, ctxt, false);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.AreEqual(pcl[0]["modA"]["foo"].First(), ((MemberSymbol)t).Definition);
		}

		/// <summary>
		/// Templated and non-template functions can now be overloaded against each other:
		/// </summary>
		[Test]
		public void TestOverloads2()
		{
			var ctxt = CreateCtxt ("A", @"module A;
int* foo(T)(T t) { }
int foo(int n) { }
long longVar = 10L;");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression ("foo(100)");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression ("foo(\"asdf\")");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf(typeof(PointerType)));

			// Integer literal 10L can be converted to int without loss of precisions.
			// Then the call matches to foo(int n).
			x = DParser.ParseExpression ("foo(10L)");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf(typeof(PrimitiveType)));

			// A runtime variable 'num' typed long is not implicitly convertible to int.
			// Then the call matches to foo(T)(T t).
			x = DParser.ParseExpression ("foo(longVar)");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf(typeof(PointerType)));
		}

		/// <summary>
		/// Array slices are now r-values
		/// </summary>
		[Test]
		public void ArraySlicesNoRValues()
		{
			var ctxt = CreateCtxt("modA", @"module modA;

int takeRef(ref int[] arr) { }
int take(int[] arr) { }
int takeAutoRef(T)(auto ref T[] arr) { }

int[] arr = [1, 2, 3, 4];
int[] arr2 = arr[1 .. 2];");

			IExpression x;
			AbstractType t;

			// error, cannot pass r-value by reference
			x = DParser.ParseExpression("takeRef(arr[1 .. 2])");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			//Assert.That(t, Is.Null);

			// ok
			x = DParser.ParseExpression ("take(arr)");
			t = ExpressionTypeEvaluation.EvaluateType (x,ctxt);
			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));

			x = DParser.ParseExpression ("takeRef(arr)");
			t = ExpressionTypeEvaluation.EvaluateType (x,ctxt);
			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));

			x = DParser.ParseExpression ("takeAutoRef(arr)");
			t = ExpressionTypeEvaluation.EvaluateType (x,ctxt);
			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));

			// ok, arr2 is a variable
			x = DParser.ParseExpression ("take(arr2)");
			t = ExpressionTypeEvaluation.EvaluateType (x,ctxt);
			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));

			x = DParser.ParseExpression ("takeRef(arr2)");
			t = ExpressionTypeEvaluation.EvaluateType (x,ctxt);
			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));

			x = DParser.ParseExpression ("takeAutoRef(arr2)");
			t = ExpressionTypeEvaluation.EvaluateType (x,ctxt);
			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));


			x = DParser.ParseExpression ("take(arr[1 .. 2])");
			t = ExpressionTypeEvaluation.EvaluateType (x,ctxt);
			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));

			

			x = DParser.ParseExpression ("takeAutoRef(arr[1 .. 2])");
			t = ExpressionTypeEvaluation.EvaluateType (x,ctxt);
			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));
		}

		[Test]
		public void TestParamDeduction4()
		{
			var ctxt = CreateCtxt("modA",@"module modA;

void fo(T:U[], U)(T o) {}
void f(T:U[n], U,int n)(T o) {}

char[5] arr;

double foo(T)(T a) {}

int delegate(int b) myDeleg;

");
			ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			var x = DParser.ParseExpression("f!(char[5])");
			var r=ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			var mr = r as MemberSymbol;
			Assert.That(r, Is.TypeOf(typeof(MemberSymbol)));

			var v = mr.DeducedTypes[2].ParameterValue;
			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			Assert.AreEqual(5M, ((PrimitiveValue)v).Value);

			x = DParser.ParseExpression("fo!(char[5])");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.That(r, Is.TypeOf(typeof(MemberSymbol)));

			x = DParser.ParseExpression("fo!(immutable(char)[])");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.That(r, Is.TypeOf(typeof(MemberSymbol)));

			x = DParser.ParseExpression("myDeleg");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsNotNull(mr);
			Assert.IsInstanceOfType(typeof(DelegateType), mr.Base);

			x=DParser.ParseExpression("myDeleg(123)");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That (r, Is.TypeOf(typeof(DelegateCallSymbol)));
			Assert.That((r as DerivedDataType).Base, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("foo(myDeleg(123))");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsNotNull(mr);
			Assert.That(mr.Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void TestParamDeduction5()
		{
			var pcl = CreateCache(@"module modA;
struct Params{}
class IConn ( P ){}
class Conn : IConn!(Params){}
class IRegistry ( P ){}
class Registry (C : IConn!(Params) ) : IRegistry!(Params){}
class ConcreteRegistry : Registry!(Conn){}
class IClient ( P, R : IRegistry!(P) ){}
class Client : IClient!(Params, ConcreteRegistry){}");

			var mod=pcl[0]["modA"];
			var Client = mod["Client"].First() as DClassLike;
			var ctxt = CreateDefCtxt(pcl, mod);

			var res = TypeDeclarationResolver.HandleNodeMatch(Client, ctxt);
			Assert.IsInstanceOfType(typeof(ClassType),res);
			var ct = (ClassType)res;

			Assert.IsInstanceOfType( typeof(ClassType),ct.Base);
			ct = (ClassType)ct.Base;

			Assert.AreEqual(ct.DeducedTypes.Count, 2);
			var dedtype = ct.DeducedTypes[0];
			Assert.AreEqual("P", dedtype.Name);
			Assert.AreEqual(mod["Params"].First(),((DSymbol)dedtype.Base).Definition);
			dedtype = ct.DeducedTypes[1];
			Assert.AreEqual("R", dedtype.Name);
			Assert.AreEqual(mod["ConcreteRegistry"].First(), ((DSymbol)dedtype.Base).Definition);


			ctxt.CurrentContext.Set(mod);
			DToken opt=null;
			var tix = DParser.ParseBasicType("IClient!(Params,ConcreteRegistry)",out opt);
			res = TypeDeclarationResolver.ResolveSingle(tix, ctxt);

			Assert.IsInstanceOfType(typeof(ClassType),res);
		}

		[Test]
		public void TestParamDeduction6()
		{
			var pcl = CreateCache(@"module modA;
class A(T) {}
class B : A!int{}
class C(U: A!int){}
class D : C!B {}");

			var mod = pcl[0]["modA"];
			var ctxt = CreateDefCtxt(pcl, mod);

			var res = TypeDeclarationResolver.HandleNodeMatch(mod["D"].First(), ctxt);
			Assert.IsInstanceOfType(typeof(ClassType),res);
			var ct = (ClassType)res;

			Assert.IsInstanceOfType(typeof(ClassType),ct.Base);
			ct = (ClassType)ct.Base;

			Assert.AreEqual(1, ct.DeducedTypes.Count);
		}
		
		[Test]
		public void TestParamDeduction7()
		{
			var pcl = CreateCache(@"module A;
U genA(U)();
T delegate(T dgParam) genDelegate(T)();");
			
			var A = pcl[0]["A"];
			var ctxt = CreateDefCtxt(pcl, A);
			
			var ex = DParser.ParseExpression("genDelegate!int()");
			var t = ExpressionTypeEvaluation.EvaluateType(ex,ctxt);
			Assert.That(t, Is.TypeOf(typeof(DelegateType)));
			var dt = (DelegateType)t;
			Assert.That(dt.Base, Is.Not.Null);
			Assert.That(dt.Parameters, Is.Not.Null);
			
			ex = DParser.ParseExpression("genA!int()");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
		}
		
		[Test]
		public void TestParamDeduction8()
		{
			var pcl = CreateCache(@"module A;
double[] darr;
struct Appender(A:E[],E) { A data; }

Appender!(E[]) appender(A : E[], E)(A array = null)
{
    return Appender!(E[])(array);
}");
			
			var A = pcl[0]["A"];
			var ctxt = CreateDefCtxt(pcl, A);
			
			var ex = DParser.ParseExpression("new Appender!(double[])(darr)");
			var t = ExpressionTypeEvaluation.EvaluateType(ex,ctxt, false);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol))); // ctor
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(StructType)));
			
			ex = DParser.ParseExpression("appender!(double[])()");
			t = ExpressionTypeEvaluation.EvaluateType(ex,ctxt);
			Assert.That(t, Is.TypeOf(typeof(StructType)));
			var ss = t as StructType;
			Assert.That(ss.DeducedTypes.Count, Is.GreaterThan(0));
		}
		
		[Test]
		public void TestParamDeduction9()
		{
			var ctxt = CreateDefCtxt(@"module A;
const int k = 4;
template mxTemp(int i)
{
	static if(i < 0)
		enum mxTemp = ""int"";
	else
		enum mxTemp = ""bool"";
}

template def(int i,string name)
{
	enum def = mxTemp!(-i) ~ "" ""~name~"";"";
}

template def2(int i)
{
	enum def2 = i;
}

mixin(def!(-1,""bar""));
");
			IExpression ex;
			ISymbolValue val;

			ex = DParser.ParseExpression(@"def!(-2,""someVar"")");
			val = Evaluation.EvaluateValue(ex, ctxt);
			Assert.That(val, Is.TypeOf(typeof(ArrayValue)));
			Assert.That((val as ArrayValue).IsString,Is.True);
			Assert.That((val as ArrayValue).StringValue, Is.EqualTo("bool someVar;"));

			var def = ctxt.ParseCache [0] ["A"]["def"].First () as DClassLike;
			var defS = new TemplateType (def, null, new[]{ 
				new TemplateParameterSymbol(def.TemplateParameters[0], new PrimitiveValue(DTokens.Int, 2, null)), 
				new TemplateParameterSymbol(def.TemplateParameters[1], new ArrayValue(Evaluation.GetStringType(ctxt), "someVar")) 
			});
			using (ctxt.Push(defS))
			{
				ex = DParser.ParseExpression("mxTemp!(-i) ~ \" \"~name~\";\"");
				val = Evaluation.EvaluateValue(ex, ctxt);
			}

			Assert.That(val, Is.TypeOf(typeof(ArrayValue)));
			Assert.That((val as ArrayValue).IsString,Is.True);
			Assert.That((val as ArrayValue).StringValue, Is.EqualTo("int someVar;"));

			ex = DParser.ParseExpression ("def2!5");
			val = Evaluation.EvaluateValue (ex, ctxt);
			Assert.That (val, Is.TypeOf (typeof(PrimitiveValue)));
			Assert.That ((val as PrimitiveValue).Value, Is.EqualTo(5m));

			ex = DParser.ParseExpression ("-k");
			val = Evaluation.EvaluateValue (ex, ctxt);
			Assert.That (val, Is.TypeOf (typeof(PrimitiveValue)));
			Assert.That ((val as PrimitiveValue).Value, Is.EqualTo(-4m));

			ex = DParser.ParseExpression ("mxTemp!(-k)");
			val = Evaluation.EvaluateValue (ex, ctxt);
			Assert.That(val, Is.TypeOf(typeof(ArrayValue)));
			Assert.That((val as ArrayValue).IsString,Is.True);
			Assert.That((val as ArrayValue).StringValue, Is.EqualTo("int"));



			
			ex = DParser.ParseExpression(@"def!(-5,""foolish"")");
			val = Evaluation.EvaluateValue(ex, ctxt);
			Assert.That(val, Is.TypeOf(typeof(ArrayValue)));
			Assert.That((val as ArrayValue).IsString,Is.True);
			Assert.That((val as ArrayValue).StringValue, Is.EqualTo("bool foolish;"));
			
			ex=DParser.ParseExpression("bar");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base,Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(((t as MemberSymbol).Base as PrimitiveType).TypeToken,Is.EqualTo(DTokens.Bool));
		}

		[Test]
		public void TestParamDeduction10()
		{
			var ctxt = CreateCtxt("A",@"module A;

void foo(T)(int a) {}
void foo2(T=double)(bool b) {}
V foo3(V)(V v) {}");

			ctxt.ContextIndependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			IExpression x;
			AbstractType t;
			MemberSymbol ms;

			x = DParser.ParseExpression("foo3(\"asdf\")");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			ms = t as MemberSymbol;
			var tps = ms.Base as TemplateParameterSymbol;
			Assert.That(tps, Is.Not.Null);
			Assert.That(tps.Base, Is.TypeOf(typeof(ArrayType)));

			x = DParser.ParseExpression("foo(123)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.Null);

			x = DParser.ParseExpression("foo2(true)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			ms = t as MemberSymbol;
			Assert.That(ms.DeducedTypes, Is.Not.Null);
			Assert.That(ms.DeducedTypes[0].Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void TemplateParamDeduction11()
		{
			var pcl = CreateCache(@"module modA;
Appender!(E[]) appender(A : E[], E)(A array = null) { return Appender!(E[])(array); }
struct Appender(A : T[], T) {
	this(T[] arr){}
}
");
			var ctxt = CreateDefCtxt(pcl, pcl[0]["modA"]);

			var ex = DParser.ParseExpression("appender!(int[])()");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.InstanceOfType(typeof(StructType)));

			ex = DParser.ParseExpression("appender!string()");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.InstanceOfType(typeof(StructType)));
		}

		[Test]
		public void TemplateParamDeduction12()
		{
			var pcl = CreateCache(@"module modA;
template Tmpl(T)
{
	enum Tmpl = false;
}

template Tmpl(alias T)
{
	enum Tmpl = true;
}

template tt(alias U)
{
}

int sym;
");
			var modA = pcl[0]["modA"];
			var ctxt = CreateDefCtxt(pcl, modA);

			var ex = DParser.ParseExpression("Tmpl!sym");
			var t = Evaluation.EvaluateValue(ex, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(PrimitiveValue)));
			Assert.That((t as PrimitiveValue).Value == 1m);

			ex = ex = DParser.ParseExpression("Tmpl!int");
			t = Evaluation.EvaluateValue(ex, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(PrimitiveValue)));
			Assert.That((t as PrimitiveValue).Value == 0m);

			ctxt.CurrentContext.Set(modA["tt"].First() as IBlockNode);
			ex = DParser.ParseExpression("Tmpl!U");
			t = Evaluation.EvaluateValue(ex, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(PrimitiveValue)));
			Assert.That((t as PrimitiveValue).Value == 1m);
		}

		[Test]
		public void TemplateParamDeduction13()
		{
			var ctxt = CreateCtxt("A", @"module A;
class A(S:string) {}
class A(T){}
class C(U: A!W, W){ W item; }
");

			ITypeDeclaration td;
			AbstractType t;

			td = DParser.ParseBasicType("C!(A!int)");
			t = TypeDeclarationResolver.ResolveSingle(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(ClassType)));
			var ct = t as ClassType;
			Assert.That(ct.DeducedTypes.Count, Is.EqualTo(2));
			Assert.That(ct.DeducedTypes[0].Name, Is.EqualTo("U"));
			Assert.That(ct.DeducedTypes[1].Name, Is.EqualTo("W"));
			Assert.That(ct.DeducedTypes[1].Base, Is.TypeOf(typeof(PrimitiveType)));

			td = DParser.ParseBasicType("C!(A!string)");
			t = TypeDeclarationResolver.ResolveSingle(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(ClassType)));
			ct = t as ClassType;
			Assert.That(ct.DeducedTypes.Count, Is.EqualTo(2));
		}

		[Test]
		public void DefaultTemplateParamType()
		{
			var ctxt = CreateCtxt("A", @"module A;
struct StringNumPair(T = string, U = long){
    T m_str;
    U m_num;

    @property size_t len(){
        return cast(size_t) (m_str.length + m_num.length);
    }
}
");
			var A = ctxt.ParseCache[0]["A"];
			var StringNumPair = A["StringNumPair"].First() as DClassLike;
			var len = StringNumPair["len"].First() as DMethod;
			ctxt.CurrentContext.Set(len,len.Body.First().Location);

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("T");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(ArrayType)));
		}

		[Test]
		public void TemplateArgAsBasetype()
		{
			var ctxt = CreateCtxt("A",@"module A;
class A(T) { T t; }
class B(Z) : A!Z {}

B!int b;");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("b.t");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			t = (t as DerivedDataType).Base;
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			t = (t as DerivedDataType).Base;
			Assert.That(t, Is.TypeOf(typeof(PrimitiveType)));
			
		}

		[Test]
		public void TemplateTypeTuple1()
		{
			var pcl = CreateCache(@"module modA;
template Print(A ...) { 
	void print() { 
		writefln(""args are "", A); 
	} 
} 

template Write(A ...) {
	void write(A a) { // A is a TypeTuple, a is an ExpressionTuple 
		writefln(""args are "", a); 
	} 
} 

void tplWrite(W...)(W a) { writefln(""args are "", a); } 
void tplWrite2(W...)(W a,double d) { } 

void main() { 
	Print!(1,'a',6.8).print(); // prints: args are 1a6.8 
	Write!(int, char, double).write(1, 'a', 6.8); // prints: args are 1a6.8
}");

			var modA = pcl[0]["modA"];
			var ctxt = CreateDefCtxt(pcl, modA);

			var x = DParser.ParseExpression("Print!(1,'a',6.8)");
			var t = ExpressionTypeEvaluation.EvaluateType(x,ctxt);
			Assert.That(t, Is.TypeOf(typeof(TemplateType)));
			var tps = (t as TemplateType).DeducedTypes[0] as TemplateParameterSymbol;
			Assert.That(tps, Is.Not.Null);
			Assert.That(tps.Base, Is.TypeOf(typeof(DTuple)));
			var tt = tps.Base as DTuple;
			Assert.That(tt.Items.Length, Is.EqualTo(3));
			Assert.That(tt.IsExpressionTuple);

			ctxt.ContextIndependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			x = DParser.ParseExpression("Write!(int, char, double).write(1, 'a', 6.8)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));

			x = DParser.ParseExpression("tplWrite!(int, char, double)(1, 'a', 6.8)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));

			x = DParser.ParseExpression("tplWrite(1, 'a', 6.8)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			tps = (t as MemberSymbol).DeducedTypes[0] as TemplateParameterSymbol;
			Assert.That(tps, Is.Not.Null);
			Assert.That(tps.Base, Is.TypeOf(typeof(DTuple)));
			tt = tps.Base as DTuple;
			Assert.That(tt.Items.Length, Is.EqualTo(3));
			Assert.That(tt.IsTypeTuple);

			x = DParser.ParseExpression("tplWrite2(\"asdf\", 'a', 6.8)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			tps = (t as MemberSymbol).DeducedTypes[0] as TemplateParameterSymbol;
			Assert.That(tps, Is.Not.Null);
			Assert.That(tps.Base, Is.TypeOf(typeof(DTuple)));
			tt = tps.Base as DTuple;
			Assert.That(tt.Items.Length, Is.EqualTo(2));
			Assert.That(tt.Items[0], Is.TypeOf(typeof(ArrayType)));
		}
		
		[Test]
		public void IdentifierOnlyTemplateDeduction()
		{
			var pcl = CreateCache(@"module A;
class Too(T:int)
{ int foo1;}
class Too(T:float)
{ int foo2;}");
			
			var ctxt = CreateDefCtxt(pcl, pcl[0]["A"]);
			
			var ex = DParser.ParseExpression("Too");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.Null);
			
			ex = DParser.ParseExpression("Too!int");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(ClassType)));
			
			DToken tk;
			var ty = DParser.ParseBasicType("Too",out tk);
			t = TypeDeclarationResolver.ResolveSingle(ty,ctxt);
			Assert.That(t, Is.Null);
			
			ty = DParser.ParseBasicType("Too!int",out tk);
			t = TypeDeclarationResolver.ResolveSingle(ty,ctxt);
			Assert.That(t, Is.TypeOf(typeof(ClassType)));
		}
		
		[Test]
		public void TemplateParameterPrototypeRecognition()
		{
			var pcl = CreateCache(@"module A;
static int tmplFoo(T)() {}
static int[] tmplFoo2(T : U[], U)(int oo) {}
static int* tmplBar(T)(T t) {}

void foo(U)(U u)
{
	tmplFoo!U;
	tmplFoo2!U;
	tmplBar!U(u);
	tmplFoo2!(int[])(123);
	tmplFoo2!U(123);
}");
			
			var foo = pcl[0]["A"]["foo"].First() as DMethod;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body);
			var subSt = foo.Body.SubStatements as List<IStatement>;

			var ex = (subSt[0] as ExpressionStatement).Expression;
			var t = ExpressionTypeEvaluation.GetOverloads(ex  as TemplateInstanceExpression, ctxt, null, true);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Length, Is.EqualTo(1));
			
			var t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t_, Is.TypeOf(typeof(PrimitiveType)));
			
			ex = (subSt[1] as ExpressionStatement).Expression;
			t = ExpressionTypeEvaluation.GetOverloads(ex  as TemplateInstanceExpression, ctxt, null, true);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Length, Is.EqualTo(1));
			
			t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt, false);
			Assert.That(t_, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t_ as MemberSymbol).Base, Is.TypeOf(typeof(ArrayType)));
			
			ex = (subSt[2] as ExpressionStatement).Expression;
			t = ExpressionTypeEvaluation.GetOverloads((ex as PostfixExpression_MethodCall).PostfixForeExpression as TemplateInstanceExpression, ctxt, null, true);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Length, Is.EqualTo(1));
			Assert.That(t[0], Is.TypeOf(typeof(MemberSymbol)));
			
			t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t_, Is.TypeOf(typeof(PointerType)));
			
			ex = (subSt[3] as ExpressionStatement).Expression;
			t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t_, Is.TypeOf(typeof(ArrayType)));

			ex = (subSt[4] as ExpressionStatement).Expression;
			t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t_, Is.TypeOf(typeof(ArrayType)));
		}
		
		[Test]
		public void EmptyTypeTuple()
		{
			var pcl = CreateCache(@"module A;
enum E {A,B}

int writeln(T...)(T t)
{
}");
			var ctxt = CreateDefCtxt(pcl, pcl[0]["A"]);

			IExpression ex;
			AbstractType x;

			ex = DParser.ParseExpression("\"asdf\".writeln()");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.TypeOf(typeof(PrimitiveType)));

			ex = DParser.ParseExpression("writeln()");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.TypeOf(typeof(PrimitiveType)));

			ex = DParser.ParseExpression("writeln(E.A)");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.TypeOf(typeof(PrimitiveType)));


		}
		
		[Test]
		public void TypeTupleAsArgument()
		{
			var pcl = CreateCache(@"module A;
template bar(T...) {
    static if(T.length == 1) {
        enum bar = ['a','g','h'];
    } else {
        enum bar = 0u;
    }
}

auto foo() {
    
}");
			var foo = pcl[0]["A"]["foo"].First() as DMethod;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body);
			
			var ex = DParser.ParseExpression("bar!int");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			var ms = t as MemberSymbol;
			Assert.That(ms.Base, Is.TypeOf(typeof(ArrayType)));
			
			ex = DParser.ParseExpression("bar");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			ms = t as MemberSymbol;
			Assert.That(ms.Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That((ms.Base as PrimitiveType).TypeToken, Is.EqualTo(DTokens.Uint));
		}

		[Test]
		public void Ctors()
		{
			var pcl = CreateCache(@"module modA;

class A {}
class B : A{
	this() {
		super();
	}
}");

			var B = pcl[0]["modA"]["B"].First() as DClassLike;
			var this_ = (DMethod)B[DMethod.ConstructorIdentifier].First();
			var ctxt = CreateDefCtxt(pcl, this_);

			var super = (this_.Body.SubStatements.First() as IExpressionContainingStatement).SubExpressions[0] as PostfixExpression_MethodCall;

			var t = ExpressionTypeEvaluation.EvaluateType(super.PostfixForeExpression, ctxt);
			Assert.That(t, Is.TypeOf(typeof(ClassType)));
			
			t = ExpressionTypeEvaluation.EvaluateType(super, ctxt);
			Assert.IsNull(t);
		}

		/// <summary>
		/// Constructor qualifiers are taken into account when constructing objects
		/// </summary>
		[Test]
		public void QualifiedConstructors()
		{
			var ctxt = CreateCtxt("modA", @"module modA;
class C
{
    this() immutable { }
    this() shared    { }
	this()           { }
    this() const     { }
}

class D
{
    this() immutable { }
	this() const { }
}

class P
{
    this() pure { }
}
");

			IExpression x;
			MemberSymbol ctor;
			ClassType ct;

			x = DParser.ParseExpression ("new C");
			ctor = ExpressionTypeEvaluation.EvaluateType (x, ctxt, false) as MemberSymbol;

			Assert.That (ctor, Is.Not.Null);
			Assert.That(ctor.Base, Is.TypeOf(typeof(ClassType)));
			ct = ctor.Base as ClassType;
			Assert.That (ct.Modifier, Is.EqualTo(0));

			x = DParser.ParseExpression ("new const C");
			ctor = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false) as MemberSymbol;

			Assert.That (ctor, Is.Not.Null);
			Assert.That(ctor.Base, Is.TypeOf(typeof(ClassType)));
			ct = ctor.Base as ClassType;
			Assert.That (ct.Modifier, Is.EqualTo(DTokens.Const));

			x = DParser.ParseExpression ("new immutable C");
			ctor = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false) as MemberSymbol;

			Assert.That (ctor, Is.Not.Null);
			Assert.That(ctor.Base, Is.TypeOf(typeof(ClassType)));
			ct = ctor.Base as ClassType;
			Assert.That (ct.Modifier, Is.EqualTo(DTokens.Immutable));

			x = DParser.ParseExpression ("new shared C");
			ctor = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false) as MemberSymbol;

			Assert.That (ctor, Is.Not.Null);
			Assert.That(ctor.Base, Is.TypeOf(typeof(ClassType)));
			ct = ctor.Base as ClassType;
			Assert.That (ct.Modifier, Is.EqualTo(DTokens.Shared));



			x = DParser.ParseExpression ("new P");
			ctor = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false) as MemberSymbol;

			Assert.That (ctor, Is.Not.Null);
			Assert.That(ctor.Base, Is.TypeOf(typeof(ClassType)));
			ct = ctor.Base as ClassType;
			Assert.That (ct.Modifier, Is.EqualTo(0));

			x = DParser.ParseExpression ("new const P");
			ctor = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false) as MemberSymbol;

			Assert.That (ctor, Is.Not.Null);
			Assert.That(ctor.Base, Is.TypeOf(typeof(ClassType)));
			ct = ctor.Base as ClassType;
			Assert.That (ct.Modifier, Is.EqualTo(DTokens.Const));

			x = DParser.ParseExpression ("new immutable P");
			ctor = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false) as MemberSymbol;

			Assert.That (ctor, Is.Not.Null);
			Assert.That(ctor.Base, Is.TypeOf(typeof(ClassType)));
			ct = ctor.Base as ClassType;
			Assert.That (ct.Modifier, Is.EqualTo(DTokens.Immutable));



			x = DParser.ParseExpression ("new const D");
			ctor = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false) as MemberSymbol;

			Assert.That (ctor, Is.Not.Null);
			Assert.That(ctor.Base, Is.TypeOf(typeof(ClassType)));
			ct = ctor.Base as ClassType;
			Assert.That (ct.Modifier, Is.EqualTo(DTokens.Const));

			x = DParser.ParseExpression ("new D");
			ctor = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false) as MemberSymbol;

			Assert.That (ctor, Is.Null);
		}

		/// <summary>
		/// Implicit Function Template Instantiation now supports enclosing type/scope deduction.
		/// </summary>
		[Test]
		public void ImprovedIFTI()
		{
			var ctxt = CreateCtxt("modA",@"module modA;
struct A{    struct Foo { } }
struct B{    struct Foo { } }

int call(T)(T t, T.Foo foo) { }

auto a = A();
auto a_f = A.Foo();

auto b = B();
auto b_f = B.Foo();
");
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression ("call(a, a_f)");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));

			x = DParser.ParseExpression ("call(b, b_f)");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));

			x = DParser.ParseExpression ("call(a, b_f)");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.Null);

			x = DParser.ParseExpression ("call(b, a_f)");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.Null);
		}

		[Test]
		public void TemplateAliasing()
		{
			var pcl = CreateCache(@"module m;
template Foo(A)
{
	A Foo;
}

template Bar(B)
{
	version(X)
		B[] Bar;
	else
		B* Bar;
}

template Baz(B)
{
	debug
		B* Baz;
	else
		B[] Baz;
}");

			var ctxt = CreateDefCtxt(pcl, pcl[0]["m"]);

			DToken tk;
			var td = DParser.ParseBasicType("Foo!int",out tk);

			var s_ = TypeDeclarationResolver.Resolve(td, ctxt);
			Assert.AreEqual(1,s_.Length);
			var s = s_[0];
			
			Assert.IsInstanceOfType(typeof(MemberSymbol),s);

			var ms = (MemberSymbol)s;
			Assert.IsInstanceOfType(typeof(DVariable),ms.Definition);
			Assert.IsInstanceOfType(typeof(TemplateParameterSymbol),ms.Base);
			var tps = (TemplateParameterSymbol)ms.Base;
			Assert.IsInstanceOfType(typeof(PrimitiveType),tps.Base);

			var pt = (PrimitiveType)tps.Base;
			Assert.AreEqual(DTokens.Int, pt.TypeToken);
			
			s_ = TypeDeclarationResolver.Resolve(DParser.ParseBasicType("Bar!int",out tk),ctxt);
			Assert.That(s_.Length, Is.EqualTo(1));
			s = s_[0];
			
			Assert.That(((DSymbol)s).Base, Is.TypeOf(typeof(PointerType)));
			
			s_ = TypeDeclarationResolver.Resolve(DParser.ParseBasicType("Baz!int",out tk),ctxt);
			Assert.That(s_.Length, Is.EqualTo(1));
			s = s_[0];
			
			Assert.That(((DSymbol)s).Base, Is.TypeOf(typeof(PointerType)));
		}

		[Test]
		public void SustainingDeducedTypesInImplicitTemplProps()
		{
			var ctxt = CreateCtxt("A",@"module A;
template baz(string s) { enum baz = ""int ""~s~"";""; }
");

			var x = DParser.ParseExpression("baz!\"w\"");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(ArrayValue)));
			var av = v as ArrayValue;
			Assert.That(av.IsString);
			Assert.That(av.StringValue, Is.EqualTo("int w;"));
		}

		[Test]
		public void CrossModuleTemplateDecl()
		{
			var ctxt = CreateCtxt ("c",@"
module a;
template Traits(T) if (is(T == string)){    enum Traits = ""abc"";}
auto func(T, A...)(A args) if (is(T == string)){    return ""abc"";}
", @"
module b;
template Traits(T) if (is(T == double)){ enum Traits = true;}
auto func(T, A...)(A args) if (is(T == double)){    return 2;}
", @"
module c;
import a, b;
");
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression ("Traits!string");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf(typeof(MemberSymbol)));
			t = (t as MemberSymbol).Base;
			Assert.That (t, Is.TypeOf (typeof(ArrayType)));

			x = DParser.ParseExpression ("Traits!double");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf(typeof(MemberSymbol)));
			t = (t as MemberSymbol).Base;
			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));

			x = DParser.ParseExpression ("Traits!int");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.Null);

			x = DParser.ParseExpression ("func!string(1)");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.TypeOf (typeof(ArrayType)));

			x = DParser.ParseExpression ("func!double(1)");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));

			x = DParser.ParseExpression ("func!int(1)");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.Null);

		}

		#region Operator overloading
		[Test]
		public void opDispatch()
		{
			var ctxt = CreateCtxt ("A", @"module A;
class S {
	int* opDispatch(string s)(int i){ }
	int opDispatch(string s)(){ }
}

struct S2 {
  T opDispatch(string s, T)(T i){ return i; }
}

struct S3 {
  static int opDispatch(string s)(){ }
}

struct D {
  template opDispatch(string s) {
    enum int opDispatch = 8;
  }
}

S s;
S2 s2;
S3 s3;

void main() {
  S2 loc;
	x;
}");
			AbstractType t;
			DSymbol ds;
			IExpression x;
			ITypeDeclaration td;
			ISymbolValue v;


			var main = ctxt.ParseCache [0] ["A"] ["main"].First () as DMethod;
			var stmt_x = main.Body.SubStatements.ElementAt (1);

			x = new PostfixExpression_MethodCall{ 
				Arguments = new[]{ new IdentifierExpression(123m, LiteralFormat.Scalar) },
				PostfixForeExpression = new PostfixExpression_Access{ 
					AccessExpression = new IdentifierExpression("bar"),
					PostfixForeExpression = new IdentifierExpression("loc") { Location = stmt_x.Location }
				} 
			};

			using(ctxt.Push (main, stmt_x.Location))
				ds = ExpressionTypeEvaluation.EvaluateType (x, ctxt) as DSymbol;
			Assert.That (ds, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That (ds.Base, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression ("s2.bar(s)");
			ds = ExpressionTypeEvaluation.EvaluateType (x, ctxt) as DSymbol;
			Assert.That (ds, Is.TypeOf (typeof(TemplateParameterSymbol)));
			Assert.That (ds.Base, Is.TypeOf(typeof(ClassType)));

			x = DParser.ParseExpression ("s.foo(123)");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.TypeOf(typeof(PointerType)));

			x = DParser.ParseExpression ("s.foo");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression ("D.foo");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));

			v = Evaluation.EvaluateValue (x, ctxt);
			Assert.That (v, Is.TypeOf(typeof(PrimitiveValue)));
			Assert.That ((v as PrimitiveValue).Value, Is.EqualTo(8m));

			td = DParser.ParseBasicType("D.foo");
			t = TypeDeclarationResolver.ResolveSingle(td, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		#endregion

		#region Declaration conditions & constraints
		[Test]
		public void DeclCond1()
		{
			var pcl = CreateCache(@"module m;

version = A;

version(Windows)
	int* f(){}
else
	int[] f(){}


debug
	int* d(){}
else
	int[] d(){}


version(A)
	int* a(){}
else
	int[] a(){}

version = B;

version(B)
	import b;

version(C)
	import c;

", @"module b; int pubB;",
@"module c; int pubC;");

			var ctxt = CreateDefCtxt(pcl, pcl[0]["m"]);

			// Test basic version-dependent resolution
			var ms = TypeDeclarationResolver.ResolveIdentifier("f", ctxt, null);
			Assert.AreEqual(1, ms.Length);
			var m = ms[0] as MemberSymbol;
			Assert.IsNotNull(m);

			Assert.That(m.Base, Is.TypeOf(typeof(PointerType)));

			ms = TypeDeclarationResolver.ResolveIdentifier("d", ctxt, null);
			Assert.AreEqual(1, ms.Length);
			m = ms[0] as MemberSymbol;
			Assert.IsNotNull(m);

			Assert.That(m.Base, Is.TypeOf(typeof(PointerType)));

			ctxt.CurrentContext.Set(ctxt.ScopedBlock.EndLocation);
			ms = TypeDeclarationResolver.ResolveIdentifier("a", ctxt, null);
			Assert.AreEqual(1, ms.Length);
			m = ms[0] as MemberSymbol;
			Assert.IsNotNull(m);

			Assert.That(m.Base, Is.TypeOf(typeof(PointerType)));

			ms = TypeDeclarationResolver.ResolveIdentifier("pubB", ctxt, null);
			Assert.AreEqual(1, ms.Length);

			ms = TypeDeclarationResolver.ResolveIdentifier("pubC", ctxt, null);
			Assert.AreEqual(0, ms.Length);
		}

		[Test]
		public void NestedTypes()
		{
			var ctxt = CreateDefCtxt (@"
module A;
class cl
{
	subCl inst;
	class subCl { int b; }
}

cl clInst;
");

			var x = DParser.ParseExpression ("clInst.inst.b");
			var v = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (v, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That ((v as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void IfStmtDeclaredSymbols()
		{
			var ctxt = CreateDefCtxt (@"module A;
void foo()
{
if(auto n = 1234)
	n;
}");
			var A = ctxt.ParseCache [0] ["A"];
			var ifStmt = (A ["foo"].First () as DMethod).Body.SubStatements.ElementAt(0) as IfStatement;
			var nStmt = (ifStmt.ScopedStatement as ExpressionStatement).Expression;
			ctxt.CurrentContext.Set (nStmt.Location);

			var t = ExpressionTypeEvaluation.EvaluateType (nStmt, ctxt);
			Assert.That (t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That ((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void AliasedTemplate()
		{
			var ctxt = CreateDefCtxt(@"module A;
int bar(){}
void* bar(T)(){}
alias bar!int aliasOne;
alias bar aliasTwo;
");

			IExpression x;
			AbstractType t;
			MemberSymbol ms;

			x = DParser.ParseExpression("aliasOne()");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PointerType)));

			x = DParser.ParseExpression("aliasOne!(byte*)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false);

			ms = t as MemberSymbol;
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That(ms.Base, Is.TypeOf(typeof(PointerType)));

			Assert.That(ms.DeducedTypes[0].Base, Is.TypeOf(typeof(PointerType)));

			x = DParser.ParseExpression("aliasTwo");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("aliasOne");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PointerType)));

			x = DParser.ParseExpression("aliasOne!(byte*,int)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.Null);
		}

		[Test]
		public void AliasThis()
		{
			var pcl = CreateCache (@"
module A;

class cl
{
	int a;
	subCl inst;
	alias inst this;
	class subCl { int b; }
}

class notherClass
{
	int[] arr;
	alias arr this;
}

cl clInst;
notherClass ncl;
");

			var mod = pcl [0] ["A"];
			var ctxt = ResolutionContext.Create (pcl, null, mod);

			var x = DParser.ParseExpression ("clInst.a");
			var v = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That ((v as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression ("clInst.inst.b");
			v = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (v, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That ((v as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression ("clInst.b");
			v = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (v, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That ((v as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression ("ncl.arr");
			v = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (v, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That ((v as MemberSymbol).Base, Is.TypeOf(typeof(ArrayType)));

			// Test for static properties
			x = DParser.ParseExpression ("ncl.length");
			v = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (v, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That ((v as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void DeclCond2()
		{
			var pcl = CreateCache(@"module m;

version(X)
	int x;
else
	int y;

class A
{
	version(X)
		void foo()
		{
			x; // 0

			version(X2) // 1
				int x2;

			version(X2) // 2
			{
				x2;
			}

			int t3=0; // 3

			t1; // 4
			t2; // 5
			t3; // 6

			int t1;
			version(X)
				int t2;
		}

	version(X)
		int z;
	int z2;
	version(X_not)
		int z3;
}

version(X)
	int postx;
else
	int posty;


debug = C

debug
	int dbg_a;

debug(C)
	int dbg_b;
else
	int dbg_c;

debug = 4;

debug = 3;

debug(2)
	int dbg_d;

debug(3)
	int dbg_e;

debug(4)
	int dbg_f;

");

			var m = pcl[0]["m"];
			var A = m["A"].First() as DClassLike;
			var foo = A["foo"].First() as DMethod;
			var subst = foo.Body.SubStatements as List<IStatement>;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body);

			var x = TypeDeclarationResolver.ResolveIdentifier("x", ctxt, null);
			Assert.AreEqual(1, x.Length);

			x = TypeDeclarationResolver.ResolveIdentifier("y",ctxt,null);
			Assert.AreEqual(0, x.Length);

			x = TypeDeclarationResolver.ResolveIdentifier("z", ctxt, null);
			Assert.AreEqual(1, x.Length);

			x = TypeDeclarationResolver.ResolveIdentifier("z2", ctxt, null);
			Assert.AreEqual(1, x.Length);

			x = TypeDeclarationResolver.ResolveIdentifier("z3", ctxt, null);
			Assert.AreEqual(0, x.Length);

			IStatement ss;
			ss=((subst[2] as StatementCondition).ScopedStatement as BlockStatement).SubStatements.First();

			ctxt.CurrentContext.Set(ss.Location);
			var x2 = ExpressionTypeEvaluation.EvaluateType(((ExpressionStatement)ss).Expression, ctxt);
			Assert.That(x2, Is.TypeOf(typeof(MemberSymbol)));

			ctxt.CurrentContext.Set((ss = subst[4]).Location);
			x2 = ExpressionTypeEvaluation.EvaluateType(((ExpressionStatement)ss).Expression, ctxt);
			Assert.IsNull(x2);

			ctxt.CurrentContext.Set((ss = subst[5]).Location);
			x2 = ExpressionTypeEvaluation.EvaluateType(((ExpressionStatement)ss).Expression, ctxt);
			Assert.IsNull(x2);

			ctxt.CurrentContext.Set((ss = subst[6]).Location);
			x2 = ExpressionTypeEvaluation.EvaluateType(((ExpressionStatement)ss).Expression, ctxt);
			Assert.That(x2, Is.TypeOf(typeof(MemberSymbol)));

			x = TypeDeclarationResolver.ResolveIdentifier("dbg_a", ctxt, null);
			Assert.AreEqual(1, x.Length);

			ctxt.CurrentContext.Set(m.EndLocation);

			x = TypeDeclarationResolver.ResolveIdentifier("dbg_b", ctxt, null);
			Assert.AreEqual(1, x.Length);

			x = TypeDeclarationResolver.ResolveIdentifier("dbg_c", ctxt, null);
			Assert.AreEqual(0, x.Length);

			x = TypeDeclarationResolver.ResolveIdentifier("dbg_d", ctxt, null);
			Assert.AreEqual(1, x.Length);

			x = TypeDeclarationResolver.ResolveIdentifier("dbg_e", ctxt, null);
			Assert.AreEqual(1, x.Length);

			x = TypeDeclarationResolver.ResolveIdentifier("dbg_f", ctxt, null);
			Assert.AreEqual(0, x.Length);
		}

		[Test]
		public void DeclCond3()
		{
			var pcl = CreateCache(@"module m;
version = X;

version(X)
	int a;
else
	int b;

version(Y)
	int c;

debug
	int dbgX;
else
	int dbgY;

", @"module B;

debug int dbg;
else int noDbg;

debug = D;

debug(D)
	int a;
else
	int b;

template T(O)
{
	version(Windows)
		O[] T;
	else
		O T;
}

void main()
{
	a;
	b;
	dbg;
	noDbg;
}");
			var mod = pcl[0]["m"];
			var ctxt = CreateDefCtxt(pcl, mod, mod.EndLocation);

			var x = TypeDeclarationResolver.ResolveIdentifier("a", ctxt,null);
			Assert.AreEqual(1, x.Length);

			x = TypeDeclarationResolver.ResolveIdentifier("b", ctxt, null);
			Assert.AreEqual(0, x.Length);

			x = TypeDeclarationResolver.ResolveIdentifier("c", ctxt, null);
			Assert.AreEqual(0, x.Length);

			x = TypeDeclarationResolver.ResolveIdentifier("dbgX", ctxt, null);
			Assert.AreEqual(1, x.Length);

			x = TypeDeclarationResolver.ResolveIdentifier("dbgY", ctxt, null);
			Assert.AreEqual(0, x.Length);

			ctxt.CurrentContext.Set(mod = pcl[0]["B"], mod.EndLocation);

			x = TypeDeclarationResolver.ResolveIdentifier("dbg", ctxt, null);
			Assert.AreEqual(1, x.Length);

			x = TypeDeclarationResolver.ResolveIdentifier("noDbg", ctxt, null);
			Assert.AreEqual(0, x.Length);

			x = TypeDeclarationResolver.ResolveIdentifier("a", ctxt, null);
			Assert.AreEqual(1, x.Length);

			x = TypeDeclarationResolver.ResolveIdentifier("b", ctxt, null);
			Assert.AreEqual(0, x.Length);

			DToken tk;
			x = TypeDeclarationResolver.Resolve(DParser.ParseBasicType("T!int",out tk),ctxt);
			Assert.AreEqual(1, x.Length);
			var t = x[0];
			Assert.That(t,Is.TypeOf(typeof(MemberSymbol)));
			t = ((MemberSymbol)t).Base;
			Assert.That(t,Is.TypeOf(typeof(ArrayType)));

			var main = pcl[0]["B"]["main"].First() as DMethod;
			var subSt = main.Body.SubStatements as List<IStatement>;
			using (ctxt.Push(main))
			{
				var ss = subSt[0] as ExpressionStatement;
				ctxt.CurrentContext.Set(ss.Location);
				t = ExpressionTypeEvaluation.EvaluateType(ss.Expression, ctxt);
				Assert.IsNotNull(t);

				ss = subSt[1] as ExpressionStatement;
				ctxt.CurrentContext.Set(ss.Location);
				t = ExpressionTypeEvaluation.EvaluateType(ss.Expression, ctxt);
				Assert.IsNull(t);

				ss = subSt[2] as ExpressionStatement;
				ctxt.CurrentContext.Set(ss.Location);
				t = ExpressionTypeEvaluation.EvaluateType(ss.Expression, ctxt);
				Assert.IsNotNull(t);

				ss = subSt[3] as ExpressionStatement;
				ctxt.CurrentContext.Set(ss.Location);
				t = ExpressionTypeEvaluation.EvaluateType(ss.Expression, ctxt);
				Assert.IsNull(t);
			}
		}

		[Test]
		public void DeclCond4()
		{
			var ctxt = CreateCtxt("A",@"module A;
version = X;

version(X){
	int vx;
}
else{
	int vy;
}

int xx1;

version = Y;

version(Y)
{
	int xa;
}
else
	int xb;

int xx2;

version(Z){
	version = U;
}

version(U)
	int xu;

int xx3;
");
			var A = ctxt.ParseCache[0]["A"];
			ctxt.CurrentContext.Set(A["vy"].First().Location);

			AbstractType t;
			t = TypeDeclarationResolver.ResolveSingle("vy", ctxt, null);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));

			t = TypeDeclarationResolver.ResolveSingle("vx", ctxt, null);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));


			ctxt.CurrentContext.Set(A["xx2"].First().Location);

			t = TypeDeclarationResolver.ResolveSingle("xa", ctxt, null);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));

			t = TypeDeclarationResolver.ResolveSingle("xb", ctxt, null);
			Assert.That(t, Is.Null);

			ctxt.CurrentContext.Set(A["xx3"].First().Location);

			t = TypeDeclarationResolver.ResolveSingle("xu", ctxt, null);
			Assert.That(t, Is.Null);
		}
		
		[Test]
		public void DeclConstraints()
		{
			var pcl=CreateCache(@"module A;

const i = 12;

static if(i>0)
	int a;
else
	int b;

template Templ(T)
{
	static if(is(T:int))
		enum Templ = 1;
	else
		enum Templ = 0;
}

static if(Templ!int == 1)
	int c;

static if(Templ!float)
	int d;
else
	int e;");
			
			var A = pcl[0]["A"];
			
			var ctxt = CreateDefCtxt(pcl, A, null);
			
			var x = TypeDeclarationResolver.ResolveIdentifier("a", ctxt, null);
			Assert.AreEqual(1, x.Length);
			
			x = TypeDeclarationResolver.ResolveIdentifier("b",ctxt,null);
			Assert.AreEqual(0, x.Length);
			
			var v = Evaluation.EvaluateValue(DParser.ParseExpression("Templ!int"), ctxt, true);
			Assert.That(v, Is.InstanceOf(typeof(VariableValue)));
			v = Evaluation.EvaluateValue(v as VariableValue, new StandardValueProvider(ctxt));
			Assert.That(v, Is.InstanceOf(typeof(PrimitiveValue)));
			var pv = (PrimitiveValue)v;
			Assert.AreEqual(1m, pv.Value);
			
			x = TypeDeclarationResolver.ResolveIdentifier("c", ctxt, null);
			Assert.AreEqual(1, x.Length);
			
			x = TypeDeclarationResolver.ResolveIdentifier("d", ctxt, null);
			Assert.AreEqual(0, x.Length);
			
			x = TypeDeclarationResolver.ResolveIdentifier("e", ctxt, null);
			Assert.AreEqual(1, x.Length);
		}
		
		[Test]
		public void DeclConditions2()
		{
			var pcl = ResolutionTests.CreateCache(@"module A;
class cl{}",
@"module B;

class home {}

static if(!is(typeof(asd)))
	import C;
static if(is(typeof(home)))
	import A;

void bar();
",
@"module C;
class imp{}");
			
			var B = (DModule)pcl[0]["B"];
			var ctxt = CreateDefCtxt(pcl, B["bar"].First() as DMethod);
			
			var x = TypeDeclarationResolver.ResolveIdentifier("imp",ctxt, null);
			Assert.That(x.Length, Is.EqualTo(0));
			
			x = TypeDeclarationResolver.ResolveIdentifier("cl",ctxt,null);
			Assert.That(x.Length, Is.EqualTo(1));
		}
		
		[Test]
		public void DeclConstraints3()
		{
			var pcl = CreateCache(@"module A;
class cl(T) if(is(T==int))
{}

class aa(T) if(is(T==float)) {}
class aa(T) if(is(T==int)) {}");
			var A = pcl[0]["A"];
			var ctxt = CreateDefCtxt(pcl, A);
			
			var x = TypeDeclarationResolver.ResolveSingle(new IdentifierDeclaration("cl"),ctxt,null,true);
			Assert.That(x, Is.Null);
			
			var ex = DParser.ParseAssignExpression("cl!int");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.Not.TypeOf(typeof(AmbiguousType)));
			
			ex = DParser.ParseAssignExpression("cl!float");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.Null);
			
			ex = DParser.ParseAssignExpression("aa!float");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.Not.TypeOf(typeof(AmbiguousType)));
			var t = x as ClassType;
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Definition, Is.EqualTo(A["aa"].First()));
			
			ex = DParser.ParseAssignExpression("aa!int");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.Not.TypeOf(typeof(AmbiguousType)));
			t = x as ClassType;
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Definition, Is.EqualTo((A["aa"] as List<INode>)[1]));
			
			ex = DParser.ParseAssignExpression("aa!string");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.Null);
		}

		[Test]
		public void Unqual()
		{
			var ctxt = CreateCtxt("A", @"module std.typecons;
template Unqual(T)
{
    version (none) // Error: recursive alias declaration @@@BUG1308@@@
    {
             static if (is(T U ==     const U)) alias Unqual!U Unqual;
        else static if (is(T U == immutable U)) alias Unqual!U Unqual;
        else static if (is(T U ==     inout U)) alias Unqual!U Unqual;
        else static if (is(T U ==    shared U)) alias Unqual!U Unqual;
        else                                    alias        T Unqual;
    }
    else // workaround
    {
             static if (is(T U == shared(inout U))) alias U Unqual;
        else static if (is(T U == shared(const U))) alias U Unqual;
        else static if (is(T U ==        inout U )) alias U Unqual;
        else static if (is(T U ==        const U )) alias U Unqual;
        else static if (is(T U ==    immutable U )) alias U Unqual;
        else static if (is(T U ==       shared U )) alias U Unqual;
        else                                        alias T Unqual;
    }
}", @"module A;

import std.typecons;

class Tmpl(M)
{
	Unqual!M inst;
}

alias immutable(int[]) ImmIntArr;

unittest
{
    static assert(is(A == immutable(int)[]));
}
");

			ITypeDeclaration td;
			AbstractType t;
			PrimitiveType pt;
			ArrayType at;

			var A = ctxt.ParseCache[0]["A"];
			var Tmpl = A["Tmpl"].First() as DClassLike;
			ctxt.CurrentContext.Set(Tmpl);

			td = DParser.ParseBasicType("inst");
			t = TypeDeclarationResolver.ResolveSingle(td, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			t = (t as MemberSymbol).Base;
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));

			ctxt.CurrentContext.Set(A);

			td = DParser.ParseBasicType("Unqual!ImmIntArr");
			t = TypeDeclarationResolver.ResolveSingle(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			at = (t as TemplateParameterSymbol).Base as ArrayType;
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(ArrayType)));
			pt = at.ValueType as PrimitiveType;
			Assert.That(at.ValueType, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(pt.Modifier, Is.EqualTo(DTokens.Immutable));
			// immutable(int[]) becomes immutable(int)[] ?!

			ctxt.CurrentContext.DeducedTemplateParameters.Clear();

			td = DParser.ParseBasicType("Unqual!int");
			t = TypeDeclarationResolver.ResolveSingle(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));

			ctxt.CurrentContext.DeducedTemplateParameters.Clear();

			td = DParser.ParseBasicType("Unqual!(const(int))");
			t = TypeDeclarationResolver.ResolveSingle(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			pt = (t as TemplateParameterSymbol).Base as PrimitiveType;
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(pt.Modifier, Is.EqualTo(0));

			ctxt.CurrentContext.DeducedTemplateParameters.Clear();

			td = DParser.ParseBasicType("Unqual!(inout(int))");
			t = TypeDeclarationResolver.ResolveSingle(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			pt = (t as TemplateParameterSymbol).Base as PrimitiveType;
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(pt.Modifier, Is.EqualTo(0));

			ctxt.CurrentContext.DeducedTemplateParameters.Clear();

			td = DParser.ParseBasicType("Unqual!(immutable(int))");
			t = TypeDeclarationResolver.ResolveSingle(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			pt = (t as TemplateParameterSymbol).Base as PrimitiveType;
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(pt.Modifier, Is.EqualTo(0));

			ctxt.CurrentContext.DeducedTemplateParameters.Clear();

			td = DParser.ParseBasicType("Unqual!(shared(int))");
			t = TypeDeclarationResolver.ResolveSingle(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			pt = (t as TemplateParameterSymbol).Base as PrimitiveType;
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(pt.Modifier, Is.EqualTo(0));

			ctxt.CurrentContext.DeducedTemplateParameters.Clear();

			td = DParser.ParseBasicType("Unqual!(const(shared(int)))");
			t = TypeDeclarationResolver.ResolveSingle(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			pt = (t as TemplateParameterSymbol).Base as PrimitiveType;
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(pt.Modifier, Is.EqualTo(0));

			ctxt.CurrentContext.DeducedTemplateParameters.Clear();

			td = DParser.ParseBasicType("Unqual!(shared const int)");
			t = TypeDeclarationResolver.ResolveSingle(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			pt = (t as TemplateParameterSymbol).Base as PrimitiveType;
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(pt.Modifier, Is.EqualTo(0));
		}

		[Test]
		public void MethodParameterTypeResolutionScope()
		{
			var ctxt = CreateCtxt("A", @"module A;
public static struct Namespace
{
alias ulong UserId;
int getGames(UserId); // UserId, not Namespace.UserId
}

Namespace.UserId uid;
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("Namespace.getGames(uid)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PrimitiveType)));

		}

		/// <summary>
		/// Strings literals which are sliced are now implicitly convertible to a char pointer:
		/// 
		/// To help ease interacting with C libraries which expect strings as 
		/// null-terminated pointers, slicing string literals (not variables!) 
		/// will now allow the implicit conversion to such a pointer:
		/// </summary>
		[Test]
		public void StringSliceConvertability()
		{
			var ctxt = CreateCtxt ("A", @"module A;");

			var constChar = new PointerType (new PrimitiveType(DTokens.Char, DTokens.Const), null);

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression ("\"abc\"");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (ResultComparer.IsImplicitlyConvertible (t, constChar, ctxt));

			x = DParser.ParseExpression ("\"abc\"[0..2]");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (ResultComparer.IsImplicitlyConvertible (t, constChar, ctxt));
		}

		[Test]
		public void AliasThis2()
		{
			var ctxt = CreateCtxt("A", @"module B;
struct LockedConnection(Connection) {
	private {
		Connection m_conn;
	}
	
	@property inout(Connection) __conn() inout { return m_conn; }
	
	alias __conn this;
}

", @"module A;

import B;

class ConnectionPool(Connection)
{
	private {
		Connection[] m_connections;
	}
	
	LockedConnection!Connection lockConnection(){}
}

final class RedisConnection
{
	int[] bar;
	int request(string command, in ubyte[][] args...) {}
}

ConnectionPool!RedisConnection m_connections;
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("m_connections.lockConnection().bar");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			t = (t as DerivedDataType).Base;
			Assert.That(t, Is.TypeOf(typeof(ArrayType)));
		}

		[Test]
		public void AliasThis3()
		{
			var ctxt = CreateCtxt("A", @"module A;
struct CL {
  enum en : Color {
    none = Color( 0, 0, 0, 0 ),
   
    white = Color( 1 ),
    black = Color( 0 ),
   
    red = Color( 1, 0, 0 ),
    green = Color( 0, 1, 0 ),
    blue = Color( 0, 0, 1 ),
  }
  alias en this;
 
  alias assoc = EnumAssociativeFunc!( en, en.none );
}
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("CL.white");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Definition, Is.TypeOf(typeof(DEnumValue)));
		}

		[Test]
		public void AliasThisSO()
		{
			var ctxt = CreateCtxt ("A", @"module A;
class Cls
{
	alias derp this;
	alias derp this;
}

Cls inst;
");
			var x = DParser.ParseExpression ("inst.a");
			ExpressionTypeEvaluation.EvaluateType (x, ctxt);
		}

		[Test]
		public void AliasThisOnNonInstances()
		{
			var ctxt = CreateCtxt("A", @"module A;
struct S1 { int a; }
struct S2(T) {
	int b;
	alias T this;
}

S2!S1 s;
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("s.a");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Definition, Is.TypeOf(typeof(DVariable)));
		}

		[Test]
		public void TypeofIntSize()
		{
			var ctxt = CreateDefCtxt();

			ITypeDeclaration td;
			AbstractType t;
			DToken tk;

			td = DParser.ParseBasicType ("typeof(int.sizeof)", out tk);
			t = TypeDeclarationResolver.ResolveSingle(td, ctxt);

			Assert.That (t, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That ((t as PrimitiveType).TypeToken, Is.EqualTo(DTokens.Uint));
		}
		#endregion
		
		#region Mixins
		[Test]
		public void Mixins1()
		{
			var pcl = ResolutionTests.CreateCache(@"module A;
private mixin(""int privA;"");
package mixin(""int packA;"");
private int privAA;
package int packAA;

mixin(""int x; int ""~""y""~"";"");",

			                                      @"module pack.B;
import A;",
			                                     @"module C; import A;");
			
			var ctxt = ResolutionTests.CreateDefCtxt(pcl, pcl[0]["A"]);
			
			var x = TypeDeclarationResolver.ResolveIdentifier("x", ctxt, null);
			Assert.That(x.Length, Is.EqualTo(1));
			
			x = TypeDeclarationResolver.ResolveIdentifier("y", ctxt, null);
			Assert.That(x.Length, Is.EqualTo(1));
			
			ctxt.CurrentContext.Set(pcl[0]["pack.B"]);
			
			x = TypeDeclarationResolver.ResolveIdentifier("x", ctxt, null);
			Assert.That(x.Length, Is.EqualTo(1));
			
			x = TypeDeclarationResolver.ResolveIdentifier("privAA", ctxt, null);
			Assert.That(x.Length, Is.EqualTo(0));
			
			x = TypeDeclarationResolver.ResolveIdentifier("privA", ctxt, null);
			Assert.That(x.Length, Is.EqualTo(0));
			
			x = TypeDeclarationResolver.ResolveIdentifier("packAA", ctxt, null);
			Assert.That(x.Length, Is.EqualTo(0));
			
			x = TypeDeclarationResolver.ResolveIdentifier("packA", ctxt, null);
			Assert.That(x.Length, Is.EqualTo(0));
			
			ctxt.CurrentContext.Set(pcl[0]["C"]);
			
			x = TypeDeclarationResolver.ResolveIdentifier("privA", ctxt, null);
			Assert.That(x.Length, Is.EqualTo(0));
			
			x = TypeDeclarationResolver.ResolveIdentifier("packAA", ctxt, null);
			Assert.That(x.Length, Is.EqualTo(1));
			
			x = TypeDeclarationResolver.ResolveIdentifier("packA", ctxt, null);
			Assert.That(x.Length, Is.EqualTo(1));
		}
		
		[Test]
		public void Mixins2()
		{
			var pcl = ResolutionTests.CreateCache(@"module A; 

void main()
{
	mixin(""int x;"");
	
	derp;
	
	mixin(""int y;"");
}
");
			
			var A = pcl[0]["A"];
			var main = A["main"].First() as DMethod;
			var stmt = main.Body.SubStatements.ElementAt(1);
			var ctxt = ResolutionTests.CreateDefCtxt(pcl, main, main.Body);
			
			var x = TypeDeclarationResolver.ResolveIdentifier("x", ctxt, stmt);
			Assert.That(x.Length, Is.EqualTo(1));
			
			x = TypeDeclarationResolver.ResolveIdentifier("y", ctxt, stmt);
			Assert.That(x.Length, Is.EqualTo(0));
		}
		
		[Test]
		public void Mixins3()
		{
			var ctxt = ResolutionTests.CreateDefCtxt(@"module A;
template Temp(string v)
{
	mixin(v);
}

class cl
{
	mixin(""int someInt=345;"");
}");
			IExpression ex;
			AbstractType t;

			ex = DParser.ParseExpression("(new cl()).someInt");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));

			ex = DParser.ParseExpression("Temp!\"int Temp;\"");
			t = ExpressionTypeEvaluation.EvaluateType(ex,ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));
		}
		
		[Test]
		public void Mixins4()
		{
			var pcl = ResolutionTests.CreateCache(@"module A; enum mixinStuff = q{import C;};",
			                                      @"module B; import A; mixin(mixinStuff); class cl{ void bar(){  } }",
			                                      @"module C; void CFoo() {}");
			
			var B =pcl[0]["B"];
			var ctxt = ResolutionTests.CreateDefCtxt(pcl, B);
			
			var t = TypeDeclarationResolver.ResolveSingle("CFoo", ctxt, null);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Definition, Is.TypeOf(typeof(DMethod)));
			
			var bar = (B["cl"].First() as DClassLike)["bar"].First() as DMethod;
			ctxt.CurrentContext.Set(bar, bar.Body.Location);
			
			t = TypeDeclarationResolver.ResolveSingle("CFoo", ctxt, null);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Definition, Is.TypeOf(typeof(DMethod)));
		}
		
		[Test]
		public void Mixins5()
		{
			var pcl = ResolutionTests.CreateCache(@"module A;
mixin(""template mxT(string n) { enum mxT = n; }"");
mixin(""class ""~mxT!(""myClass"")~"" {}"");
", @"module B;
mixin(""class ""~mxT!(""myClass"")~"" {}"");
mixin(""template mxT(string n) { enum mxT = n; }"");
");
			
			var ctxt = ResolutionTests.CreateDefCtxt(pcl, pcl[0]["A"]);
			
			var t = TypeDeclarationResolver.ResolveSingle("myClass", ctxt, null);
			Assert.That(t, Is.TypeOf(typeof(ClassType)));
			
			ctxt.CurrentContext.Set(pcl[0]["B"]);
			
			t = TypeDeclarationResolver.ResolveSingle("myClass", ctxt, null);
			Assert.That(t, Is.Null);
		}

		[Test]
		public void Mixins6()
		{
			var ctxt = CreateCtxt("A",@"module A;
interface IUnknown {}

public template uuid(T, const char[] g) {
	const char [] uuid =
		""const IID IID_""~T.stringof~""={ 0x"" ~ g[0..8] ~ "",0x"" ~ g[9..13] ~ "",0x"" ~ g[14..18] ~ "",[0x"" ~ g[19..21] ~ "",0x"" ~ g[21..23] ~ "",0x"" ~ g[24..26] ~ "",0x"" ~ g[26..28] ~ "",0x"" ~ g[28..30] ~ "",0x"" ~ g[30..32] ~ "",0x"" ~ g[32..34] ~ "",0x"" ~ g[34..36] ~ ""]};""
		""template uuidof(T:""~T.stringof~""){""
		""    const IID uuidof ={ 0x"" ~ g[0..8] ~ "",0x"" ~ g[9..13] ~ "",0x"" ~ g[14..18] ~ "",[0x"" ~ g[19..21] ~ "",0x"" ~ g[21..23] ~ "",0x"" ~ g[24..26] ~ "",0x"" ~ g[26..28] ~ "",0x"" ~ g[28..30] ~ "",0x"" ~ g[30..32] ~ "",0x"" ~ g[32..34] ~ "",0x"" ~ g[34..36] ~ ""]};""
		""}"";
}
");

			IExpression x;
			ISymbolValue v;

			x = DParser.ParseExpression(@"uuid!(IUnknown, ""00000000-0000-0000-C000-000000000046"")");
			(x as TemplateInstanceExpression).Location = new CodeLocation(1, 3);
			v = D_Parser.Resolver.ExpressionSemantics.Evaluation.EvaluateValue(x, ctxt);

			var av = v as ArrayValue;
			Assert.That(v, Is.TypeOf(typeof(ArrayValue)));
			Assert.That(av.IsString);
			Assert.That(av.StringValue, Is.EqualTo(@"const IID IID_A.IUnknown={ 0x00000000,0x0000,0x0000,[0xC0,0x00,0x00,0x00,0x00,0x00,0x00,0x46]};template uuidof(T:A.IUnknown){    const IID uuidof ={ 0x00000000,0x0000,0x0000,[0xC0,0x00,0x00,0x00,0x00,0x00,0x00,0x46]};}"));
		}

		[Test]
		public void Mixins7()
		{
			var ctxt = CreateCtxt("A", @"module A;
mixin template mix_test() {int a;}

class C {
enum mix = ""test"";
mixin( ""mixin mix_"" ~ mix ~ "";"" );
}

C c;
");
			IExpression x;
			AbstractType t;
			ISymbolValue v;

			x = DParser.ParseExpression("c.a");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}
		
		[Test]
		public void NestedMixins()
		{
			var pcl = CreateCache(@"module A;
mixin(""template mxT1(string n) { enum mxT1 = n; }"");
mixin(mxT1!(""template"")~"" mxT2(string n) { enum mxT2 = n; }"");
mixin(""template mxT3(string n) { ""~mxT2!(""enum"")~"" mxT3 = n; }"");

mixin(""template mxT4(""~mxT3!(""string"")~"" n) { enum mxT4 = n; }"");
mixin(""class ""~mxT4!(""myClass"")~"" {}"");"");");
			
			var ctxt = CreateDefCtxt(pcl, pcl[0]["A"]);
			
			var t = TypeDeclarationResolver.ResolveSingle("mxT1",ctxt,null);
			Assert.That(t,Is.TypeOf(typeof(TemplateType)));
			
			t = TypeDeclarationResolver.ResolveSingle("mxT2",ctxt,null);
			Assert.That(t,Is.TypeOf(typeof(TemplateType)));
			
			t = TypeDeclarationResolver.ResolveSingle("mxT3",ctxt,null);
			Assert.That(t,Is.TypeOf(typeof(TemplateType)));
			
			t = TypeDeclarationResolver.ResolveSingle("mxT4",ctxt,null);
			Assert.That(t,Is.TypeOf(typeof(TemplateType)));
			
			t = TypeDeclarationResolver.ResolveSingle("myClass",ctxt,null);
			Assert.That(t,Is.TypeOf(typeof(ClassType)));
		}
		#endregion

		#region Operator Overloads
		[Test]
		public void opSlice()
		{
			var ctxt = CreateCtxt("A", @"module A;

struct S(T)
{
	T opSlice() {}
	int[] opSlice(int dope);
	T* opSlice(U)(U x, size_t y); // overloads a[i .. j]
}

S!int s;
");
			IExpression x;
			AbstractType t;
			/*
			x = DParser.ParseExpression("s[]");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((t as DerivedDataType).Base, Is.TypeOf(typeof(PrimitiveType)));
			*/
			x = DParser.ParseExpression("s[1..3]");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(PointerType)));
			t = (t as PointerType).Base;
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((t as DerivedDataType).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void opIndex()
		{
			var ctxt = CreateCtxt("A", @"module A;

struct S(T)
{
	T opIndex(size_t i) {}
	int[] opIndex(int j,int k);
	int* opIndex(int j, int k, int l);
}

S!int s;
");
			IExpression x;
			AbstractType t;
			/*
			x = DParser.ParseExpression("s[1]");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((t as DerivedDataType).Base, Is.TypeOf(typeof(PrimitiveType)));
			*/
			x = DParser.ParseExpression("s[1,2]");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(ArrayType)));

			x = DParser.ParseExpression("s[1,2,3]");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(PointerType)));
		}
		#endregion

		#region Template Mixins
		[Test]
		public void TemplateMixins1()
		{
			var pcl = ResolutionTests.CreateCache(@"module A;
mixin template Mx(T)
{
	T myFoo;
}

mixin template Mx1()
{
	int someProp;
}
mixin Mx1;
mixin Mx!int;

mixin Mx1 myMx;
mixin Mx!float myTempMx;");
			
			var A =pcl[0]["A"];
			var ctxt = ResolutionTests.CreateDefCtxt(pcl, A);
			
			var ex = DParser.ParseExpression("someProp");
			var x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.InstanceOf(typeof(MemberSymbol)));
			Assert.That((x as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			
			ex = DParser.ParseExpression("myFoo");
			x = ExpressionTypeEvaluation.EvaluateType(ex,ctxt);
			Assert.That(x, Is.InstanceOf(typeof(MemberSymbol)));
			var ms = x as MemberSymbol;
			Assert.That(ms.Base, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((ms.Base as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			
			ex = DParser.ParseExpression("myMx.someProp;");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.InstanceOf(typeof(MemberSymbol)));
			Assert.That((x as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			
			ex = DParser.ParseExpression("myTempMx.myFoo");
			x = ExpressionTypeEvaluation.EvaluateType(ex,ctxt);
			Assert.That(x, Is.InstanceOf(typeof(MemberSymbol)));
			ms = x as MemberSymbol;
			Assert.That(ms.Base, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((ms.Base as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}
		
		[Test]
		public void TemplateMixins2()
		{
			var pcl = ResolutionTests.CreateCache(@"module A;
mixin template Foo() {
  int[] func() { writefln(""Foo.func()""); }
}

class Bar {
  mixin Foo;
}

class Code : Bar {
  float func() { writefln(""Code.func()""); }
}

void test() {
  Bar b = new Bar();
  b.func();      // calls Foo.func()

  b = new Code();
  b.func();      // calls Code.func()
}");
			
			var A =pcl[0]["A"];
			var ctxt = ResolutionTests.CreateDefCtxt(pcl, A);
			
			var ex = DParser.ParseExpression("(new Code()).func");
			var x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.TypeOf(typeof(PrimitiveType)));
			
			ex = DParser.ParseExpression("(new Bar()).func");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.InstanceOf(typeof(ArrayType)));
		}
		
		[Test]
		public void TemplateMixins3()
		{
			var pcl = ResolutionTests.CreateCache(@"module A;
mixin template Singleton(I) {
	static I getInstance() {}
	
	void singletonBar() {}
}

mixin template Foo(T) {
  int localDerp;
  T[] arrayTest;
}

class clA
{
	mixin Singleton!clA;
	
	void clFoo() {}
}

void foo() {
	localDerp;
	mixin Foo!int;
	localDerp;
	arrayTest[0];
}");
			var A = pcl[0]["A"];
			var foo = A["foo"].First() as DMethod;
			var ctxt = ResolutionTests.CreateDefCtxt(pcl, foo, foo.Body);
			var subSt = foo.Body.SubStatements as List<IStatement>;
			
			var t = ExpressionTypeEvaluation.EvaluateType((subSt[0] as ExpressionStatement).Expression,ctxt);
			Assert.That(t, Is.Null);
			
			t = ExpressionTypeEvaluation.EvaluateType((subSt[2] as ExpressionStatement).Expression,ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			var ms = t as MemberSymbol;
			Assert.That(ms.Base, Is.TypeOf(typeof(PrimitiveType)));
			
			t = ExpressionTypeEvaluation.EvaluateType((subSt[3] as ExpressionStatement).Expression,ctxt);
			Assert.That(t, Is.TypeOf(typeof(ArrayAccessSymbol)));
			t = (t as ArrayAccessSymbol).Base;
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			
			var ex = DParser.ParseExpression("clA.getInstance");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt, false);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			
			foo = (A["Singleton"].First() as DClassLike)["singletonBar"].First() as DMethod;
			ctxt.CurrentContext.Set(foo, foo.Body.Location);
			t = TypeDeclarationResolver.ResolveSingle("I",ctxt,null);
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			
			foo = (A["clA"].First() as DClassLike)["clFoo"].First() as DMethod;
			ctxt.CurrentContext.Set(foo, foo.Body.Location);
			t = TypeDeclarationResolver.ResolveSingle("I",ctxt,null);
			Assert.That(t, Is.Null);
		}
		
		[Test]
		public void TemplateMixins4()
		{
			var pcl = ResolutionTests.CreateCache(@"module A;
mixin template mixedInImports()
{
	import C;
}",			                                      @"module B; import A; mixin mixedInImports; class cl{ void bar(){  } }",
			                                      @"module C;
void CFoo() {}");
			
			var B =pcl[0]["B"];
			var ctxt = ResolutionTests.CreateDefCtxt(pcl, B);
			
			var t = TypeDeclarationResolver.ResolveSingle("CFoo", ctxt, null);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Definition, Is.TypeOf(typeof(DMethod)));
			
			var bar = (B["cl"].First() as DClassLike)["bar"].First() as DMethod;
			ctxt.CurrentContext.Set(bar, bar.Body.Location);
			
			t = TypeDeclarationResolver.ResolveSingle("CFoo", ctxt, null);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Definition, Is.TypeOf(typeof(DMethod)));
		}

		[Test]
		public void AutoImplementHook()
		{
			var ctxt = CreateCtxt("A", @"module A;
import std.typecons;
struct Parms;
interface TestAPI
{
        string foo(string name);
        string bar(string lol, int lal, Parms parms);
}

AutoImplement!(TestAPI, generateEmptyFunction) derp;
BlackHole!TestAPI yorp;
		", @"module std.typecons;

template generateEmptyFunction(C, func.../+[BUG 4217]+/)
{
    static if (is(ReturnType!(func) == void))
        enum string generateEmptyFunction = q{
        };
    else static if (functionAttributes!(func) & FunctionAttribute.ref_)
        enum string generateEmptyFunction = q{
            static typeof(return) dummy;
            return dummy;
        };
    else
        enum string generateEmptyFunction = q{
            return typeof(return).init;
        };
}

template isAbstractFunction() {}

class AutoImplement(Base, alias how, alias what = isAbstractFunction) : Base
{
    private alias AutoImplement_Helper!(
            ""autoImplement_helper_"", ""Base"", Base, how, what )
             autoImplement_helper_;
    mixin(autoImplement_helper_.code);
}

template BlackHole(Base)
{
    alias AutoImplement!(Base, generateEmptyFunction, isAbstractFunction)
            BlackHole;
}
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("BlackHole!TestAPI");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false);

			Assert.That (t, Is.TypeOf (typeof(ClassType)));
			Assert.That ((t as ClassType).BaseInterfaces[0], Is.TypeOf(typeof(InterfaceType)));

			x = DParser.ParseExpression("yorp.foo");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false);

			Assert.That (t, Is.TypeOf (typeof(MemberSymbol)));

			x = DParser.ParseExpression("AutoImplement!(TestAPI, generateEmptyFunction)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false);

			Assert.That (t, Is.TypeOf (typeof(ClassType)));

			x = DParser.ParseExpression("derp.foo");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false);

			Assert.That (t, Is.TypeOf (typeof(MemberSymbol)));
		}

		[Test]
		public void BitfieldsHook()
		{
			var ctxt = CreateCtxt("A", @"module A;
import std.bitmanip;

struct S {
    int a;
    mixin(bitfields!(
        uint, ""x"",    2,
        int*,  ""y"",    3,
        uint[], ""z"",    2,
        bool, ""flag"", 1));
}

S s;
		", @"module std.bitmanip;

template bitfields(T...)
{
    enum { bitfields = createFields!(createStoreName!(T), 0, T).result }
}
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("s.x");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void TemplateAliasParams()
		{
			var ctxt = CreateCtxt("A", @"module A;
class Mixery(U)
{
	int a;
	U u;
}

struct TestField(T)
{
        T t;
        alias t this; // doesn't matter
}
 
mixin template MyTemplate(alias T)
{
        auto Field1 = T!(ulong)();
        auto Field2 = T!(string)();
}
 
class TestClass
{
        mixin MyTemplate!(TestField);
}

TestClass c;
void main(string[] args) { }
");
			var A = ctxt.ParseCache[0]["A"];
			var main = A["main"].First() as DMethod;
			var TestField = A["TestField"].First() as DClassLike;
			ctxt.CurrentContext.Set(main);

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("MyTemplate!(TestField)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MixinTemplateType)));
			var MyTemplate = t as MixinTemplateType;
			var MyTemplateDef = MyTemplate.Definition as DClassLike;
			var firstDeducedParam = MyTemplate.DeducedTypes[0];
			Assert.That((firstDeducedParam.Definition as TemplateParameter.Node).TemplateParameter, Is.SameAs(MyTemplateDef.TemplateParameters[0]));
			Assert.That(firstDeducedParam.Base, Is.TypeOf(typeof(StructType)));
		
			ctxt.CurrentContext.Set(MyTemplateDef);
			ctxt.CurrentContext.IntroduceTemplateParameterTypes(MyTemplate);

			x = DParser.ParseExpression("T!ulong");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(StructType)));

			ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(MyTemplate);

			ctxt.CurrentContext.Set(main);
			x = DParser.ParseExpression("c.Field1");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			var @base = (t as MemberSymbol).Base;
			Assert.That(@base, Is.TypeOf(typeof(StructType)));
		}

		[Test]
		public void StdSignals()
		{
			var ctxt = CreateCtxt ("A", @"module A;
mixin template Signal(T1 ...)
{
	final int emit( T1 i ) {}
}

class D
{
	mixin Signal!int sig;
	mixin Signal!int;
}

D d;
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression ("d.emit(123)");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));

			x = DParser.ParseExpression ("d.sig.emit(123)");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));
		}
		#endregion
	}
}
