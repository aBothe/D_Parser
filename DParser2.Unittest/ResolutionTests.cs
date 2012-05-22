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

			var code = @"void foo(T:E...,E)(T t) {}

int foo(Y,T)(Y y, T t) {}

class MyClass(U) {}

alias immutable(char)[] string;";

			var ast = DParser.ParseString(code);

			var ctxt = new ResolverContextStack(new D_Parser.Misc.ParseCacheList(), new ResolverContext { 
				ScopedBlock = ast,
				ScopedStatement = null
			});

			var instanceExpr = DParser.ParseExpression("foo!(string...)(3)");

			var res = ExpressionTypeResolver.Resolve(instanceExpr, ctxt);
		}
	}
}
