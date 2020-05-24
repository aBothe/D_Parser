using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using NUnit.Framework;

namespace Tests.ExpressionEvaluation
{
	[TestFixture]
	public class TraitsEvaluationTests
	{
		[Test]
		public void Traits()
		{
			var pcl = ResolutionTestHelper.CreateCache(out DModule m, @"module A;
int i;
string s;

abstract class C { int foo(); }
class NC { int foo(); }

C c;
int[] dynArr;
int[5] statArr;
auto assocArr = ['c' : 23, 'b' : 84];

struct S {
  void bar() { }
  void bar(int i) {}
  void bar(string s) {}
  static int statInt;
}

class D {
	void bar() { }
	abstract void absBar();
	static void statFoo() {}
	final void finBar() {};
	private int privInt;
	package int packInt;
}

class E : D{
	final override void absBar()
	{
		
	}
	
	final void bar() {}
}

interface I {
  void bar();
}

template Tmpl(){
	void bar();
}
", @"module std.someStd;");
			var ctxt = ResolutionTestHelper.CreateDefCtxt(pcl, m, null);

			BoolTrait(ctxt, "isArithmetic, int");
			BoolTrait(ctxt, "isArithmetic, i, i+1, int");
			BoolTrait(ctxt, "isArithmetic", false);
			BoolTrait(ctxt, "isArithmetic, int*", false);
			BoolTrait(ctxt, "isArithmetic, s, 123", false);
			BoolTrait(ctxt, "isArithmetic, 123, s", false);

			BoolTrait(ctxt, "isAbstractClass, C, c");
			BoolTrait(ctxt, "isAbstractClass, C");
			BoolTrait(ctxt, "isAbstractClass, int", false);
			BoolTrait(ctxt, "isAbstractClass, NC", false);
			BoolTrait(ctxt, "isAbstractClass", false);

			BoolTrait(ctxt, "isAssociativeArray, assocArr");
			BoolTrait(ctxt, "isAssociativeArray, dynArr", false);
			BoolTrait(ctxt, "isStaticArray, statArr");
			BoolTrait(ctxt, "isStaticArray, dynArr", false);

			BoolTrait(ctxt, "isVirtualMethod, D.bar");
			BoolTrait(ctxt, "isVirtualMethod, D.absBar");
			BoolTrait(ctxt, "isVirtualMethod, I.bar");
			BoolTrait(ctxt, "isVirtualMethod, Tmpl!().bar");
			//BoolTrait(ctxt, "isVirtualMethod, E.bar");
			//BoolTrait(ctxt, "isVirtualMethod, E.absBar");
			BoolTrait(ctxt, "isVirtualMethod, S.bar", false);
			BoolTrait(ctxt, "isVirtualMethod, D.statFoo", false);
			BoolTrait(ctxt, "isVirtualMethod, D.finBar", false);

			BoolTrait(ctxt, "isVirtualFunction, D.bar");
			BoolTrait(ctxt, "isVirtualFunction, D.absBar");
			BoolTrait(ctxt, "isVirtualFunction, I.bar");
			BoolTrait(ctxt, "isVirtualFunction, Tmpl!().bar");
			//BoolTrait(ctxt, "isVirtualFunction, E.bar");
			//BoolTrait(ctxt, "isVirtualFunction, E.absBar");
			BoolTrait(ctxt, "isVirtualFunction, S.bar", false);
			BoolTrait(ctxt, "isVirtualFunction, D.statFoo", false);
			BoolTrait(ctxt, "isVirtualFunction, D.finBar");

			BoolTrait(ctxt, "hasMember, C, \"foo\"");
			BoolTrait(ctxt, "hasMember, c, \"foo\"");
			BoolTrait(ctxt, "hasMember, C, \"noFoo\"", false);
			BoolTrait(ctxt, "hasMember, int, \"sizeof\"");

			var x = DParser.ParseExpression(@"__traits(identifier, C.aso.derp)");
			var v = D_Parser.Resolver.ExpressionSemantics.Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(ArrayValue)));
			var av = v as ArrayValue;
			Assert.That(av.IsString, Is.True);
			Assert.That(av.StringValue, Is.EqualTo("C.aso.derp"));

			x = DParser.ParseExpression("__traits(getMember, c, \"foo\")");
			var t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));



			x = DParser.ParseExpression("__traits(getOverloads, S, \"bar\")");
			v = D_Parser.Resolver.ExpressionSemantics.Evaluation.EvaluateValue(x, ctxt);
			Assert.That(v, Is.TypeOf(typeof(TypeValue)));
			Assert.That((v as TypeValue).RepresentedType, Is.TypeOf(typeof(DTuple)));

			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(DTuple)));


			x = DParser.ParseExpression("__traits(getProtection, D.privInt)");
			v = D_Parser.Resolver.ExpressionSemantics.Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(ArrayValue)));
			av = v as ArrayValue;
			Assert.That(av.IsString, Is.True);
			Assert.That(av.StringValue, Is.EqualTo("private"));

			x = DParser.ParseExpression("__traits(getProtection, D)");
			v = D_Parser.Resolver.ExpressionSemantics.Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(ArrayValue)));
			av = v as ArrayValue;
			Assert.That(av.IsString, Is.True);
			Assert.That(av.StringValue, Is.EqualTo("public"));

			x = DParser.ParseExpression("__traits(getProtection, D.packInt)");
			v = D_Parser.Resolver.ExpressionSemantics.Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(ArrayValue)));
			av = v as ArrayValue;
			Assert.That(av.IsString, Is.True);
			Assert.That(av.StringValue, Is.EqualTo("package"));

			BoolTrait(ctxt, "isSame, int, int");
			BoolTrait(ctxt, "isSame, int, double", false);
			BoolTrait(ctxt, "isSame, C, D", false);
			BoolTrait(ctxt, "isSame, D, D");

			BoolTrait(ctxt, "compiles", false);
			BoolTrait(ctxt, "compiles, asd.herp", false);
			BoolTrait(ctxt, "compiles, i");
			BoolTrait(ctxt, "compiles, i + 1");
			//BoolTrait(ctxt, "compiles, &i + 1", false); //TODO: Check if both operand types match..is this still efficient?
			BoolTrait(ctxt, "compiles, typeof(1)");
			BoolTrait(ctxt, "compiles, S.nonExistingItem", false); //TODO: Make the resolver not resolve non-static items implicitly (i.e. without explicit resolution option)
			BoolTrait(ctxt, "compiles, S.statInt");
			BoolTrait(ctxt, "compiles, 1,2,3,int,long,std");
			BoolTrait(ctxt, "compiles, 1,2,3,int,long,3[1]", false);
			BoolTrait(ctxt, "compiles, 3[1]", false);
			BoolTrait(ctxt, "compiles, immutable(S44)(3, &i)", false);
		}

		void BoolTrait(ResolutionContext ctxt, string traitCode, bool shallReturnTrue = true)
		{
			var x = DParser.ParseExpression("__traits(" + traitCode + ")");
			var v = D_Parser.Resolver.ExpressionSemantics.Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			Assert.That((v as PrimitiveValue).BaseTypeToken, Is.EqualTo(DTokens.Bool));
			Assert.That((v as PrimitiveValue).Value, Is.EqualTo(shallReturnTrue ? 1m : 0m));
		}
	}
}
