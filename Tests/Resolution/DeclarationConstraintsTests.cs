using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using NUnit.Framework;

namespace Tests.Resolution
{
	[TestFixture]
	public class DeclarationConstraintsTests : ResolutionTestHelper
	{
		[Test]
		public void DeclCond1()
		{
			var pcl = CreateCache(out DModule module, @"module m;

version = A;

version(Windows)
	int* f(){}
else
	int[] f(){}


debug
	int* d(){}
else
	int[] d(){}


version(A)
	int* a(){}
else
	int[] a(){}

version = B;

version(B)
	import b;

version(C)
	import c;

", @"module b; int pubB;",
@"module c; int pubC;");

			var ctxt = CreateDefCtxt(pcl, module);

			// Test basic version-dependent resolution
			var ms = R("f", ctxt);
			Assert.AreEqual(1, ms.Count);
			var m = ms[0] as MemberSymbol;
			Assert.IsNotNull(m);

			Assert.That(m.Base, Is.TypeOf(typeof(PointerType)));

			ms = R("d", ctxt);
			Assert.AreEqual(1, ms.Count);
			m = ms[0] as MemberSymbol;
			Assert.IsNotNull(m);

			Assert.That(m.Base, Is.TypeOf(typeof(PointerType)));

			ctxt.CurrentContext.Set(ctxt.ScopedBlock.EndLocation);
			ms = R("a", ctxt);
			Assert.AreEqual(1, ms.Count);
			m = ms[0] as MemberSymbol;
			Assert.IsNotNull(m);

			Assert.That(m.Base, Is.TypeOf(typeof(PointerType)));

			ms = R("pubB", ctxt);
			Assert.AreEqual(1, ms.Count);

			ms = R("pubC", ctxt);
			Assert.AreEqual(0, ms.Count);
		}

