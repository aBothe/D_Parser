using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.ExpressionSemantics.Exceptions;
using D_Parser.Resolver.Model;
using NUnit.Framework;

namespace Tests.ExpressionEvaluation
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

			Assert.IsInstanceOf<ArrayValue>(v);
			av = v as ArrayValue;
			Assert.IsTrue(av.IsString);
			Assert.AreEqual("int y;", av.StringValue);
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

			Assert.IsInstanceOf<ArrayValue>(v);
			av = v as ArrayValue;
			Assert.IsTrue(av.IsString);
			Assert.AreEqual("asdfgh", av.StringValue);
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

			Assert.IsInstanceOf<PrimitiveValue>(v);
			pv = v as PrimitiveValue;
			Assert.AreEqual(DTokens.Int, pv.BaseTypeToken);
			Assert.AreEqual(123M, pv.Value);
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

			Assert.IsInstanceOf<PrimitiveValue>(v);
			var pv = v as PrimitiveValue;
			Assert.AreEqual(DTokens.Bool, pv.BaseTypeToken);
			Assert.AreEqual(1m, pv.Value);
		}

		[Test]
		public void IfStatement_ElseCase()
		{
			var ctxt = CreateDefCtxt(ctfe_ifStatement);

			var x = DParser.ParseExpression("youDecide(0)");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<PrimitiveValue>(v);
			var pv = v as PrimitiveValue;
			Assert.AreEqual(DTokens.Bool, pv.BaseTypeToken);
			Assert.AreEqual(0m, pv.Value);
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

			Assert.IsInstanceOf<PrimitiveValue>(v);
			var pv = v as PrimitiveValue;
			Assert.AreEqual(DTokens.Int, pv.BaseTypeToken);
			Assert.AreEqual(3m, pv.Value);
		}

		[Test]
		public void VoidReturnValue_ImplicitReturn()
		{
			var ctxt = CreateDefCtxt(@"module A;
void returnvoid() {
}");

			var x = DParser.ParseExpression("returnvoid()");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<VoidValue>(v);
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

			Assert.IsInstanceOf<VoidValue>(v);
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

			Assert.IsInstanceOf<PrimitiveValue>(v);
			var pv = v as PrimitiveValue;
			Assert.AreEqual(DTokens.Int, pv.BaseTypeToken);
			Assert.AreEqual(123m, pv.Value);
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

			Assert.IsInstanceOf<PrimitiveValue>(v);
			var pv = v as PrimitiveValue;
			Assert.AreEqual(DTokens.Int, pv.BaseTypeToken);
			Assert.AreEqual(123m, pv.Value);
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

			Assert.IsInstanceOf<PrimitiveValue>(v);
			var pv = v as PrimitiveValue;
			Assert.AreEqual(DTokens.Int, pv.BaseTypeToken);
			Assert.AreEqual(130m, pv.Value);
		}

		[Test]
		public void VariableDeclarationDefinition()
		{
			var ctxt = CreateDefCtxt(@"module A;
string keks() {
	auto p = `asdf`;
	return p;
}");

			var x = DParser.ParseExpression("keks()");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<ArrayValue>(v);
			var av = v as ArrayValue;
			Assert.IsTrue(av.IsString);
			Assert.AreEqual("asdf", av.StringValue);
		}

		[Test]
		public void VariableUnrefencingWhileAssigning()
		{
			var ctxt = CreateDefCtxt(@"module A;
string keks() {
	auto p = `asdf`;
	auto s = p;
	p = ``;
	return s;
}");

			var x = DParser.ParseExpression("keks()");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<ArrayValue>(v);
			var av = v as ArrayValue;
			Assert.IsTrue(av.IsString);
			Assert.AreEqual("asdf", av.StringValue);
		}

		[Test]
		public void VariableDeclarationDefinition_UndefinedValue()
		{
			var ctxt = CreateDefCtxt(@"module A;
string keks() {
	string p;
	return p;
}");

			var x = DParser.ParseExpression("keks()");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<ErrorValue>(v);
			var ev = v as ErrorValue;
			Assert.IsInstanceOf<VariableNotInitializedException>(ev.Errors[0]);
		}

		[Test]
		public void VariableArrayIndexAccessing()
		{
			var ctxt = CreateDefCtxt(@"module A;
auto keks(string s) {
	return s[$-1];
}");

			var x = DParser.ParseExpression("keks(`asdf`)");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<PrimitiveValue>(v);
			var pv = v as PrimitiveValue;
			Assert.AreEqual(DTokens.Char, pv.BaseTypeToken);
			Assert.AreEqual((decimal)'f', pv.Value);
		}

		[Test]
		public void ArrayLength()
		{
			var ctxt = CreateDefCtxt(@"module A;
auto keks(string s) {
	return s.length;
}");

			var x = DParser.ParseExpression("keks(`asdf`)");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<PrimitiveValue>(v);
			var pv = v as PrimitiveValue;
			Assert.AreEqual(DTokens.Int, pv.BaseTypeToken);
			Assert.AreEqual(4m, pv.Value);
		}

		[Test]
		public void ArraySlicing()
		{
			var ctxt = CreateDefCtxt(@"module A;
string keks(string p) {
	return p[0 .. $-1];
}");

			var x = DParser.ParseExpression("keks(`asdf`)");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<ArrayValue>(v);
			var av = v as ArrayValue;
			Assert.IsTrue(av.IsString);
			Assert.AreEqual("asd", av.StringValue);
		}

		[Test]
		public void ArrayIndexComparison()
		{
			var ctxt = CreateDefCtxt(@"module A;
auto keks(string s) {
	return s[$-1] == 'f';
}");

			{
				var x = DParser.ParseExpression("keks(`asdf`)");
				var v = Evaluation.EvaluateValue(x, ctxt);

				Assert.IsInstanceOf<PrimitiveValue>(v);
				var pv = v as PrimitiveValue;
				Assert.AreEqual(DTokens.Bool, pv.BaseTypeToken);
				Assert.AreEqual(1m, pv.Value);
			}

			{
				var x = DParser.ParseExpression("keks(`asd`)");
				var v = Evaluation.EvaluateValue(x, ctxt);

				Assert.IsInstanceOf<PrimitiveValue>(v);
				var pv = v as PrimitiveValue;
				Assert.AreEqual(DTokens.Bool, pv.BaseTypeToken);
				Assert.AreEqual(0m, pv.Value);
			}
		}

		private const string dirnameCode = @"module A;
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

enum dir = _dirName(""myDir/someFile"");
enum dir_windows = _dirName(""dir\someFile"");
enum filename = dir ~ ""/myFile"";
";

		[Test]
		public void StdPathDirnameCTFE()
		{
			var ctxt = CreateDefCtxt(dirnameCode);

			var x = DParser.ParseExpression("_dirName(`myDir/someFile`)");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<ArrayValue>(v);
			var av = v as ArrayValue;
			Assert.IsTrue(av.IsString);
			Assert.AreEqual("myDir", av.StringValue);
		}

		[Test]
		public void StdPathDirnameCTFE2()
		{
			var ctxt = CreateDefCtxt(dirnameCode);

			var x = DParser.ParseExpression("dir_windows");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<ArrayValue>(v);
			var av = v as ArrayValue;
			Assert.IsTrue(av.IsString);
			Assert.AreEqual("dir", av.StringValue);
		}

		[Test]
		public void StdPathDirnameCTFE3()
		{
			var ctxt = CreateDefCtxt(dirnameCode);

			var x = DParser.ParseExpression("filename");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<ArrayValue>(v);
			var av = v as ArrayValue;
			Assert.IsTrue(av.IsString);
			Assert.AreEqual("myDir/myFile", av.StringValue);
		}

		[Test]
		public void StdPathDirnameCTFE4()
		{
			var ctxt = CreateDefCtxt(dirnameCode);

			var x = DParser.ParseExpression("dir");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<ArrayValue>(v);
			var av = v as ArrayValue;
			Assert.IsTrue(av.IsString);
			Assert.AreEqual("myDir", av.StringValue);
		}

		[Test]
		public void StackOverflowPrevention()
		{
			var ctxt = CreateDefCtxt(@"module A;
void fooByAccident() { bar(); }
void bar() { baz(); }
void baz() { fooByAccident(); }
");

			var x = DParser.ParseExpression("fooByAccident()");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<ErrorValue>(v);
			var ev = v as ErrorValue;
			Assert.IsInstanceOf<EvaluationStackOverflowException>(ev.Errors[0]);
		}

		[Test]
		public void StackOverflowPrevention2()
		{
			var ctxt = CreateDefCtxt(@"module A;
void fooByAccident() { bar(); }
void bar() { while(true) baz(); }
void baz() { fooByAccident(); }
");

			var x = DParser.ParseExpression("fooByAccident()");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<ErrorValue>(v);
			var ev = v as ErrorValue;
			Assert.IsInstanceOf<EvaluationStackOverflowException>(ev.Errors[0]);
		}

		[Test]
		public void AccessingOuterScopedConsts()
		{
			var ctxt = CreateDefCtxt(@"module A;
string keks(string p) {
	return myConst;
}
const myConst = `asdf`;
");

			var x = DParser.ParseExpression("keks(`fooo`)");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<ArrayValue>(v);
			var av = v as ArrayValue;
			Assert.IsTrue(av.IsString);
			Assert.AreEqual("asdf", av.StringValue);
		}

		[Test]
		public void StackOverflowPrevention_ConstOuterVariable()
		{
			var ctxt = CreateDefCtxt(@"module A;
const int someConst = fooByAccident();
int fooByAccident() { return bar(); }
void bar() { return someConst; }
");

			var x = DParser.ParseExpression("fooByAccident()");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<ErrorValue>(v);
			var ev = v as ErrorValue;
			Assert.IsInstanceOf<EvaluationStackOverflowException>(ev.Errors[0]);
		}

		[Test]
		public void ClassInstance()
		{
			var ctxt = CreateDefCtxt(@"module A;
class MyClass { }
MyClass keks() {
	return new MyClass();
}
");

			var x = DParser.ParseExpression("keks()");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<ComplexValue>(v);
			var cv = v as ComplexValue;
			Assert.IsInstanceOf<ClassType>(cv.RepresentedType);
		}
	}
}
