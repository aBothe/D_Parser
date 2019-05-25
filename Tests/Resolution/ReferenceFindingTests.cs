using System.Collections.Generic;
using System.IO;
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

			var refs = ReferencesFinder.SearchModuleForASTNodeReferences(pcl.FirstPackage()["modA"]["A"].First(), ctxt) as List<ISyntaxRegion>;

			Assert.IsNotNull(refs);
			Assert.AreEqual(8, refs.Count);
		}

		[Test]
		public void TypeRefFinding()
		{
			var modA = DParser.ParseString(@"module modA;
import modB;

class A(T)
{
	int n;
	static int prop;
	static A!float statA;
}

class ClassName { void MethodName(); }
interface InterfaceName {}
struct StructName { int FieldName; }
union UnionName {}
enum { AnonEnumValue }
enum EnumName { EnumValue }
template TemplateName(TemplateParameter) { TemplateParameter name; }

enum constant = 1;
shared sharedVar;
__gshared gsharedVar;

alias AliasName = StructB;
alias int* PINT;

void main(string args[])
{
	auto a = new A!int();
	a.n;
	A.prop = 3;
	int b = A.prop + 4;
	A!double.statA.statA = new A!double();

	StructB localVar;
	localVar.fieldB = 1;
	localVar.getFieldB();
	int markLine, fieldB = localVar.fieldB;
}
");
			var modB = DParser.ParseString(@"module modB;
struct StructB
{
	int fieldB;
	int getFieldB() { return fieldB; }
}
");
			var rootpkgs = new RootPackage[1];
			var rootpkg = new MutableRootPackage();
			rootpkg.AddModule(modB);
			rootpkgs[0] = rootpkg;

			var ed = new EditorData
			{
				SyntaxTree = modA,
				ParseCache = new LegacyParseCacheView(rootpkgs)
			};

			// no timeout
			var cancelTokenSource = new System.Threading.CancellationTokenSource();
			var res = TypeReferenceFinder.Scan(ed, cancelTokenSource.Token, true, null);

			Assert.That(res.Count, Is.GreaterThan(6));
			Assert.AreEqual(TypeReferenceKind.Class          , typeRefId(res, "ClassName"));
			Assert.AreEqual(TypeReferenceKind.Interface      , typeRefId(res, "InterfaceName"));
			Assert.AreEqual(TypeReferenceKind.Struct         , typeRefId(res, "StructName"));
			Assert.AreEqual(TypeReferenceKind.Union          , typeRefId(res, "UnionName"));
			Assert.AreEqual(TypeReferenceKind.EnumValue      , typeRefId(res, "AnonEnumValue"));
			Assert.AreEqual(TypeReferenceKind.Enum           , typeRefId(res, "EnumName"));
			Assert.AreEqual(TypeReferenceKind.EnumValue      , typeRefId(res, "EnumValue"));

			Assert.AreEqual(TypeReferenceKind.Class          , typeRefId(res, "A"));
			Assert.AreEqual(TypeReferenceKind.Template       , typeRefId(res, "TemplateName"));
			Assert.AreEqual(TypeReferenceKind.TemplateTypeParameter, typeRefId(res, "TemplateParameter"));

			Assert.AreEqual(TypeReferenceKind.LocalVariable  , typeRefId(res, "localVar"));
			Assert.AreEqual(TypeReferenceKind.SharedVariable , typeRefId(res, "sharedVar"));
			Assert.AreEqual(TypeReferenceKind.GSharedVariable, typeRefId(res, "gsharedVar"));
			Assert.AreEqual(TypeReferenceKind.Constant       , typeRefId(res, "constant"));
			Assert.AreEqual(TypeReferenceKind.MemberVariable , typeRefId(res, "FieldName"));
			Assert.AreEqual(TypeReferenceKind.ParameterVariable, typeRefId(res, "args"));

			Assert.AreEqual(TypeReferenceKind.Function       , typeRefId(res, "main"));
			Assert.AreEqual(TypeReferenceKind.Method         , typeRefId(res, "MethodName"));

			Assert.AreEqual(TypeReferenceKind.Struct         , typeRefId(res, "AliasName"));
			Assert.AreEqual(TypeReferenceKind.BasicType      , typeRefId(res, "PINT"));
			Assert.AreEqual(TypeReferenceKind.Struct         , typeRefId(res, "StructB"));
			Assert.AreEqual(TypeReferenceKind.MemberVariable , typeRefId(res, "fieldB"));
			Assert.AreEqual(TypeReferenceKind.Method         , typeRefId(res, "getFieldB"));

			var line = findRefLine(res, "markLine");
			Assert.AreEqual(4, line.Count());
			var arr = line.ToArray();
			System.Array.Sort(arr, new TypeReferenceLocationComparer());
			Assert.AreEqual(TypeReferenceKind.LocalVariable,  arr[0].Value); // markLine
			Assert.AreEqual(TypeReferenceKind.LocalVariable,  arr[1].Value); // fieldB
			Assert.AreEqual(TypeReferenceKind.LocalVariable,  arr[2].Value); // localVar
			Assert.AreEqual(TypeReferenceKind.MemberVariable, arr[3].Value); // fieldB
		}

		[Test]
		public void TypeRefPackage()
		{
			var modA = DParser.ParseString(@"module test.modA;
import imp.pkg;
imp.pkg.PKG p;
imp.pkg.MOD m;
");
			var modB = DParser.ParseString(@"module imp.pkg;
public import imp.pkg.mod;
alias PKG = int;
");
			var modC = DParser.ParseString(@"module imp.pkg.mod;
alias MOD = long;
");
			var rootpkgs = new RootPackage[1];
			var rootpkg = new MutableRootPackage();
			rootpkg.AddModule(modB);
			rootpkg.AddModule(modC);
			rootpkgs[0] = rootpkg;

			modB.FileName = Path.Combine("imp", "pkg", "package.d");
			modC.FileName = Path.Combine("imp", "pkg", "mod.d");

			var ed = new EditorData
			{
				SyntaxTree = modA,
				ParseCache = new LegacyParseCacheView(rootpkgs)
			};

			// no timeout
			var cancelTokenSource = new System.Threading.CancellationTokenSource();
			var res = TypeReferenceFinder.Scan(ed, cancelTokenSource.Token, true, null);

			Assert.That(res.Count, Is.EqualTo(4)); // at least 2 on 4 lines
		}

		class TypeReferenceLocationComparer : Comparer<KeyValuePair<ISyntaxRegion, TypeReferenceKind>>
		{
			public override int Compare(KeyValuePair<ISyntaxRegion, TypeReferenceKind> x,
			                            KeyValuePair<ISyntaxRegion, TypeReferenceKind> y)
			{
				if (x.Key.Location == y.Key.Location)
					return 0;
				return x.Key.Location < y.Key.Location ? -1 : 1;
			}
		}

		private TypeReferenceKind typeRefId(Dictionary<int, Dictionary<ISyntaxRegion, TypeReferenceKind>> refs, string id)
		{
			foreach (var line in refs)
				foreach (var idref in line.Value)
				{
					var sr = idref.Key;
					if (sr is INode && (sr as INode).Name == id)
						return idref.Value;
					if (sr is TemplateParameter && (sr as TemplateParameter).Name == id)
						return idref.Value;
					if (sr is IdentifierDeclaration && (sr as IdentifierDeclaration).Id == id)
						return idref.Value;
					if (sr.ToString() == id)
						return idref.Value;
				}
			return 0;
		}

		private Dictionary<ISyntaxRegion, TypeReferenceKind> findRefLine(Dictionary<int, Dictionary<ISyntaxRegion, TypeReferenceKind>> refs, string id)
		{
			foreach (var line in refs)
				foreach (var idref in line.Value)
				{
					var sr = idref.Key;
					if (sr is INode && (sr as INode).Name == id)
						return line.Value;
				}
			return null;
		}

		[Test]
		public void StaticForeach_StackOverflow()
		{
			var modA = DParser.ParseString(@"module modA;
alias PageBits = size_t[4];

void sweep()
{
    Pool* pool = null;
    static foreach (w; 0 .. PageBits.length) {
	}

    if (pool.data) {}
}");
			var rootpkgs = new RootPackage[0];
			var ed = new EditorData
			{
				SyntaxTree = modA,
				ParseCache = new LegacyParseCacheView(rootpkgs)
			};

			// no timeout
			var cancelTokenSource = new System.Threading.CancellationTokenSource();
			var res = TypeReferenceFinder.Scan(ed, cancelTokenSource.Token, true, null);
			Assert.That(res.Count, Is.GreaterThan(4)); // PageBits, sweep, pool, PageBits?, data
		}

		[Test]
		public void ValueProvider()
		{
			var modA = DParser.ParseString(@"module modA;
@safe unittest
{
	int[] src = [1, 5, 8, 9, 10, 1, 2, 0];
	auto dest = new int[src.length];
	auto rem = src.copy(dest);
 
	assert(dest[0.. $ -rem.length] == [ 1, 5, 9, 1 ]);
}
}");
			var rootpkgs = new RootPackage[0];
			var ed = new EditorData
			{
				SyntaxTree = modA,
				ParseCache = new LegacyParseCacheView(rootpkgs)
			};

			// no timeout
			var cancelTokenSource = new System.Threading.CancellationTokenSource();
			var res = TypeReferenceFinder.Scan(ed, cancelTokenSource.Token, true, null);
			Assert.That(res.Count, Is.GreaterThan(4)); // PageBits, sweep, pool, PageBits?, data
		}
	}
}