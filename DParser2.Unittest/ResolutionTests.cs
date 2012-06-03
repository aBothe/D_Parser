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

namespace D_Parser.Unittest
{
	[TestClass]
	public class ResolutionTests
	{
		[TestInitialize]
		public void Init()
		{
			/*
			* If the ActualClass is defined in an other module (so not in where the type resolution has been started),
			* we have to enable access to the ActualClass's module's imports!
			* 
			* module modA:
			* import modB;
			* 
			* class A:B{
			* 
			*		void bar()
			*		{
			*			fooC(); // Note that modC wasn't imported publically! Anyway, we're still able to access this method!
			*			// So, the resolver must know that there is a class C.
			*		}
			* }
			* 
			* -----------------
			* module modB:
			* import modC;
			* 
			* // --> When being about to resolve B's base class C, we have to use the imports of modB(!), not modA
			* class B:C{}
			* -----------------
			* module modC:
			* 
			* class C{
			* 
			* void fooC();
			* 
			* }
			*/
		}

		[TestMethod]
		public void TestMethod1()
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

			var objMod = DParser.ParseString(@"module object;
alias immutable(char)[] string;
class Object {}");

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