		[Test]
		public void NestedTypes()
		{
			var ctxt = CreateDefCtxt(@"
module A;
class cl
{
	subCl inst;
	class subCl { int b; }
}

cl clInst;
");

			var x = DParser.ParseExpression("clInst.inst.b");
			var v = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(v, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((v as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void IfStmtDeclaredSymbols()
		{
			var ctxt = CreateDefCtxt(@"module A;
void foo()
{
if(auto n = 1234)
	n;
}");
			var A = ctxt.MainPackage()["A"];
			var ifStmt = (A["foo"].First() as DMethod).Body.SubStatements.ElementAt(0) as IfStatement;
			var nStmt = (ifStmt.ScopedStatement as ExpressionStatement).Expression;
			ctxt.CurrentContext.Set(nStmt.Location);

			var t = ExpressionTypeEvaluation.EvaluateType(nStmt, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void IfStmtPseudoVersion()
		{
			var ctxt = CreateCtxt("A", @"module A;
import B;

static if(enumA):

int a;

static if(enumB):

int b;

", @"module B; 
enum enumA = true; 
enum enumB = false;
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("a");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("b");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.Null);
		}

		[Test]
		public void AliasedTemplate_PreferenceOfParameterizedBaseSymbols()
		{
			var ctxt = CreateDefCtxt(@"module A;
int bar(){}
void* bar(T)(){}
alias bar!int aliasOne;
alias bar aliasTwo;
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("aliasOne()");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PointerType)));
		}

		[Test]
		public void AliasedTemplate()
		{
			var ctxt = CreateDefCtxt(@"module A;
int bar(){}
T[] bar(T)(){}
alias bar!int aliasOne;
alias bar aliasTwo;
");

			IExpression x;
			AbstractType t;
			MemberSymbol ms;

			x = DParser.ParseExpression("aliasOne()");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(ArrayType)));

			x = DParser.ParseExpression("aliasOne!(byte*)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false);

			ms = t as MemberSymbol;
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That(ms.DeducedTypes[0].Base, Is.TypeOf(typeof(PointerType)));
			Assert.That(ms.Base, Is.TypeOf(typeof(ArrayType)));
		}

		[Test]
		public void AliasedTemplate2()
		{
			var ctxt = CreateDefCtxt(@"module A;
int bar(){}
void[] bar(T)(){}
alias bar!int aliasOne;
alias bar aliasTwo;
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("aliasTwo");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("aliasOne");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(ArrayType)));

			x = DParser.ParseExpression("aliasOne!(byte*,int)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.Null);
		}

		[Test]
		public void AliasThis()
		{
			var pcl = CreateCache(out DModule mod, @"
module A;

class cl
{
	int a;
	subCl inst;
	alias inst this;
	class subCl { int b; }
}

class notherClass
{
	int[] arr;
	alias arr this;
}

cl clInst;
notherClass ncl;
");

			var ctxt = ResolutionContext.Create(pcl, null, mod);

			var x = DParser.ParseExpression("clInst.a");
			var v = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That((v as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("clInst.inst.b");
			v = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(v, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((v as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("clInst.b");
			v = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(v, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((v as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("ncl.arr");
			v = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(v, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((v as MemberSymbol).Base, Is.TypeOf(typeof(ArrayType)));

			// Test for static properties
			x = DParser.ParseExpression("ncl.length");
			v = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(v, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((v as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void DeclCond2()
		{
			var pcl = CreateCache(out DModule m, @"module m;

version(X)
	int x;
else
	int y;

class A
{
	version(X)
		void foo()
		{
			x; // 0

			version(X2) // 1
				int x2;

			version(X2) // 2
			{
				x2;
			}

			int t3=0; // 3

			t1; // 4
			t2; // 5
			t3; // 6

			int t1;
			version(X)
				int t2;
		}

	version(X)
		int z;
	int z2;
	version(X_not)
		int z3;
}

version(X)
	int postx;
else
	int posty;


debug = C

debug
	int dbg_a;

debug(C)
	int dbg_b;
else
	int dbg_c;

debug = 4;

debug = 3;

debug(2)
	int dbg_d;

debug(3)
	int dbg_e;

debug(4)
	int dbg_f;

");
			var A = m["A"].First() as DClassLike;
			var foo = A["foo"].First() as DMethod;
			var subst = foo.Body.SubStatements as List<IStatement>;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body);

			var x = R("x", ctxt);
			Assert.AreEqual(1, x.Count);

			x = R("y", ctxt);
			Assert.AreEqual(0, x.Count);

			x = R("z", ctxt);
			Assert.AreEqual(1, x.Count);

			x = R("z2", ctxt);
			Assert.AreEqual(1, x.Count);

			x = R("z3", ctxt);
			Assert.AreEqual(0, x.Count);

			IStatement ss;
			ss = ((subst[2] as StatementCondition).ScopedStatement as BlockStatement).SubStatements.First();

			ctxt.CurrentContext.Set(ss.Location);
			var x2 = ExpressionTypeEvaluation.EvaluateType(((ExpressionStatement)ss).Expression, ctxt);
			Assert.That(x2, Is.TypeOf(typeof(MemberSymbol)));

			ctxt.CurrentContext.Set((ss = subst[4]).Location);
			x2 = ExpressionTypeEvaluation.EvaluateType(((ExpressionStatement)ss).Expression, ctxt);
			Assert.IsNull(x2);

			ctxt.CurrentContext.Set((ss = subst[5]).Location);
			x2 = ExpressionTypeEvaluation.EvaluateType(((ExpressionStatement)ss).Expression, ctxt);
			Assert.IsNull(x2);

			ctxt.CurrentContext.Set((ss = subst[6]).Location);
			x2 = ExpressionTypeEvaluation.EvaluateType(((ExpressionStatement)ss).Expression, ctxt);
			Assert.That(x2, Is.TypeOf(typeof(MemberSymbol)));

			x = R("dbg_a", ctxt);
			Assert.AreEqual(1, x.Count);

			ctxt.CurrentContext.Set(m.EndLocation);

			x = R("dbg_b", ctxt);
			Assert.AreEqual(1, x.Count);

			x = R("dbg_c", ctxt);
			Assert.AreEqual(0, x.Count);

			x = R("dbg_d", ctxt);
			Assert.AreEqual(1, x.Count);

			x = R("dbg_e", ctxt);
			Assert.AreEqual(1, x.Count);

			x = R("dbg_f", ctxt);
			Assert.AreEqual(0, x.Count);
		}

		[Test]
		public void DeclCond3()
		{
			var pcl = CreateCache(out DModule mod, @"module m;
version = X;

version(X)
	int a;
else
	int b;

version(Y)
	int c;

debug
	int dbgX;
else
	int dbgY;

", @"module B;

debug int dbg;
else int noDbg;

debug = D;

debug(D)
	int a;
else
	int b;

template T(O)
{
	version(Windows)
		O[] T;
	else
		O T;
}

void main()
{
	a;
	b;
	dbg;
	noDbg;
}");
			var ctxt = CreateDefCtxt(pcl, mod, mod.EndLocation);

			var x = R("a", ctxt);
			Assert.AreEqual(1, x.Count);

			x = R("b", ctxt);
			Assert.AreEqual(0, x.Count);

			x = R("c", ctxt);
			Assert.AreEqual(0, x.Count);

			x = R("dbgX", ctxt);
			Assert.AreEqual(1, x.Count);

			x = R("dbgY", ctxt);
			Assert.AreEqual(0, x.Count);

			ctxt.CurrentContext.Set(mod = pcl.FirstPackage()["B"], mod.EndLocation);

			x = R("dbg", ctxt);
			Assert.AreEqual(1, x.Count);

			x = R("noDbg", ctxt);
			Assert.AreEqual(0, x.Count);

			x = R("a", ctxt);
			Assert.AreEqual(1, x.Count);

			x = R("b", ctxt);
			Assert.AreEqual(0, x.Count);

			DToken tk;
			var t = RS(DParser.ParseBasicType("T!int", out tk), ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			t = ((MemberSymbol)t).Base;
			Assert.That(t, Is.TypeOf(typeof(ArrayType)));

			var main = pcl.FirstPackage()["B"]["main"].First() as DMethod;
			var subSt = main.Body.SubStatements as List<IStatement>;
			using (ctxt.Push(main))
			{
				var ss = subSt[0] as ExpressionStatement;
				ctxt.CurrentContext.Set(ss.Location);
				t = ExpressionTypeEvaluation.EvaluateType(ss.Expression, ctxt);
				Assert.IsNotNull(t);

				ss = subSt[1] as ExpressionStatement;
				ctxt.CurrentContext.Set(ss.Location);
				t = ExpressionTypeEvaluation.EvaluateType(ss.Expression, ctxt);
				Assert.IsNull(t);

				ss = subSt[2] as ExpressionStatement;
				ctxt.CurrentContext.Set(ss.Location);
				t = ExpressionTypeEvaluation.EvaluateType(ss.Expression, ctxt);
				Assert.IsNotNull(t);

				ss = subSt[3] as ExpressionStatement;
				ctxt.CurrentContext.Set(ss.Location);
				t = ExpressionTypeEvaluation.EvaluateType(ss.Expression, ctxt);
				Assert.IsNull(t);
			}
		}

		[Test]
		public void DeclCond4()
		{
			var ctxt = CreateCtxt("A", @"module A;
version = X;

version(X){
	int vx;
}
else{
	int vy;
}

int xx1;

version = Y;

version(Y)
{
	int xa;
}
else
	int xb;

int xx2;

version(Z){
	version = U;
}

version(U)
	int xu;

int xx3;
");
			var A = ctxt.MainPackage()["A"];
			ctxt.CurrentContext.Set(A["vy"].First().Location);

			AbstractType t;
			t = RS("vy", ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));

			t = RS("vx", ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));


			ctxt.CurrentContext.Set(A["xx2"].First().Location);

			t = RS("xa", ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));

			t = RS("xb", ctxt);
			Assert.That(t, Is.Null);

			ctxt.CurrentContext.Set(A["xx3"].First().Location);

			t = RS("xu", ctxt);
			Assert.That(t, Is.Null);
		}

		[Test]
		public void DeclConstraints()
		{
			var pcl = CreateCache(out DModule A, @"module A;

const i = 12;

static if(i>0)
	int a;
else
	int b;

template Templ(T)
{
	static if(is(T:int))
		enum Templ = 1;
	else
		enum Templ = 0;
}

static if(Templ!int == 1)
	int c;

static if(Templ!float)
	int d;
else
	int e;");
			var ctxt = CreateDefCtxt(pcl, A);

			var x = R("a", ctxt);
			Assert.AreEqual(1, x.Count);

			x = R("b", ctxt);
			Assert.AreEqual(0, x.Count);

			var v = Evaluation.EvaluateValue(DParser.ParseExpression("Templ!int"), ctxt, out var variableValue);
			Assert.That(variableValue, Is.Not.Null);
			Assert.That(v, Is.InstanceOf(typeof(PrimitiveValue)));
			var pv = (PrimitiveValue)v;
			Assert.AreEqual(1m, pv.Value);

			x = R("c", ctxt);
			Assert.AreEqual(1, x.Count);

			x = R("d", ctxt);
			Assert.AreEqual(0, x.Count);

			x = R("e", ctxt);
			Assert.AreEqual(1, x.Count);
		}

		[Test]
		public void DeclConditions2()
		{
			var pcl = CreateCache(out DModule B, @"module B;

class home {}

static if(!is(typeof(asd)))
	import C;
static if(is(typeof(home)))
	import A;

void bar();
", @"module A;
class cl{}",

@"module C;
class imp{}");
			var ctxt = CreateDefCtxt(pcl, B["bar"].First() as DMethod);

			var x = R("imp", ctxt);
			Assert.That(x.Count, Is.EqualTo(0));

			x = R("cl", ctxt);
			Assert.That(x.Count, Is.EqualTo(1));
		}

		[Test]
		public void DeclConstraints3()
		{
			var pcl = CreateCache(out DModule A, @"module A;
class cl(T) if(is(T==int))
{}

class aa(T) if(is(T==float)) {}
class aa(T) if(is(T==int)) {}");
			var ctxt = CreateDefCtxt(pcl, A);

			var x = TypeDeclarationResolver.ResolveSingle(new IdentifierDeclaration("cl"), ctxt);
			Assert.That(x, Is.Null);

			var ex = DParser.ParseAssignExpression("cl!int");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.Not.TypeOf(typeof(AmbiguousType)));

			ex = DParser.ParseAssignExpression("cl!float");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.Null);

			ex = DParser.ParseAssignExpression("aa!float");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.Not.TypeOf(typeof(AmbiguousType)));
			var t = x as ClassType;
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Definition, Is.EqualTo(A["aa"].First()));

			ex = DParser.ParseAssignExpression("aa!int");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.Not.TypeOf(typeof(AmbiguousType)));
			t = x as ClassType;
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Definition, Is.EqualTo((A["aa"] as List<INode>)[1]));

			ex = DParser.ParseAssignExpression("aa!string");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.Null);
		}

		[Test]
		public void Unqual()
		{
			var ctxt = CreateCtxt("A", @"module std.typecons;
template Unqual(T)
{
    version (none) // Error: recursive alias declaration @@@BUG1308@@@
    {
             static if (is(T U ==     const U)) alias Unqual!U Unqual;
        else static if (is(T U == immutable U)) alias Unqual!U Unqual;
        else static if (is(T U ==     inout U)) alias Unqual!U Unqual;
        else static if (is(T U ==    shared U)) alias Unqual!U Unqual;
        else                                    alias        T Unqual;
    }
    else // workaround
    {
             static if (is(T U == shared(inout U))) alias U Unqual;
        else static if (is(T U == shared(const U))) alias U Unqual;
        else static if (is(T U ==        inout U )) alias U Unqual;
        else static if (is(T U ==        const U )) alias U Unqual;
        else static if (is(T U ==    immutable U )) alias U Unqual;
        else static if (is(T U ==       shared U )) alias U Unqual;
        else                                        alias T Unqual;
    }
}", @"module A;

import std.typecons;

class Tmpl(M)
{
	Unqual!M inst;
}

alias immutable(int[]) ImmIntArr;

unittest
{
    static assert(is(A == immutable(int)[]));
}
");

			ITypeDeclaration td;
			AbstractType t;
			PrimitiveType pt;
			ArrayType at;

			var A = ctxt.MainPackage()["A"];
			var Tmpl = A["Tmpl"].First() as DClassLike;
			ctxt.CurrentContext.Set(Tmpl);

			td = DParser.ParseBasicType("inst");
			t = RS(td, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			var baseT = (t as MemberSymbol).Base;
			Assert.That(baseT, Is.TypeOf(typeof(TemplateParameterSymbol)));

			ctxt.CurrentContext.Set(A);

			td = DParser.ParseBasicType("Unqual!ImmIntArr");
			t = RS(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			at = (t as TemplateParameterSymbol).Base as ArrayType;
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(ArrayType)));
			pt = at.ValueType as PrimitiveType;
			Assert.That(at.ValueType, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(pt.HasModifier(DTokens.Immutable));
			// immutable(int[]) becomes immutable(int)[] ?!

			ctxt.CurrentContext.DeducedTemplateParameters.Clear();

			td = DParser.ParseBasicType("Unqual!int");
			t = RS(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));

			ctxt.CurrentContext.DeducedTemplateParameters.Clear();

			td = DParser.ParseBasicType("Unqual!(const(int))");
			t = RS(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			pt = (t as TemplateParameterSymbol).Base as PrimitiveType;
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(pt.HasModifiers, Is.False);

			ctxt.CurrentContext.DeducedTemplateParameters.Clear();

			td = DParser.ParseBasicType("Unqual!(inout(int))");
			t = RS(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			pt = (t as TemplateParameterSymbol).Base as PrimitiveType;
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(pt.HasModifiers, Is.False);

			ctxt.CurrentContext.DeducedTemplateParameters.Clear();

			td = DParser.ParseBasicType("Unqual!(immutable(int))");
			t = RS(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			pt = (t as TemplateParameterSymbol).Base as PrimitiveType;
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(pt.HasModifiers, Is.False);

			ctxt.CurrentContext.DeducedTemplateParameters.Clear();

			td = DParser.ParseBasicType("Unqual!(shared(int))");
			t = RS(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			pt = (t as TemplateParameterSymbol).Base as PrimitiveType;
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(pt.HasModifiers, Is.False);

			ctxt.CurrentContext.DeducedTemplateParameters.Clear();

			td = DParser.ParseBasicType("Unqual!(const(shared(int)))");
			t = RS(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			pt = (t as TemplateParameterSymbol).Base as PrimitiveType;
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(pt.HasModifiers, Is.False);

			ctxt.CurrentContext.DeducedTemplateParameters.Clear();

			td = DParser.ParseBasicType("Unqual!(shared const int)");
			t = RS(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			pt = (t as TemplateParameterSymbol).Base as PrimitiveType;
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(pt.HasModifiers, Is.False);
		}

		[Test]
		public void MethodParameterTypeResolutionScope()
		{
			var ctxt = CreateCtxt("A", @"module A;
public static struct Namespace
{
alias ulong UserId;
int getGames(UserId); // UserId, not Namespace.UserId
}

Namespace.UserId uid;
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("Namespace.getGames(uid)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PrimitiveType)));

		}

		/// <summary>
		/// Strings literals which are sliced are now implicitly convertible to a char pointer:
		/// 
		/// To help ease interacting with C libraries which expect strings as 
		/// null-terminated pointers, slicing string literals (not variables!) 
		/// will now allow the implicit conversion to such a pointer:
		/// </summary>
		[Test]
		public void StringSliceConvertability()
		{
			var ctxt = CreateCtxt("A", @"module A;");

			var constChar = new PointerType(new PrimitiveType(DTokens.Char, DTokens.Const));

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("\"abc\"");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(ResultComparer.IsImplicitlyConvertible(t, constChar, ctxt));

			x = DParser.ParseExpression("\"abc\"[0..2]");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(ResultComparer.IsImplicitlyConvertible(t, constChar, ctxt));
		}

		[Test]
		public void AliasThis2()
		{
			var ctxt = CreateCtxt("A", @"module B;
struct LockedConnection(Connection) {
	private {
		Connection m_conn;
	}
	
	@property inout(Connection) __conn() inout { return m_conn; }
	
	alias __conn this;
}

", @"module A;

import B;

class ConnectionPool(Connection)
{
	private {
		Connection[] m_connections;
	}
	
	LockedConnection!Connection lockConnection(){}
}

final class RedisConnection
{
	int[] bar;
	int request(string command, in ubyte[][] args...) {}
}

ConnectionPool!RedisConnection m_connections;
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("m_connections.lockConnection().bar");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			t = (t as DerivedDataType).Base;
			Assert.That(t, Is.TypeOf(typeof(ArrayType)));
		}

		[Test]
		public void AliasThis3()
		{
			var ctxt = CreateCtxt("A", @"module A;
struct CL {
  enum en : Color {
    none = Color( 0, 0, 0, 0 ),
   
    white = Color( 1 ),
    black = Color( 0 ),
   
    red = Color( 1, 0, 0 ),
    green = Color( 0, 1, 0 ),
    blue = Color( 0, 0, 1 ),
  }
  alias en this;
 
  alias assoc = EnumAssociativeFunc!( en, en.none );
}
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("CL.white");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Definition, Is.TypeOf(typeof(DEnumValue)));
		}

		[Test]
		public void AliasThis4()
		{
			var ctxt = CreateCtxt("m", @"module m;
struct A {
int hello;
}
alias TR = A*;
struct AliasThis {
alias TR this;
}

AliasThis str;
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("str.hello");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void AliasThis5()
		{
			var ctxt = CreateCtxt("m", @"module m;
struct A {
    int hello;
}

struct AliasThis {
    @property const(A) get() const;
    @property A get();
    alias get this;
}

AliasThis str;
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("str.hello");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void AliasThisSO()
		{
			var ctxt = CreateCtxt("A", @"module A;
class Cls
{
	alias derp this;
	alias derp this;
}

Cls inst;
");
			var x = DParser.ParseExpression("inst.a");
			ExpressionTypeEvaluation.EvaluateType(x, ctxt);
		}

		[Test]
		public void AliasThisOnNonInstances()
		{
			var ctxt = CreateCtxt("A", @"module A;
struct S1 { int a; }
struct S2(T) {
	int b;
	alias T this;
}

S2!S1 s;
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("s.a");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Definition, Is.TypeOf(typeof(DVariable)));
		}

		[Test]
		public void TypeofIntSize()
		{
			var ctxt = CreateDefCtxt();

			ITypeDeclaration td;
			AbstractType t;
			DToken tk;

			td = DParser.ParseBasicType("typeof(int.sizeof)", out tk);
			t = RS(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That((t as PrimitiveType).TypeToken, Is.EqualTo(DTokens.Uint));
		}
	}
}
