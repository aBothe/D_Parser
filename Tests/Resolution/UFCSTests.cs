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
			Assert.IsInstanceOf<ArrayType>(t);

			x = DParser.ParseExpression ("globI.foo()");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.IsInstanceOf<PrimitiveType>(t);

			x = DParser.ParseExpression ("globStr.writeln()");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.IsInstanceOf<PrimitiveType>(t);
			Assert.AreEqual(DTokens.Void, (t as PrimitiveType).TypeToken);
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
			Assert.IsInstanceOf<PostfixExpression_MethodCall>(x);
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOf<TemplateParameterSymbol>(t);
			Assert.IsInstanceOf<ArrayType>((t as TemplateParameterSymbol).Base);
		}

		[Test]
		public void NoParenthesesUfcsCall()
		{
			var ctxt = ResolutionTestHelper.CreateCtxt("A", @"module A;
int foo() { return 123; }
string foo(string s) { return s ~ ""gh""; }
");

			IExpression x;
			ISymbolValue v;
			ArrayValue av;

			x = DParser.ParseExpression("\"asdf\".foo");
			v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<ArrayValue>(v);
			av = v as ArrayValue;

			Assert.IsTrue(av.IsString);
			Assert.AreEqual("asdfgh", av.StringValue);
		}

	}
}
