using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.ExpressionSemantics;
using NUnit.Framework;
using Tests.ExpressionEvaluation;

namespace Tests.Resolution
{
	[TestFixture]
	public class CtfeTests : ResolutionTestHelper
	{
		[Test]
		public void ReturnStmt()
		{
			var ctxt = CreateCtxt("A", @"module A;
string inty(A)() { return ""int y;""; }
");

			IExpression x;
			ISymbolValue v;
			ArrayValue av;
			PrimitiveValue pv;

			x = DParser.ParseExpression("inty!(\"asdf\")");
			v = Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(ArrayValue)));
			av = v as ArrayValue;
			Assert.That(av.IsString, Is.True);
			Assert.That(av.StringValue, Is.EqualTo("int y;"));
		}

		[Test]
		public void ReturnStmt2()
		{
			var ctxt = CreateCtxt("A", @"module A;
int foo() { return 123; }
string foo(string s) { return s ~ ""gh""; }
");

			IExpression x;
			ISymbolValue v;
			ArrayValue av;

			x = DParser.ParseExpression("foo(\"asdf\")");
			v = Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(ArrayValue)));
			av = v as ArrayValue;
			Assert.That(av.IsString, Is.True);
			Assert.That(av.StringValue, Is.EqualTo("asdfgh"));
		}

		[Test]
		public void ReturnStmt3()
		{
			var ctxt = CreateCtxt("A", @"module A;
int foo() { return 123; }
string foo(string s) { return s ~ ""gh""; }
");

			IExpression x;
			ISymbolValue v;
			PrimitiveValue pv;

			x = DParser.ParseExpression("foo");
			v = Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			pv = v as PrimitiveValue;
			Assert.That(pv.BaseTypeToken, Is.EqualTo(DTokens.Int));
			Assert.That(pv.Value, Is.EqualTo(123M));
		}

		private const string ctfe_ifStatement = @"module A;
bool youDecide(int a) {
	if(a > 25)
		return true;
	else {
		return false;
	}
}";

		[Test]
		public void IfStatement_PositiveCase()
		{
			var ctxt = CreateDefCtxt(ctfe_ifStatement);

			var x = DParser.ParseExpression("youDecide(30)");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			var pv = v as PrimitiveValue;
			Assert.That(pv.BaseTypeToken, Is.EqualTo(DTokens.Bool));
			Assert.That(pv.Value, Is.EqualTo(1m));
		}

		[Test]
		public void IfStatement_ElseCase()
		{
			var ctxt = CreateDefCtxt(ctfe_ifStatement);

			var x = DParser.ParseExpression("youDecide(0)");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			var pv = v as PrimitiveValue;
			Assert.That(pv.BaseTypeToken, Is.EqualTo(DTokens.Bool));
			Assert.That(pv.Value, Is.EqualTo(0m));
		}

		[Test]
		public void WhileStatement()
		{
			var ctxt = CreateDefCtxt(@"module A;
int whileReturn() {
	while(true){
		return 3;
	}
	return 1;
}");

			var x = DParser.ParseExpression("whileReturn()");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			var pv = v as PrimitiveValue;
			Assert.That(pv.BaseTypeToken, Is.EqualTo(DTokens.Int));
			Assert.That(pv.Value, Is.EqualTo(3m));
		}

		[Test]
		public void VoidReturnValue_ImplicitReturn()
		{
			var ctxt = CreateDefCtxt(@"module A;
void returnvoid() {
}");

			var x = DParser.ParseExpression("returnvoid()");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(VoidValue)));
		}

		[Test]
		public void VoidReturnValue_ExplicitReturn()
		{
			var ctxt = CreateDefCtxt(@"module A;
void returnvoid() {
	return;
}");

			var x = DParser.ParseExpression("returnvoid()");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(VoidValue)));
		}

		[Test]
		public void ReturnVariableContent()
		{
			var ctxt = CreateDefCtxt(@"module A;
int keks(int a) {
	return a;
}");

			var x = DParser.ParseExpression("keks(123)");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			var pv = v as PrimitiveValue;
			Assert.That(pv.BaseTypeToken, Is.EqualTo(DTokens.Int));
			Assert.That(pv.Value, Is.EqualTo(123m));
		}

		[Test]
		public void VariableValueAssignment()
		{
			var ctxt = CreateDefCtxt(@"module A;
int keks(int a) {
	a = 123;
	return a;
}");

			var x = DParser.ParseExpression("keks(0)");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			var pv = v as PrimitiveValue;
			Assert.That(pv.BaseTypeToken, Is.EqualTo(DTokens.Int));
			Assert.That(pv.Value, Is.EqualTo(123m));
		}

		[Test]
		public void VariableValueAssignment2()
		{
			var ctxt = CreateDefCtxt(@"module A;
int keks(int a) {
	a = 137 + -a;
	return a;
}");

			var x = DParser.ParseExpression("keks(7)");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			var pv = v as PrimitiveValue;
			Assert.That(pv.BaseTypeToken, Is.EqualTo(DTokens.Int));
			Assert.That(pv.Value, Is.EqualTo(130m));
		}

		[Test]
		[Ignore("CTFE not fully there yet")]
		public void stdPathDirnameCTFE()
		{
			var ctxt = CreateDefCtxt(@"
string _dirName(string s)
{
    string p = s;
    while (p.length > 0)
    {
        if (p[$-1] == '/' || p[$-1] == '\\')
            return p[0..$-1];
        p = p[0..$-1];
    }
    return s;
}

enum dir = _dirName(""dir/someFile"");
enum dir_windows = _dirName(""dir\someFile"");
enum filename = dir ~ ""/myFile"";
");

			{
				var x = DParser.ParseExpression("dir");
				var v = Evaluation.EvaluateValue(x, ctxt);

				Assert.That(v, Is.TypeOf(typeof(ArrayValue)));
				var av = v as ArrayValue;
				Assert.That(av.IsString);
				Assert.That(av.StringValue, Is.EqualTo("dir"));
			}

			{
				// Mind caching issues!
				var x = DParser.ParseExpression("dir_windows");
				var v = Evaluation.EvaluateValue(x, ctxt);

				Assert.That(v, Is.TypeOf(typeof(ArrayValue)));
				var av = v as ArrayValue;
				Assert.That(av.IsString);
				Assert.That(av.StringValue, Is.EqualTo("dir"));
			}

			{
				var x = DParser.ParseExpression("filename");
				var v = Evaluation.EvaluateValue(x, ctxt);

				Assert.That(v, Is.TypeOf(typeof(ArrayValue)));
				var av = v as ArrayValue;
				Assert.That(av.IsString);
				Assert.That(av.StringValue, Is.EqualTo("dir/myFile"));
			}
		}
	}
}
