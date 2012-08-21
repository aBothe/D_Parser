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
	public class UFCSTests
	{
		[TestMethod]
		public void TestBasicUFCS()
		{
			var pcl = ResolutionTests.CreateCache(@"module modA;
void writeln(T...)(T t) {}
string foo(string a) {}
void foo(int a) {}

void main(){
	string s;
}");
			var modA=pcl[0]["modA"];
			var ctxt = new ResolverContextStack(pcl, new ResolverContext { ScopedBlock=modA });

			var main=modA["main"][0] as DMethod;
			var s = main.Body.Declarations[0];
			var s_res= TypeDeclarationResolver.HandleNodeMatch(s, ctxt);

			var methods=pcl[0].UfcsCache.FindFitting(ctxt, s.EndLocation, s_res).ToArray();

			// foo(string), writeln
			Assert.AreEqual(methods.Length, 2);
		}
	}
}
