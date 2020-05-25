using Microsoft.VisualStudio.TestTools.UnitTesting;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.ExpressionSemantics;

namespace Tests
{
	[TestClass]
	public class UFCSTests
	{
		[TestMethod]
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
			Assert.IsInstanceOfType(t, typeof(ArrayType));

			x = DParser.ParseExpression ("globI.foo()");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.IsInstanceOfType(t, typeof(PrimitiveType));

			x = DParser.ParseExpression ("globStr.writeln()");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.IsInstanceOfType(t, typeof(PrimitiveType));
			Assert.AreEqual(DTokens.Void, (t as PrimitiveType).TypeToken);
		}

		[TestMethod]
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
			Assert.IsInstanceOfType(x, typeof(PostfixExpression_MethodCall));
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(TemplateParameterSymbol));
			Assert.IsInstanceOfType((t as TemplateParameterSymbol).Base, typeof(ArrayType));
		}

		[TestMethod]
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

			Assert.IsInstanceOfType(v, typeof(ArrayValue));
			av = v as ArrayValue;

			Assert.IsTrue(av.IsString);
			Assert.AreEqual("asdfgh", av.StringValue);
		}

	}
}
