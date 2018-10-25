﻿using System.Collections.Generic;
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

void main(string args[])
{
	auto a = new A!int();
	a.n;
	A.prop = 3;
	int b = A.prop + 4;
	A!double.statA.statA = new A!double();

	StructB localVar;
	localVar.fieldB = 1;
}
");
			var modB = DParser.ParseString(@"module modB;
struct StructB
{
	int fieldB;
}
");
			var rootpkgs = new RootPackage[1];
			var rootpkg = new MutableRootPackage();
			rootpkg.AddModule(modB);
			rootpkgs[0] = rootpkg;

			var ed = new EditorData { 
				SyntaxTree = modA,
				ParseCache = new LegacyParseCacheView(rootpkgs)
			};

			// no timeout
			var cancelTokenSource = new System.Threading.CancellationTokenSource();
			var res = TypeReferenceFinder.Scan(ed, cancelTokenSource.Token, null);

			Assert.That(res.Count, Is.GreaterThan(6));
			Assert.AreEqual((byte)TypeReferenceKind.Class          , typeRefId(res, "ClassName"));
			Assert.AreEqual((byte)TypeReferenceKind.Interface      , typeRefId(res, "InterfaceName"));
			Assert.AreEqual((byte)TypeReferenceKind.Struct         , typeRefId(res, "StructName"));
			Assert.AreEqual((byte)TypeReferenceKind.Union          , typeRefId(res, "UnionName"));
			Assert.AreEqual((byte)TypeReferenceKind.EnumValue      , typeRefId(res, "AnonEnumValue"));
			Assert.AreEqual((byte)TypeReferenceKind.Enum           , typeRefId(res, "EnumName"));
			Assert.AreEqual((byte)TypeReferenceKind.EnumValue      , typeRefId(res, "EnumValue"));

			Assert.AreEqual((byte)TypeReferenceKind.Class          , typeRefId(res, "A"));
			Assert.AreEqual((byte)TypeReferenceKind.Template       , typeRefId(res, "TemplateName"));
			Assert.AreEqual((byte)TypeReferenceKind.TemplateTypeParameter, typeRefId(res, "TemplateParameter"));

			Assert.AreEqual((byte)TypeReferenceKind.LocalVariable  , typeRefId(res, "localVar"));
			Assert.AreEqual((byte)TypeReferenceKind.SharedVariable , typeRefId(res, "sharedVar"));
			Assert.AreEqual((byte)TypeReferenceKind.GSharedVariable, typeRefId(res, "gsharedVar"));
			Assert.AreEqual((byte)TypeReferenceKind.Constant       , typeRefId(res, "constant"));
			Assert.AreEqual((byte)TypeReferenceKind.MemberVariable , typeRefId(res, "FieldName"));
			Assert.AreEqual((byte)TypeReferenceKind.ParameterVariable, typeRefId(res, "args"));

			Assert.AreEqual((byte)TypeReferenceKind.Function       , typeRefId(res, "main"));
			Assert.AreEqual((byte)TypeReferenceKind.Method         , typeRefId(res, "MethodName"));

			Assert.AreEqual((byte)TypeReferenceKind.Struct         , typeRefId(res, "StructB"));
		}

		private byte typeRefId(Dictionary<int, Dictionary<ISyntaxRegion, byte>> refs, string id)
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
	}
}
