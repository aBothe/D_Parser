using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.ExpressionSemantics;
using NUnit.Framework;

namespace Tests.Resolution
{
	[TestFixture]
	public class CtfeTests
	{
		[Test]
		public void ReturnStmt()
		{
			var ctxt = ResolutionTestHelper.CreateCtxt("A", @"module A;
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
			var ctxt = ResolutionTestHelper.CreateCtxt("A", @"module A;
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
			var ctxt = ResolutionTestHelper.CreateCtxt("A", @"module A;
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

		[Test]
		[Ignore("CTFE not fully there yet")]
		public void stdPathDirnameCTFE()
		{
			var ctxt = ResolutionTestHelper.CreateDefCtxt(@"
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
