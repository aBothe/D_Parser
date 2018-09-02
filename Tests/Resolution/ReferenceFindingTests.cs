using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D_Parser.Parser;
using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Refactoring;
using D_Parser.Completion;
using D_Parser.Misc;

namespace Tests.Resolution
{
	[TestFixture]
	public class ReferenceFindingTests
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
			var ctxt = ResolutionContext.Create(pcl, null, pcl.FirstPackage()["modA"]);

			var refs = ReferencesFinder.SearchModuleForASTNodeReferences(pcl.FirstPackage()["modA"]["A"].First(),ctxt) as List<ISyntaxRegion>;

			Assert.IsNotNull(refs);
			Assert.AreEqual(8, refs.Count);
		}

		[Test]
		public void TypeRefFinding()
		{
			var modA = DParser.ParseString(@"module modA;
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

			var ed = new EditorData { 
				SyntaxTree = modA,
				ParseCache = new LegacyParseCacheView(new RootPackage[0])
			};

			var res = TypeReferenceFinder.Scan(ed, System.Threading.CancellationToken.None, null);

			Assert.That(res.Count, Is.GreaterThan(6));
		}
	}
}
