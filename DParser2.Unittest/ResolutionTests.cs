using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Misc;

namespace ParserTests
{
	class ResolutionTests
	{
		public static void Run()
		{
			Console.WriteLine("\tResolution tests...");

			var code = @"

//void foo(T:MyClass!E,E)(T t) {}
int foo(Y,T)(Y y, T t) {}
//string[] foo(T)(T t, T u) {}

class A {
	const void aBar(this T)() {}
}
class B:A{}
class C:B{}

class MyClass(T) {}
class MyClass(T:A) {}
class MyClass(T:B) {}

class D(int u) {}
class D(int u:1) {}

const int a=3;
int b=4;


alias immutable(char)[] string;";

			var ast = DParser.ParseString(code);
			ast.ModuleName = "moduleA";

			var pcl=new ParseCacheList();
			var pc=new ParseCache();
			pcl.Add(pc);
			pc.AddOrUpdate(ast);
			pc.UfcsCache.Update(pcl);

			var ctxt = new ResolverContextStack(pcl, new ResolverContext{
				ScopedBlock = ast,
				ScopedStatement = null
			});

			var instanceExpr = DParser.ParseExpression("(1).foo(2)");

			var res = ExpressionTypeResolver.Resolve(instanceExpr, ctxt);
		}
	}
}
