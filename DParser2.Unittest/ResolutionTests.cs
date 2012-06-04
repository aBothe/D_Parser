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

namespace D_Parser.Unittest
{
	[TestClass]
	public class ResolutionTests
	{
		IAbstractSyntaxTree objMod;

		[TestInitialize]
		public void Init()
		{
			objMod = DParser.ParseString(@"module object;
alias immutable(char)[] string;
class Object {}");
		}

		[TestMethod]
		public void TestMultiModuleResolution1()
		{
			var pcl = new ParseCacheList();
			var pc = new ParseCache();
			pcl.Add(pc);
			pc.AddOrUpdate(objMod);

			var modC = DParser.ParseString(@"module modC;
class C { void fooC(); }");
			pc.AddOrUpdate(modC);

			var modB = DParser.ParseString(@"module modB;
import modC;
class B:C{}");
			pc.AddOrUpdate(modB);

			var modA = DParser.ParseString(@"module modA;
import modB;
			
class A:B{	
		void bar() {
			fooC(); // Note that modC wasn't imported publically! Anyway, we're still able to access this method!
			// So, the resolver must know that there is a class C.
		}
}");
			pc.AddOrUpdate(modA);

			var A=modA["A"] as DClassLike;
			var bar = A["bar"] as DMethod;
			var call_fooC = bar.Body.SubStatements[0];

			Assert.IsInstanceOfType(call_fooC, typeof(Dom.Statements.ExpressionStatement));

			var ctxt=new ResolverContextStack(pcl, new ResolverContext{ ScopedBlock=bar, ScopedStatement=call_fooC });

			var call = ((Dom.Statements.ExpressionStatement)call_fooC).Expression;
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
			var code = @"

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
";

			var ast = DParser.ParseString(code);
			ast.ModuleName = "moduleA";

			var pcl = new ParseCacheList();
			var pc = new ParseCache();
			pcl.Add(pc);
			pc.AddOrUpdate(objMod);
			pc.AddOrUpdate(ast);
			pc.UfcsCache.Update(pcl);

			var ctxt = new ResolverContextStack(pcl, new ResolverContext
			{
				ScopedBlock = ast,
				ScopedStatement = null
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
	}
}
