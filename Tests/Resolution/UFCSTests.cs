using NUnit.Framework;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.ExpressionSemantics;

namespace Tests
{
	[TestFixture]
	public class UFCSTests
	{
		[Test]
		public void BasicResolution()
		{
			var ctxt = ResolutionTestHelper.CreateCtxt("modA",@"module modA;
void writeln(T...)(T t) {}
int[] foo(string a) {}
int foo(int a) {}

string globStr;
int globI;
");
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression ("globStr.foo()");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.TypeOf (typeof(ArrayType)));

			x = DParser.ParseExpression ("globI.foo()");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));

			x = DParser.ParseExpression ("globStr.writeln()");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));
			Assert.That ((t as PrimitiveType).TypeToken, Is.EqualTo(DTokens.Void));
		}

		[Test]
		public void AccessUFCS()
		{
			var ctxt = ResolutionTestHelper.CreateCtxt("A", @"module A;
template to(T)
{
    T to(A...)(A args)
    {
        return toImpl!T(args);
    }
}
int a;
");
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("a.to!string()");
			Assert.That(x, Is.TypeOf(typeof(PostfixExpression_MethodCall)));
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(ArrayType)));
		}

	}
}
