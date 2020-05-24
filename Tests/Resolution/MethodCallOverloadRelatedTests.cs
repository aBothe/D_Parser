using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using NUnit.Framework;

namespace Tests.Resolution
{
	[TestFixture]
	public class MethodCallOverloadRelatedTests : ResolutionTestHelper
	{
		[Test]
		public void ConstAttributedSymbolType ()
		{
			AbstractType t;
			IExpression x;
			DModule A;

			var code = "module A; const Object co;";

			var ctxt = CreateDefCtxt ("A", out A, code);

			x = DParser.ParseExpression ("co");

			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf<MemberSymbol> ());
			var baseType = ((MemberSymbol)t).Base;

			Assert.That (baseType, Is.TypeOf<ClassType> ());
			var objectClass = baseType as ClassType;

			Assert.That (objectClass.HasModifier (DTokens.Const));
		}

		[Test]
		public void ConstAttributedSymbolType_MemberFunctionAttributeDecl(){
			AbstractType t;
			IExpression x;
			DModule A;

			var code = "module A; const(Object) co;";

			var ctxt = CreateDefCtxt ("A", out A, code);

			x = DParser.ParseExpression ("co");

			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf<MemberSymbol>());
			var baseType = ((MemberSymbol)t).Base;

			Assert.That (baseType, Is.TypeOf<ClassType> ());
			var objectClass = baseType as ClassType;

			Assert.That (objectClass.HasModifier (DTokens.Const));
		}

		readonly string constNonConstParamDistinguishingSOcode = @"module A;
class B{
auto opEquals(Object lhs, Object rhs)
{
    return lhs.opEquals(rhs) && rhs.opEquals(lhs);
}
auto opEquals(const Object lhs, const Object rhs)
{
    return opEquals(cast()lhs, cast()rhs);
}
}
B b;
Object o,o2;
const Object co,co2;
";

		[Test]
		public void ConstNonConstParamDistinguishingSO()
		{
			AbstractType t;
			IExpression x;
			DModule A;
			DClassLike B;
			DMethod opEquals1, opEquals2;
			var ctxt = CreateDefCtxt("A", out A, constNonConstParamDistinguishingSOcode);

			B = N<DClassLike>(A, "B");
			opEquals1 = B.Children[0] as DMethod;
			opEquals2 = B.Children[1] as DMethod;
			Assert.That(opEquals1, Is.Not.Null);
			Assert.That(opEquals2, Is.Not.Null);

			x = DParser.ParseExpression("b.opEquals(o,o2)");

			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false);
			Assert.That(t, Is.TypeOf<MemberSymbol>());
			Assert.That((t as MemberSymbol).Definition, Is.SameAs(opEquals1));

			Assert.That((t as MemberSymbol).Base, Is.TypeOf<PrimitiveType>());
		}

		[Test]
		public void ConstNonConstParamDistinguishingSO2 ()
		{
			AbstractType t2;
			IExpression x2;
			DModule A;
			DClassLike B;
			DMethod opEquals1, opEquals2;
			var ctxt = CreateDefCtxt ("A", out A, constNonConstParamDistinguishingSOcode);

			B = N<DClassLike> (A, "B");
			opEquals1 = B.Children [0] as DMethod;
			opEquals2 = B.Children [1] as DMethod;
			Assert.That (opEquals1, Is.Not.Null);
			Assert.That (opEquals2, Is.Not.Null);

			x2 = DParser.ParseExpression ("b.opEquals(co,co2)");

			t2 = ExpressionTypeEvaluation.EvaluateType (x2, ctxt, false);
			Assert.That (t2, Is.TypeOf<MemberSymbol> ());
			Assert.That ((t2 as MemberSymbol).Definition, Is.SameAs (opEquals2));

			Assert.That ((t2 as MemberSymbol).Base, Is.TypeOf<PrimitiveType> ());

		}

		[Test]
		public void ParamArgMatching1()
		{
			var ctxt = CreateCtxt("A", @"module A;
enum mye
{
	a,b,c
}

int foo(string s, mye en);
double* foo(string s, string ss);
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("foo(\"derp\",mye.a)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("foo(\"derp\",\"yeah\")");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(PointerType)));

			x = DParser.ParseExpression("foo(\"derp\",1.2)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.Null);
		}

		[Test]
		public void TestOverloads1()
		{
			var pcl = CreateCache(out DModule m, @"module modA;

int foo(int i) {}

class A
{
	void foo(int k) {}

	void bar()
	{
		
	}
}

");
			var A = m["A"].First() as DClassLike;
			var bar = A["bar"].First() as DMethod;
			var ctxt = CreateDefCtxt(pcl, bar, bar.Body);

			var e = DParser.ParseExpression("123.foo");

			var t = ExpressionTypeEvaluation.EvaluateType(e, ctxt, false);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.AreEqual(pcl.FirstPackage()["modA"]["foo"].First(), ((MemberSymbol)t).Definition);
		}

		/// <summary>
		/// Templated and non-template functions can now be overloaded against each other:
		/// </summary>
		[Test]
		public void TestOverloads2()
		{
			var ctxt = CreateCtxt("A", @"module A;
int* foo(T)(T t) { }
int foo(int n) { }
long longVar = 10L;");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("foo(100)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("foo(\"asdf\")");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PointerType)));

			// Integer literal 10L can be converted to int without loss of precisions.
			// Then the call matches to foo(int n).
			x = DParser.ParseExpression("foo(10L)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PrimitiveType)));

			// A runtime variable 'num' typed long is not implicitly convertible to int.
			// Then the call matches to foo(T)(T t).
			x = DParser.ParseExpression("foo(longVar)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(PointerType)));
		}

	}
}

