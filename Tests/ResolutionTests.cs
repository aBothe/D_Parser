using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D_Parser.Parser;
using D_Parser.Misc;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.Templates;

namespace Tests
{
	[TestFixture]
	public class ResolutionTests
	{
		public static IAbstractSyntaxTree objMod = DParser.ParseString(@"module object;
						alias immutable(char)[] string;
						alias immutable(wchar)[] wstring;
						alias immutable(dchar)[] dstring;
						class Object {}");

		public static ParseCacheList CreateCache(params string[] moduleCodes)
		{
			var pcl = new ParseCacheList();
			var pc = new ParseCache();
			pcl.Add(pc);

			pc.AddOrUpdate(objMod);

			foreach (var code in moduleCodes)
				pc.AddOrUpdate(DParser.ParseString(code));

			pc.UfcsCache.Update(pcl, null, pc);

			return pcl;
		}

		public static ResolutionContext CreateDefCtxt(ParseCacheList pcl, IBlockNode scope, IStatement stmt=null)
		{
			var r = ResolutionContext.Create(pcl, new ConditionalCompilationFlags(new[]{"Windows","all"},1,true,null,0), scope, stmt);
			return r;
		}

		[Test]
		public void BasicResolution()
		{
			var pcl = CreateCache(@"module modA;

class foo {}");

			var ctxt = CreateDefCtxt(pcl, pcl[0]["modA"]);

			var id = new IdentifierDeclaration("foo");

			var foo = TypeDeclarationResolver.ResolveSingle(id, ctxt);

			Assert.IsInstanceOfType(typeof(ClassType),foo);
			var ct = (ClassType)foo;

			Assert.AreEqual("foo", ct.Name);
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

void foo(T)(T a) {}

int delegate(int b) myDeleg;

");

			var ctxt = CreateDefCtxt(pcl, pcl[0]["modA"]);
			ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			var x = DParser.ParseExpression("f!(char[5])");
			var r=Evaluation.EvaluateType(x, ctxt);
			var mr = r as MemberSymbol;
			Assert.IsNotNull(mr);

			var v = mr.DeducedTypes[2].Value.ParameterValue;
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
			var t = mr.Base;
			Assert.IsInstanceOfType(typeof(DelegateType),t);

			x=DParser.ParseExpression("myDeleg(123)");
			r = Evaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsNotNull(mr);
			t = mr.Base;
			Assert.IsInstanceOfType(typeof(DelegateType),t);

			x = DParser.ParseExpression("foo(myDeleg(123))");
			r = Evaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(typeof(MemberSymbol),r);
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
			Assert.AreEqual("P", dedtype.Key);
			Assert.AreEqual(mod["Params"][0],((DSymbol)dedtype.Value.Base).Definition);
			dedtype = ct.DeducedTypes[1];
			Assert.AreEqual("R", dedtype.Key);
			Assert.AreEqual(mod["ConcreteRegistry"][0], ((DSymbol)dedtype.Value.Base).Definition);


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
			ctxt.ContextIndependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			var super = (this_.Body.SubStatements[0] as IExpressionContainingStatement).SubExpressions[0];

			var sym = Evaluation.EvaluateType(super, ctxt);
			Assert.IsInstanceOfType(typeof(MemberSymbol),sym);
			var mr = (MemberSymbol)sym;
			Assert.IsInstanceOfType( typeof(DMethod),mr.Definition);
			Assert.AreEqual(DMethod.MethodType.Constructor, ((DMethod)mr.Definition).SpecialType);
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
			
			var v = Evaluation.EvaluateValue(DParser.ParseExpression("Templ!int"), ctxt);
			Assert.That(v, Is.InstanceOf(typeof(VariableValue)));
			v = Evaluation.EvaluateValue(((VariableValue)v).Variable.Initializer, ctxt);
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
}");
			var A =pcl[0]["A"];
			var ctxt = ResolutionTests.CreateDefCtxt(pcl, A);
			
			var ex = DParser.ParseExpression("Temp!\"int Temp;\"");
			var x = Evaluation.EvaluateType(ex,ctxt);
			Assert.That(x, Is.InstanceOf(typeof(MemberSymbol)));
		}
	}
}
