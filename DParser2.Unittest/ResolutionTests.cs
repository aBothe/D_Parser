using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom;
using D_Parser.Parser;

namespace ParserTests
{
	class ResolutionTests
	{
		public static void Run()
		{
			Console.WriteLine("\tResolution tests...");

			var code = @"void foo(T:MyClass!(E[]),E)(T t) {}

int foo(Y,T)(Y y, T t) {}

class A {}
class B:A{}
class C:B{}

class MyClass(T:T) {}
class MyClass(T:T*) {}
class MyClass(T:T**) {}

class Derp(alias X) {}

const int a=3;
int b=4;

alias immutable(char)[] string;";

			var ast = DParser.ParseString(code);

			var ctxt = new ResolverContextStack(new D_Parser.Misc.ParseCacheList(), new ResolverContext { 
				ScopedBlock = ast,
				ScopedStatement = null
			});

			var instanceExpr = DParser.ParseExpression("(new MyClass!(int*))");

			var res = ExpressionTypeResolver.Resolve(instanceExpr, ctxt);
		}
	}
}
