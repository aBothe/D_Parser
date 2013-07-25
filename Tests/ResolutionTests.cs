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

namespace Tests
{
	[TestFixture]
	public class ResolutionTests
	{
		public static DModule objMod = DParser.ParseString(@"module object;
						alias immutable(char)[] string;
						alias immutable(wchar)[] wstring;
						alias immutable(dchar)[] dstring;
						class Object { string toString(); }");

		public static ParseCacheView CreateCache(params string[] moduleCodes)
		{
			var r = new MutableRootPackage (objMod);

			foreach (var code in moduleCodes)
				r.AddModule(DParser.ParseString(code));

			UFCSCache.SingleThreaded = true;
			var pcl = new ParseCacheView (new [] { r });
			r.UfcsCache.BeginUpdate (pcl);
			r.UfcsCache.WaitForFinish ();

			return pcl;
		}

		public static ResolutionContext CreateDefCtxt(ParseCacheView pcl, IBlockNode scope, IStatement stmt=null)
		{
			var r = ResolutionContext.Create(pcl, new ConditionalCompilationFlags(new[]{"Windows","all"},1,true,null,0), scope, stmt);
			CompletionOptions.Instance.DisableMixinAnalysis = false;
			return r;
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

void asdf(int ni=23) {
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

			ctxt.CurrentContext.Set(pcl[0]["modC"]);
			t = TypeDeclarationResolver.ResolveIdentifier("T", ctxt, null);
			Assert.That(t.Length, Is.EqualTo(1));
			Assert.That((t[0] as DSymbol).Definition.Parent, Is.SameAs(pcl[0]["modC"]));

			ctxt.CurrentContext.Set(pcl[0]["modB"]);
			t = TypeDeclarationResolver.ResolveIdentifier("T", ctxt, null);
			Assert.That(t.Length, Is.EqualTo(2));

			ctxt.ResolutionErrors.Clear();
			var mod = pcl[0]["modE"];
			ctxt.CurrentContext.Set(mod);
			t = TypeDeclarationResolver.ResolveIdentifier("U", ctxt, null);
			Assert.That(t.Length, Is.EqualTo(2));

			ctxt.CurrentContext.Set((mod["N"][0] as DClassLike)["foo"][0] as DMethod);
			t = TypeDeclarationResolver.ResolveIdentifier("X",ctxt,null);
			Assert.That(t.Length, Is.EqualTo(1));
			Assert.That(t[0], Is.TypeOf(typeof(ClassType)));

			mod = pcl[0]["modF"];
			var f = mod["asdf"][0] as DMethod;
			ctxt.CurrentContext.Set(f, ((f.Body.SubStatements[0] as IfStatement).ThenStatement as BlockStatement).SubStatements[1]);
			t = TypeDeclarationResolver.ResolveIdentifier("ni", ctxt, null);
			Assert.That(t.Length, Is.EqualTo(2));

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
			
			ctxt.CurrentContext.Set(modA["foo"][0] as IBlockNode);
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
			var foo = pcl[0]["A"]["foo"][0] as DMethod;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body);
			
			var t = Evaluation.EvaluateType((foo.Body.SubStatements[1] as ExpressionStatement).Expression, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.InstanceOf(typeof(PointerType)));
			
			t = Evaluation.EvaluateType((foo.Body.SubStatements[2] as ExpressionStatement).Expression, ctxt);
			Assert.That(t, Is.Null);
			
			t = Evaluation.EvaluateType((foo.Body.SubStatements[3] as ExpressionStatement).Expression, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.InstanceOf(typeof(PrimitiveType)));
			
			t = Evaluation.EvaluateType((foo.Body.SubStatements[4] as ExpressionStatement).Expression, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));
			
			t = Evaluation.EvaluateType((foo.Body.SubStatements[5] as ExpressionStatement).Expression, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));
			
			t = Evaluation.EvaluateType((foo.Body.SubStatements[6] as ExpressionStatement).Expression, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));
			
			t = Evaluation.EvaluateType((foo.Body.SubStatements[7] as ExpressionStatement).Expression, ctxt);
			Assert.That(t, Is.Null);
			
			ctxt.CurrentContext.Set(pcl[0]["B"]);
			
