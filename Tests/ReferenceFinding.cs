using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D_Parser;
using D_Parser.Parser;
using D_Parser.Dom;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver;
using D_Parser.Refactoring;

namespace Tests
{
	[TestFixture]
	public class ReferenceFinding
	{
		[Test]
		public void Test1()
		{
			var pcl = ResolutionTests.CreateCache(@"module modA;

class A(T = int)
{
	static int prop;
	static A statA; // 6
}

A a = new A(); // 9

void main()
{
	A.prop = 3; // 13
	int b = A.prop + 4; // 14
	A.statA.statA = new A!float(); // 15
}
");
			var ctxt = ResolutionContext.Create(pcl, null, pcl[0]["modA"]);

			var refs = ReferencesFinder.Scan(pcl[0]["modA"]["A"][0],ctxt) as List<ISyntaxRegion>;

			Assert.IsNotNull(refs);
			Assert.AreEqual(8, refs.Count);
		}

		[Test]
		public void TypeRefFinding()
		{
			var pcl = ResolutionTests.CreateCache(@"module modA;

class A(T)
{
	int n;
	static int prop;
	static A!float statA;
}

void main()
{
	auto a = new A!int();
	a.n;
	A.prop = 3;
	int b = A.prop + 4;
	A!double.statA.statA = new A!double();
}
");

			var res = TypeReferenceFinder.Scan(pcl[0]["modA"], pcl);

			//Assert.AreEqual(6, res.TypeMatches.Count);
			//TODO: Correct variable recognization
		}
	}
}
