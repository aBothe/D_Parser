using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using D_Parser;
using D_Parser.Parser;
using D_Parser.Dom;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver;

namespace D_Parser.Unittest
{
	[TestClass]
	public class ReferenceFinding
	{
		[TestMethod]
		public void TestMethod1()
		{
			var pcl = ResolutionTests.CreateCache(@"module modA;

class A
{

}

A a = new A();
");
			var ctxt = new ResolverContextStack(pcl, new ResolverContext{ ScopedBlock = pcl[0]["modA"] });

			var refs = ReferencesFinder.Scan(pcl[0]["modA"]["A"]);
		}
	}
}