			var ex = DParser.ParseExpression("inst.b");
			t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.Null);
			
			ex = DParser.ParseExpression("inst.c");
			t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.Null);
			
			ctxt.CurrentContext.Set((pcl[0]["A"]["cl"][0] as DClassLike)["bar"][0] as DMethod);
			
			ex = DParser.ParseExpression("statVar");
			t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));
			
			ex = DParser.ParseExpression("a");
			t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.Null);
			
			ex = DParser.ParseExpression("b");
			t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.Null);
			
			ex = DParser.ParseExpression("c");
			t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.Null);
			
			ex = DParser.ParseExpression("statBase");
			t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));
			
			ex = DParser.ParseExpression("baseA");
			t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.Null);
			
			ex = DParser.ParseExpression("otherClass");
			t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(ClassType)));
			
			ex = DParser.ParseExpression("globalVar");
			t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(MemberSymbol)));

			ex = DParser.ParseExpression("enumSym");
			t = Evaluation.EvaluateType(ex, ctxt);
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
			var t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(StructType)));
			
			ex = DParser.ParseExpression("IntThing");
			t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(AliasedType)));
			t = DResolver.StripAliasSymbol(t);
			Assert.That(t, Is.TypeOf(typeof(StructType)));
			
			ex = DParser.ParseExpression("new Thing!int");
			t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol))); // Returns the ctor
			Assert.That(((DSymbol)t).Name, Is.EqualTo(DMethod.ConstructorIdentifier));
			
			ex = DParser.ParseExpression("new IntThing");
			t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That(((DSymbol)t).Name, Is.EqualTo(DMethod.ConstructorIdentifier));
		}
		
		[Test]
		public void BasicResolution3()
		{
			var pcl = CreateCache(@"module A;
class Blupp : Blah!(Blupp) {}
class Blah(T){ T b; }");
			
			var ctxt = CreateDefCtxt(pcl, pcl[0]["A"]);
			
			var ex = DParser.ParseExpression("Blah!Blupp");
			var t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(ClassType)));
		}

		[Test]
		public void BasicResolution4()
		{
			var pcl = CreateCache(@"module modA;");
			var ctxt = CreateDefCtxt(pcl, pcl[0]["modA"]);

			var ts = TypeDeclarationResolver.Resolve(new IdentifierDeclaration("string"), ctxt);
			Assert.That(ts, Is.Not.Null);
			Assert.That(ts.Length, Is.EqualTo(1));

			var x = DParser.ParseExpression(@"(new Object).toString()");
			var t = Evaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(AliasedType)));
			t = DResolver.StripAliasSymbol(t);
			Assert.That(t, Is.TypeOf(typeof(ArrayType)));
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
			var foo = A["foo"][0] as DMethod;
			var case1 = ((foo.Body.SubStatements[1] as SwitchStatement).ScopedStatement as BlockStatement).SubStatements[1] as SwitchStatement.CaseStatement;
			var colStmt = case1.SubStatements[1] as ExpressionStatement;
			
			var ctxt = CreateDefCtxt(pcl, foo, colStmt);
			
			var t = Evaluation.EvaluateType(colStmt.Expression, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
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
			var t = Evaluation.EvaluateType(ex, ctxt);
			
			Assert.That(t, Is.TypeOf(typeof(ArrayAccessSymbol)));
			Assert.That((t as ArrayAccessSymbol).Base, Is.TypeOf(typeof(ClassType)));
			
			ex = DParser.ParseExpression("arr[0].myProp");
			t = Evaluation.EvaluateType(ex, ctxt);
			
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			
			ex = DParser.ParseExpression("o.myProp");
			t = Evaluation.EvaluateType(ex, ctxt);
			
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
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

			var A = pcl[0]["modA"]["A"][0] as DClassLike;
			var bar = A["bar"][0] as DMethod;
			var call_fooC = bar.Body.SubStatements[0];

			Assert.IsInstanceOfType(typeof(ExpressionStatement),call_fooC);

			var ctxt = CreateDefCtxt(pcl, bar, call_fooC);

			var call = ((ExpressionStatement)call_fooC).Expression;
			var methodName = ((PostfixExpression_MethodCall)call).PostfixForeExpression;

			var res=Evaluation.EvaluateType(methodName,ctxt);

			Assert.IsTrue(res!=null , "Resolve() returned no result!");
			Assert.IsInstanceOfType(typeof(MemberSymbol),res);

			var mr = (MemberSymbol)res;

			Assert.IsInstanceOfType(typeof(DMethod),mr.Definition);
			Assert.AreEqual(mr.Name, "fooC");
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

			var res = Evaluation.EvaluateType(instanceExpr, ctxt);

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
			var bt = Evaluation.EvaluateType(call, ctxt);
			
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
			var A = pcl[0]["modA"]["A"][0] as DClassLike;
			var bar = A["bar"][0] as DMethod;
			var ctxt = CreateDefCtxt(pcl, bar, bar.Body);

			var e = DParser.ParseExpression("123.foo");

			var t = Evaluation.EvaluateType(e, ctxt);

			Assert.IsInstanceOfType(typeof(MemberSymbol),t);
			Assert.AreEqual(pcl[0]["modA"]["foo"][0], ((MemberSymbol)t).Definition);
		}

		[Test]
		public void TestParamDeduction4()
		{
			var pcl = CreateCache(@"module modA;

void fo(T:U[], U)(T o) {}
void f(T:U[n], U,int n)(T o) {}

char[5] arr;

double foo(T)(T a) {}

int delegate(int b) myDeleg;

");

			var ctxt = CreateDefCtxt(pcl, pcl[0]["modA"]);
			ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			var x = DParser.ParseExpression("f!(char[5])");
			var r=Evaluation.EvaluateType(x, ctxt);
			var mr = r as MemberSymbol;
			Assert.IsNotNull(mr);

			var v = mr.DeducedTypes[2].ParameterValue;
			Assert.IsInstanceOfType(typeof(PrimitiveValue),v);
			Assert.AreEqual(5M, ((PrimitiveValue)v).Value);

			x = DParser.ParseExpression("fo!(char[5])");
			r = Evaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsNotNull(mr);

			x = DParser.ParseExpression("fo!(immutable(char)[])");
			r = Evaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsNotNull(mr);

			x = DParser.ParseExpression("myDeleg");
			r = Evaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsNotNull(mr);
			Assert.IsInstanceOfType(typeof(DelegateType), mr.Base);

			x=DParser.ParseExpression("myDeleg(123)");
			r = Evaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsNotNull(mr);
			Assert.That(mr.Base, Is.TypeOf(typeof(DelegateType)));

			x = DParser.ParseExpression("foo(myDeleg(123))");
			r = Evaluation.EvaluateType(x, ctxt);
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
			var Client = mod["Client"][0] as DClassLike;
			var ctxt = CreateDefCtxt(pcl, mod);

			var res = TypeDeclarationResolver.HandleNodeMatch(Client, ctxt);
			Assert.IsInstanceOfType(typeof(ClassType),res);
			var ct = (ClassType)res;

			Assert.IsInstanceOfType( typeof(ClassType),ct.Base);
			ct = (ClassType)ct.Base;

			Assert.AreEqual(ct.DeducedTypes.Count, 2);
			var dedtype = ct.DeducedTypes[0];
			Assert.AreEqual("P", dedtype.Name);
			Assert.AreEqual(mod["Params"][0],((DSymbol)dedtype.Base).Definition);
			dedtype = ct.DeducedTypes[1];
			Assert.AreEqual("R", dedtype.Name);
			Assert.AreEqual(mod["ConcreteRegistry"][0], ((DSymbol)dedtype.Base).Definition);


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

			var res = TypeDeclarationResolver.HandleNodeMatch(mod["D"][0], ctxt);
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
			var t = Evaluation.EvaluateType(ex,ctxt);
			Assert.That(t, Is.TypeOf(typeof(DelegateType)));
			var dt = (DelegateType)t;
			Assert.That(dt.Base, Is.Not.Null);
			Assert.That(dt.Parameters, Is.Not.Null);
			
			ex = DParser.ParseExpression("genA!int()");
			t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
		}
		
		[Test]
		public void TestParamDeduction8()
		{
			var pcl = CreateCache(@"module A;
struct Appender(A:E[],E) { A data; }

Appender!(E[]) appender(A : E[], E)(A array = null)
{
    return Appender!(E[])(array);
}");
			
			var A = pcl[0]["A"];
			var ctxt = CreateDefCtxt(pcl, A);
			
			var ex = DParser.ParseExpression("new Appender!(double[])()");
			var t = Evaluation.EvaluateType(ex,ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol))); // ctor
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(StructType)));
			
			ex = DParser.ParseExpression("appender!(double[])()");
			t = Evaluation.EvaluateType(ex,ctxt);
			Assert.That(t, Is.TypeOf(typeof(StructType)));
			var ss = t as StructType;
			Assert.That(ss.DeducedTypes.Count, Is.GreaterThan(0));
		}
		
		[Test]
		public void TestParamDeduction9()
		{
			var pcl = Tests.ResolutionTests.CreateCache(@"module A;
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

mixin(def!(-1,""bar""));
");
			var A = pcl[0]["A"];
			var ctxt = Tests.ResolutionTests.CreateDefCtxt(pcl, A);
			
			var ex = DParser.ParseExpression(@"def!(2,""someVar"")");
			var val = Evaluation.EvaluateValue(ex, ctxt);
			Assert.That(val, Is.TypeOf(typeof(ArrayValue)));
			Assert.That((val as ArrayValue).IsString,Is.True);
			Assert.That((val as ArrayValue).StringValue, Is.EqualTo("int someVar;"));
			
			ex = DParser.ParseExpression(@"def!(-5,""foolish"")");
			val = Evaluation.EvaluateValue(ex, ctxt);
			Assert.That(val, Is.TypeOf(typeof(ArrayValue)));
			Assert.That((val as ArrayValue).IsString,Is.True);
			Assert.That((val as ArrayValue).StringValue, Is.EqualTo("bool foolish;"));
			
			ex=DParser.ParseExpression("bar");
			var t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base,Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(((t as MemberSymbol).Base as PrimitiveType).TypeToken,Is.EqualTo(DTokens.Bool));
		}

		[Test]
		public void TestParamDeduction10()
		{
			var pcl = CreateCache(@"module modA;

void foo(T)(int a) {}
void foo2(T=double)(bool b) {}
V foo3(V)(int a,V v) {}");

			var ctxt = CreateDefCtxt(pcl, pcl[0]["modA"]);
			ctxt.ContextIndependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			var x = DParser.ParseExpression("foo(123)");
			var t = Evaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.Null);

			x = DParser.ParseExpression("foo2(true)");
			t = Evaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			var ms = t as MemberSymbol;
			Assert.That(ms.DeducedTypes, Is.Not.Null);
			Assert.That(ms.DeducedTypes[0].Base, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("foo3(123,\"asdf\")");
			t = Evaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			ms = t as MemberSymbol;
			var tps = ms.Base as TemplateParameterSymbol;
			Assert.That(tps, Is.Not.Null);
			Assert.That(tps.Base, Is.TypeOf(typeof(ArrayType)));
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
			var t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.InstanceOfType(typeof(StructType)));

			ex = DParser.ParseExpression("appender!string()");
			t = Evaluation.EvaluateType(ex, ctxt);
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
			Assert.That(t, Is.InstanceOfType(typeof(PrimitiveValue)));
			Assert.That((t as PrimitiveValue).Value == 1m);

			ex = ex = DParser.ParseExpression("Tmpl!int");
			t = Evaluation.EvaluateValue(ex, ctxt);
			Assert.That(t, Is.InstanceOfType(typeof(PrimitiveValue)));
			Assert.That((t as PrimitiveValue).Value == 0m);

			ctxt.CurrentContext.Set(modA["tt"][0] as IBlockNode);
			ex = DParser.ParseExpression("Tmpl!U");
			t = Evaluation.EvaluateValue(ex, ctxt);
			Assert.That(t, Is.InstanceOfType(typeof(PrimitiveValue)));
			Assert.That((t as PrimitiveValue).Value == 1m);
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
			var t = Evaluation.EvaluateType(x,ctxt);
			Assert.That(t, Is.TypeOf(typeof(TemplateType)));
			var tps = (t as TemplateType).DeducedTypes[0] as TemplateParameterSymbol;
			Assert.That(tps, Is.Not.Null);
			Assert.That(tps.Base, Is.TypeOf(typeof(DTuple)));
			var tt = tps.Base as DTuple;
			Assert.That(tt.Items.Length, Is.EqualTo(3));
			Assert.That(tt.IsExpressionTuple);

			ctxt.ContextIndependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			x = DParser.ParseExpression("Write!(int, char, double).write(1, 'a', 6.8)");
			t = Evaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));

			x = DParser.ParseExpression("tplWrite!(int, char, double)(1, 'a', 6.8)");
			t = Evaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));

			x = DParser.ParseExpression("tplWrite(1, 'a', 6.8)");
			t = Evaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			tps = (t as MemberSymbol).DeducedTypes[0] as TemplateParameterSymbol;
			Assert.That(tps, Is.Not.Null);
			Assert.That(tps.Base, Is.TypeOf(typeof(DTuple)));
			tt = tps.Base as DTuple;
			Assert.That(tt.Items.Length, Is.EqualTo(3));
			Assert.That(tt.IsTypeTuple);

			x = DParser.ParseExpression("tplWrite2(\"asdf\", 'a', 6.8)");
			t = Evaluation.EvaluateType(x, ctxt);
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
			var t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.Null);
			
			ex = DParser.ParseExpression("Too!int");
			t = Evaluation.EvaluateType(ex, ctxt);
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
			
			var foo = pcl[0]["A"]["foo"][0] as DMethod;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body);
			
			var ex = (foo.Body.SubStatements[0] as ExpressionStatement).Expression;
			var t = Evaluation.GetOverloads(ex  as TemplateInstanceExpression, ctxt, null, true);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Length, Is.EqualTo(1));
			
			var t_ = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t_ , Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t_ as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			
			ex = (foo.Body.SubStatements[1] as ExpressionStatement).Expression;
			t = Evaluation.GetOverloads(ex  as TemplateInstanceExpression, ctxt, null, true);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Length, Is.EqualTo(1));
			
			t_ = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t_ , Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t_ as MemberSymbol).Base, Is.TypeOf(typeof(ArrayType)));
			
			ex = (foo.Body.SubStatements[2] as ExpressionStatement).Expression;
			t = Evaluation.GetOverloads((ex as PostfixExpression_MethodCall).PostfixForeExpression as TemplateInstanceExpression, ctxt, null, true);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Length, Is.EqualTo(1));
			Assert.That(t[0], Is.TypeOf(typeof(MemberSymbol)));
			
			t_ = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t_, Is.TypeOf(typeof(PointerType)));
			
			ex = (foo.Body.SubStatements[3] as ExpressionStatement).Expression;
			t_ = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t_, Is.TypeOf(typeof(ArrayType)));

			ex = (foo.Body.SubStatements[4] as ExpressionStatement).Expression;
			t_ = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t_, Is.TypeOf(typeof(ArrayType)));
		}
		
		[Test]
		public void EmptyTypeTuple()
		{
			var pcl = CreateCache(@"module A;
void writeln(T...)(T t)
{
}");
			var ctxt = CreateDefCtxt(pcl, pcl[0]["A"]);
			
			var ex = DParser.ParseExpression("writeln()");
			var x = Evaluation.EvaluateType(ex, ctxt);
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
			var foo = pcl[0]["A"]["foo"][0] as DMethod;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body);
			
			var ex = DParser.ParseExpression("bar!int");
			var t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			var ms = t as MemberSymbol;
			Assert.That(ms.Base, Is.TypeOf(typeof(ArrayType)));
			
			ex = DParser.ParseExpression("bar");
			t = Evaluation.EvaluateType(ex, ctxt);
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

			var B = pcl[0]["modA"]["B"][0] as DClassLike;
			var this_ = (DMethod)B[DMethod.ConstructorIdentifier][0];
			var ctxt = CreateDefCtxt(pcl, this_);

			var super = (this_.Body.SubStatements[0] as IExpressionContainingStatement).SubExpressions[0] as PostfixExpression_MethodCall;

			var t = Evaluation.EvaluateType(super.PostfixForeExpression, ctxt);
			Assert.That(t, Is.TypeOf(typeof(ClassType)));
			
			t = Evaluation.EvaluateType(super, ctxt);
			Assert.IsNull(t);
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

			Assert.IsInstanceOfType(typeof(PointerType),m.Base);

			ms = TypeDeclarationResolver.ResolveIdentifier("d", ctxt, null);
			Assert.AreEqual(1, ms.Length);
			m = ms[0] as MemberSymbol;
			Assert.IsNotNull(m);

			Assert.IsInstanceOfType(typeof(PointerType),m.Base);

			ms = TypeDeclarationResolver.ResolveIdentifier("a", ctxt, null);
			Assert.AreEqual(1, ms.Length);
			m = ms[0] as MemberSymbol;
			Assert.IsNotNull(m);

			Assert.IsInstanceOfType(typeof(PointerType),m.Base);

			ms = TypeDeclarationResolver.ResolveIdentifier("pubB", ctxt, null);
			Assert.AreEqual(1, ms.Length);

			ms = TypeDeclarationResolver.ResolveIdentifier("pubC", ctxt, null);
			Assert.AreEqual(0, ms.Length);
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


debug = C

debug
	int dbg_a;

debug(C)
	int dbg_b;
else
	int dbg_c;

debug = 3;

debug(2)
	int dbg_d;

debug(3)
	int dbg_e;

debug(4)
	int dbg_f;

");

			var m = pcl[0]["m"];
			var A = m["A"][0] as DClassLike;
			var foo = A["foo"][0] as DMethod;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body.SubStatements[0]);

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
			ctxt.CurrentContext.Set(ss=((foo.Body.SubStatements[2] as StatementCondition).ScopedStatement as BlockStatement).SubStatements[0]);

			var x2 = Evaluation.EvaluateType(((ExpressionStatement)ss).Expression, ctxt);
			Assert.That(x2, Is.TypeOf(typeof(MemberSymbol)));

			ctxt.CurrentContext.Set(ss = foo.Body.SubStatements[4]);
			x2 = Evaluation.EvaluateType(((ExpressionStatement)ss).Expression, ctxt);
			Assert.IsNull(x2);

			ctxt.CurrentContext.Set(ss =  foo.Body.SubStatements[5]);
			x2 = Evaluation.EvaluateType(((ExpressionStatement)ss).Expression, ctxt);
			Assert.IsNull(x2);

			ctxt.CurrentContext.Set(ss = foo.Body.SubStatements[6]);
			x2 = Evaluation.EvaluateType(((ExpressionStatement)ss).Expression, ctxt);
			Assert.IsNotNull(x2);

			x = TypeDeclarationResolver.ResolveIdentifier("dbg_a", ctxt, null);
			Assert.AreEqual(1, x.Length);

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
			var ctxt = CreateDefCtxt(pcl, pcl[0]["m"]);

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

			ctxt.CurrentContext.Set(pcl[0]["B"]);

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

			var main = pcl[0]["B"]["main"][0] as DMethod;
			var body = main.Body;
			ctxt.PushNewScope(main, body);

			var ss = body.SubStatements[0] as ExpressionStatement;
			t = Evaluation.EvaluateType(ss.Expression, ctxt);
			Assert.IsNotNull(t);

			ss = body.SubStatements[1] as ExpressionStatement;
			t = Evaluation.EvaluateType(ss.Expression, ctxt);
			Assert.IsNull(t);

			ss = body.SubStatements[2] as ExpressionStatement;
			t = Evaluation.EvaluateType(ss.Expression, ctxt);
			Assert.IsNotNull(t);

			ss = body.SubStatements[3] as ExpressionStatement;
			t = Evaluation.EvaluateType(ss.Expression, ctxt);
			Assert.IsNull(t);
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
			var ctxt = CreateDefCtxt(pcl, B["bar"][0] as DMethod);
			
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
			
			var x = TypeDeclarationResolver.Resolve(new IdentifierDeclaration("cl"),ctxt,null,true);
			Assert.That(x, Is.Null);
			
			var ex = DParser.ParseAssignExpression("cl!int");
			x = Evaluation.EvaluateTypes(ex, ctxt);
			Assert.That(x.Length, Is.EqualTo(1));
			
			ex = DParser.ParseAssignExpression("cl!float");
			x = Evaluation.EvaluateTypes(ex, ctxt);
			Assert.That(x, Is.Null);
			
			ex = DParser.ParseAssignExpression("aa!float");
			x = Evaluation.EvaluateTypes(ex, ctxt);
			Assert.That(x.Length, Is.EqualTo(1));
			var t = x[0] as ClassType;
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Definition, Is.EqualTo(A["aa"][0]));
			
			ex = DParser.ParseAssignExpression("aa!int");
			x = Evaluation.EvaluateTypes(ex, ctxt);
			Assert.That(x.Length, Is.EqualTo(1));
			t = x[0] as ClassType;
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Definition, Is.EqualTo(A["aa"][1]));
			
			ex = DParser.ParseAssignExpression("aa!string");
			x = Evaluation.EvaluateTypes(ex, ctxt);
			Assert.That(x, Is.Null);
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
			var main = A["main"][0] as DMethod;
			var stmt = main.Body.SubStatements[1];
			var ctxt = ResolutionTests.CreateDefCtxt(pcl, main, stmt);
			
			var x = TypeDeclarationResolver.ResolveIdentifier("x", ctxt, stmt);
			Assert.That(x.Length, Is.EqualTo(1));
			
			x = TypeDeclarationResolver.ResolveIdentifier("y", ctxt, stmt);
			Assert.That(x.Length, Is.EqualTo(0));
		}
		
		[Test]
		public void Mixins3()
		{
			var pcl = ResolutionTests.CreateCache(@"module A;
template Temp(string v)
{
	mixin(v);
}

class cl
{
	mixin(""int someInt=345;"");
}");
			var A =pcl[0]["A"];
			var ctxt = ResolutionTests.CreateDefCtxt(pcl, A);
			
			var ex = DParser.ParseExpression("Temp!\"int Temp;\"");
			var x = Evaluation.EvaluateType(ex,ctxt);
			Assert.That(x, Is.InstanceOf(typeof(MemberSymbol)));
			
			ex = DParser.ParseExpression("(new cl()).someInt");
			x = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.InstanceOf(typeof(MemberSymbol)));
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
			
			var bar = (B["cl"][0] as DClassLike)["bar"][0] as DMethod;
			ctxt.CurrentContext.Set(bar, bar.Body);
			
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
			var x = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.InstanceOf(typeof(MemberSymbol)));
			Assert.That((x as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			
			ex = DParser.ParseExpression("myFoo");
			x = Evaluation.EvaluateType(ex,ctxt);
			Assert.That(x, Is.InstanceOf(typeof(MemberSymbol)));
			var ms = x as MemberSymbol;
			Assert.That(ms.Base, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((ms.Base as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			
			ex = DParser.ParseExpression("myMx.someProp;");
			x = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.InstanceOf(typeof(MemberSymbol)));
			Assert.That((x as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			
			ex = DParser.ParseExpression("myTempMx.myFoo");
			x = Evaluation.EvaluateType(ex,ctxt);
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
			var x = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.InstanceOf(typeof(MemberSymbol)));
			Assert.That((x as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			
			ex = DParser.ParseExpression("(new Bar()).func");
			x = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.InstanceOf(typeof(MemberSymbol)));
			Assert.That((x as MemberSymbol).Base, Is.TypeOf(typeof(ArrayType)));
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
			var foo = A["foo"][0] as DMethod;
			var ctxt = ResolutionTests.CreateDefCtxt(pcl, foo, foo.Body);
			
			var t = Evaluation.EvaluateType((foo.Body.SubStatements[0] as ExpressionStatement).Expression,ctxt);
			Assert.That(t, Is.Null);
			
			t = Evaluation.EvaluateType((foo.Body.SubStatements[2] as ExpressionStatement).Expression,ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			var ms = t as MemberSymbol;
			Assert.That(ms.Base, Is.TypeOf(typeof(PrimitiveType)));
			
			t = Evaluation.EvaluateType((foo.Body.SubStatements[3] as ExpressionStatement).Expression,ctxt);
			Assert.That(t, Is.TypeOf(typeof(ArrayAccessSymbol)));
			t = (t as ArrayAccessSymbol).Base;
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			
			var ex = DParser.ParseExpression("clA.getInstance");
			t = Evaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			
			foo = (A["Singleton"][0] as DClassLike)["singletonBar"][0] as DMethod;
			ctxt.CurrentContext.Set(foo, foo.Body);
			t = TypeDeclarationResolver.ResolveSingle("I",ctxt,null);
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			
			foo = (A["clA"][0] as DClassLike)["clFoo"][0] as DMethod;
			ctxt.CurrentContext.Set(foo, foo.Body);
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
			
			var bar = (B["cl"][0] as DClassLike)["bar"][0] as DMethod;
			ctxt.CurrentContext.Set(bar, bar.Body);
			
			t = TypeDeclarationResolver.ResolveSingle("CFoo", ctxt, null);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Definition, Is.TypeOf(typeof(DMethod)));
		}
		#endregion
	}
}
