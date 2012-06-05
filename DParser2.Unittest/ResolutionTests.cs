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

namespace D_Parser.Unittest
{
	[TestClass]
	public class ResolutionTests
	{
		public static IAbstractSyntaxTree objMod = DParser.ParseString(@"module object;
						alias immutable(char)[] string;
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

			var A=pcl[0]["modA"]["A"] as DClassLike;
			var bar = A["bar"] as DMethod;
			var call_fooC = bar.Body.SubStatements[0];

			Assert.IsInstanceOfType(call_fooC, typeof(ExpressionStatement));

			var ctxt=new ResolverContextStack(pcl, new ResolverContext{ ScopedBlock=bar, ScopedStatement=call_fooC });

			var call = ((ExpressionStatement)call_fooC).Expression;
			var methodName = ((PostfixExpression_MethodCall)call).PostfixForeExpression;

			var res=ExpressionTypeResolver.Resolve(methodName,ctxt);

			Assert.IsTrue(res!=null && res.Length==1, "Resolve() returned no result!");
			Assert.IsInstanceOfType(res[0],typeof(MemberResult));

			var mr = (MemberResult)res[0];

			Assert.IsInstanceOfType(mr.Node, typeof(DMethod));
			Assert.AreEqual(mr.Node.Name, "fooC");
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

			var res = ExpressionTypeResolver.Resolve(instanceExpr, ctxt);

			Assert.IsNotNull(res);
			Assert.AreEqual(res.Length, 1);

			var r1 = res[0];

			Assert.IsInstanceOfType(r1,typeof(MemberResult));
			var mr = r1 as MemberResult;

			Assert.IsNotNull(mr.MemberBaseTypes);
			Assert.AreEqual(mr.MemberBaseTypes.Length, 1);
			Assert.IsInstanceOfType(mr.MemberBaseTypes[0], typeof(StaticTypeResult));
			var sr = (StaticTypeResult)mr.MemberBaseTypes[0];

			Assert.AreEqual(sr.BaseTypeToken, DTokens.Int);
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
			var bt = ExpressionTypeResolver.Resolve(call, ctxt);

			Assert.IsTrue(bt!=null && bt.Length == 1, "Resolution returned empty result instead of 'int'");
			var st = bt[0] as StaticTypeResult;
			Assert.IsNotNull(st, "Result must be Static type int");
			Assert.AreEqual(st.BaseTypeToken, DTokens.Int, "Static type must be int");
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
	}
}
