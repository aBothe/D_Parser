using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using D_Parser.Parser;
using D_Parser.Misc;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.Templates;

namespace D_Parser.Unittest
{
	[TestClass]
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

			pc.UfcsCache.Update(pcl, pc);

			return pcl;
		}

		[TestMethod]
		public void BasicResolution()
		{
			var pcl = CreateCache(@"module modA;

class foo {}");

			var ctxt = new ResolverContextStack(pcl, new ResolverContext { ScopedBlock = pcl[0]["modA"]["foo"][0] as IBlockNode });

			var id = new IdentifierDeclaration("foo");

			var foo = TypeDeclarationResolver.ResolveSingle(id, ctxt);

			Assert.IsInstanceOfType(foo, typeof(ClassType));
			var ct = (ClassType)foo;

			Assert.AreEqual("foo", ct.Name);
		}

		[TestMethod]
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

			Assert.IsInstanceOfType(call_fooC, typeof(ExpressionStatement));

			var ctxt=new ResolverContextStack(pcl, new ResolverContext{ ScopedBlock=bar, ScopedStatement=call_fooC });

			var call = ((ExpressionStatement)call_fooC).Expression;
			var methodName = ((PostfixExpression_MethodCall)call).PostfixForeExpression;

			var res=Evaluation.EvaluateType(methodName,ctxt);

			Assert.IsTrue(res!=null , "Resolve() returned no result!");
			Assert.IsInstanceOfType(res,typeof(MemberSymbol));

			var mr = (MemberSymbol)res;

			Assert.IsInstanceOfType(mr.Definition, typeof(DMethod));
			Assert.AreEqual(mr.Name, "fooC");
		}

		[TestMethod]
		public void TestMethodParamDeduction1()
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

			var ctxt = new ResolverContextStack(pcl, new ResolverContext {
				ScopedBlock = pcl[0]["modA"]
			});

			var instanceExpr = DParser.ParseExpression("(new MyClass!int).tvar");

			Assert.IsInstanceOfType(instanceExpr, typeof(PostfixExpression_Access));

			var res = Evaluation.EvaluateType(instanceExpr, ctxt);

			Assert.IsInstanceOfType(res,typeof(MemberSymbol));
			var mr = (MemberSymbol)res;

			Assert.IsInstanceOfType(mr.Base, typeof(TemplateParameterSymbol));
			var tps = (TemplateParameterSymbol)mr.Base;
			Assert.IsInstanceOfType(tps.Base, typeof(PrimitiveType));
			var sr = (PrimitiveType)tps.Base;

			Assert.AreEqual(sr.TypeToken, DTokens.Int);
		}

		[TestMethod]
		public void TestMethodParamDeduction2()
		{
			var pcl = CreateCache(@"
module modA;
T foo(T)() {}
");

			var ctxt = new ResolverContextStack(pcl, new ResolverContext { ScopedBlock=pcl[0]["modA"] });

			var call = DParser.ParseExpression("foo!int()");
			var bt = Evaluation.EvaluateType(call, ctxt);

			Assert.IsInstanceOfType(bt, typeof(TemplateParameterSymbol));
			var tps = (TemplateParameterSymbol)bt;
			Assert.IsInstanceOfType(tps.Base, typeof(PrimitiveType), "Resolution returned empty result instead of 'int'");
			var st = (PrimitiveType)tps.Base;
			Assert.IsNotNull(st, "Result must be Static type int");
			Assert.AreEqual(st.TypeToken, DTokens.Int, "Static type must be int");
		}

		[TestMethod]
		public void TestMethodParamDeduction3()
		{
			var pcl = CreateCache(@"module modA;

class A {}
class A2 {}

class B(T){
	class C(T2) : T {} 
}");

			var ctxt = new ResolverContextStack(pcl, new ResolverContext { ScopedBlock=pcl[0]["modA"] });

			var inst = DParser.ParseExpression("(new B!A).new C!A2"); // TODO
		}

		[TestMethod]
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
			var ctxt = new ResolverContextStack(pcl, new ResolverContext { ScopedBlock = bar, ScopedStatement=bar.Body });

			var e = DParser.ParseExpression("123.foo");

			var t = Evaluation.EvaluateType(e, ctxt);

			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			Assert.AreEqual(pcl[0]["modA"]["foo"][0], ((MemberSymbol)t).Definition);
		}

		[TestMethod]
		public void TestMethodParamDeduction4()
		{
			var pcl = CreateCache(@"module modA;

void fo(T:U[], U)(T o) {}
void f(T:U[n], U,int n)(T o) {}

char[5] arr;

void foo(T)(T a) {}

int delegate(int b) myDeleg;

");

			var ctxt=new ResolverContextStack(pcl, new ResolverContext{ ScopedBlock=pcl[0]["modA"] });
			ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			var x = DParser.ParseExpression("f!(char[5])");
			var r=Evaluation.EvaluateType(x, ctxt);
			var mr = r as MemberSymbol;
			Assert.IsNotNull(mr);

			var v = mr.DeducedTypes[2].Value.ParameterValue;
			Assert.IsInstanceOfType(v, typeof(PrimitiveValue));
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
			Assert.IsInstanceOfType(t, typeof(DelegateType));

			x=DParser.ParseExpression("myDeleg(123)");
			r = Evaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsNotNull(mr);
			t = mr.Base;
			Assert.IsInstanceOfType(t, typeof(DelegateType));

			x = DParser.ParseExpression("foo(myDeleg(123))");
			r = Evaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(r, typeof(MemberSymbol));
		}

		[TestMethod]
		public void TestMethodParamDeduction5()
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
			var ctxt = new ResolverContextStack(pcl, new ResolverContext {  ScopedBlock = mod });

			var res = TypeDeclarationResolver.HandleNodeMatch(Client, ctxt);
			Assert.IsInstanceOfType(res, typeof(ClassType));
			var ct = (ClassType)res;

			Assert.IsInstanceOfType(ct.Base, typeof(ClassType));
			ct = (ClassType)ct.Base;

			Assert.AreEqual(ct.DeducedTypes.Count, 2);
			var dedtype = ct.DeducedTypes[0];
			Assert.AreEqual("P", dedtype.Key);
			Assert.AreEqual(mod["Params"][0],((DSymbol)dedtype.Value.Base).Definition);
			dedtype = ct.DeducedTypes[1];
			Assert.AreEqual("R", dedtype.Key);
			Assert.AreEqual(mod["ConcreteRegistry"][0], ((DSymbol)dedtype.Value.Base).Definition);


			ctxt.ScopedBlock = mod;
			DToken opt=null;
			var tix = DParser.ParseBasicType("IClient!(Params,ConcreteRegistry)",out opt);
			res = TypeDeclarationResolver.ResolveSingle(tix, ctxt);

			Assert.IsInstanceOfType(res, typeof(ClassType));
		}

		[TestMethod]
		public void TestMethodParamDeduction6()
		{
			var pcl = CreateCache(@"module modA;
class A(T) {}
class B : A!int{}
class C(U: A!int){}
class D : C!B {}");

			var mod = pcl[0]["modA"];
			var ctxt = new ResolverContextStack(pcl, new ResolverContext { ScopedBlock = mod });

			var res = TypeDeclarationResolver.HandleNodeMatch(mod["D"][0], ctxt);
			Assert.IsInstanceOfType(res, typeof(ClassType));
			var ct = (ClassType)res;

			Assert.IsInstanceOfType(ct.Base, typeof(ClassType));
			ct = (ClassType)ct.Base;

			Assert.AreEqual(1, ct.DeducedTypes.Count);
		}

		[TestMethod]
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
			var ctxt = new ResolverContextStack(pcl, new ResolverContext { ScopedBlock = this_, ContextDependentOptions = ResolutionOptions.ReturnMethodReferencesOnly });

			var super = (this_.Body.SubStatements[0] as IExpressionContainingStatement).SubExpressions[0];

			var sym = Evaluation.EvaluateType(super, ctxt);
			Assert.IsInstanceOfType(sym, typeof(MemberSymbol));
			var mr = (MemberSymbol)sym;
			Assert.IsInstanceOfType(mr.Definition, typeof(DMethod));
			Assert.AreEqual(DMethod.MethodType.Constructor, ((DMethod)mr.Definition).SpecialType);
		}

		[TestMethod]
		public void TemplateAliasing()
		{
			var pcl = CreateCache(@"module m;
template Foo(A)
{
	A Foo;
}");

			var ctxt = new ResolverContextStack(pcl, new ResolverContext { ScopedBlock=pcl[0]["m"] });

			DToken tk;
			var td = DParser.ParseBasicType("Foo!int",out tk);

			var s = TypeDeclarationResolver.ResolveSingle(td, ctxt);

			Assert.IsInstanceOfType(s, typeof(MemberSymbol));

			var ms = (MemberSymbol)s;
			Assert.IsInstanceOfType(ms.Definition, typeof(DVariable));
			Assert.IsInstanceOfType(ms.Base, typeof(TemplateParameterSymbol));
			var tps = (TemplateParameterSymbol)ms.Base;
			Assert.IsInstanceOfType(tps.Base, typeof(PrimitiveType));

			var pt = (PrimitiveType)tps.Base;
			Assert.AreEqual(DTokens.Int, pt.TypeToken);
		}
	}
}
