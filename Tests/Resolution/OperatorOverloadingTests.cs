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
	public class OperatorOverloadingTests : ResolutionTestHelper
	{
		[Test]
		public void opDispatch()
		{
			var ctxt = CreateCtxt("A", @"module A;
class S {
	int* opDispatch(string s)(int i){ }
	int opDispatch(string s)(){ }
}

struct S2 {
  T opDispatch(string s, T)(T i){ return i; }
}

struct S3 {
  static int opDispatch(string s)(){ }
}

struct D {
  template opDispatch(string s) {
    enum int opDispatch = 8;
  }
}

S s;
S2 s2;
S3 s3;

void main() {
  S2 loc;
	x;
}");
			AbstractType t;
			DSymbol ds;
			IExpression x;
			ITypeDeclaration td;
			ISymbolValue v;


			var main = ctxt.MainPackage()["A"]["main"].First() as DMethod;
			var stmt_x = main.Body.SubStatements.ElementAt(1);

			x = new PostfixExpression_MethodCall
			{
				Arguments = new[] { new IdentifierExpression(123m, LiteralFormat.Scalar) },
				PostfixForeExpression = new PostfixExpression_Access
				{
					AccessExpression = new IdentifierExpression("bar"),
					PostfixForeExpression = new IdentifierExpression("loc") { Location = stmt_x.Location }
				}
			};

			using (ctxt.Push(main, stmt_x.Location))
				ds = ExpressionTypeEvaluation.EvaluateType(x, ctxt) as DSymbol;
			Assert.That(ds, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That(ds.Base, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("s2.bar(s)");
			ds = ExpressionTypeEvaluation.EvaluateType(x, ctxt) as DSymbol;
			Assert.That(ds, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That(ds.Base, Is.TypeOf(typeof(ClassType)));

			x = DParser.ParseExpression("s.foo(123)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(PointerType)));

			x = DParser.ParseExpression("s.foo");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("D.foo");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));

			v = Evaluation.EvaluateValue(x, ctxt);
			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			Assert.That((v as PrimitiveValue).Value, Is.EqualTo(8m));

			td = DParser.ParseBasicType("D.foo");
			t = RS(td, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void opSlice()
		{
			var ctxt = CreateCtxt("A", @"module A;

struct S(T)
{
	T opSlice() {}
	int[] opSlice(int dope);
	T* opSlice(U)(U x, size_t y); // overloads a[i .. j]
}

S!int s;
");
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("s[]");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((t as DerivedDataType).Base, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("s[1..3]");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(PointerType)));
			t = (t as PointerType).Base;
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((t as DerivedDataType).Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void opIndex()
		{
			var ctxt = CreateCtxt("A", @"module A;

struct S(T)
{
	T opIndex(size_t i) {}
	int[] opIndex(int j,int k);
	int* opIndex(int j, int k, int l);
}

S!int s;
");
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("s[1]");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((t as DerivedDataType).Base, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("s[1,2]");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(ArrayType)));

			x = DParser.ParseExpression("s[1,2,3]");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(PointerType)));
		}
	}
}
