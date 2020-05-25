using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Resolution
{
	[TestClass]
	public class OperatorOverloadingTests : ResolutionTestHelper
	{
		[TestMethod]
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
				Arguments = new[] { new ScalarConstantExpression(123m, LiteralFormat.Scalar) },
				PostfixForeExpression = new PostfixExpression_Access
				{
					AccessExpression = new IdentifierExpression("bar"),
					PostfixForeExpression = new IdentifierExpression("loc") { Location = stmt_x.Location }
				}
			};

			using (ctxt.Push(main, stmt_x.Location))
				ds = ExpressionTypeEvaluation.EvaluateType(x, ctxt) as DSymbol;
			Assert.IsInstanceOfType(ds, typeof(TemplateParameterSymbol));
			Assert.IsInstanceOfType(ds.Base, typeof(PrimitiveType));

			x = DParser.ParseExpression("s2.bar(s)");
			ds = ExpressionTypeEvaluation.EvaluateType(x, ctxt) as DSymbol;
			Assert.IsInstanceOfType(ds, typeof(TemplateParameterSymbol));
			Assert.IsInstanceOfType(ds.Base, typeof(ClassType));

			x = DParser.ParseExpression("s.foo(123)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(PointerType));

			x = DParser.ParseExpression("s.foo");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(PrimitiveType));

			x = DParser.ParseExpression("D.foo");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			Assert.IsInstanceOfType((t as MemberSymbol).Base, typeof(PrimitiveType));

			v = Evaluation.EvaluateValue(x, ctxt);
			Assert.IsInstanceOfType(v, typeof(PrimitiveValue));
			Assert.AreEqual(8m, (v as PrimitiveValue).Value);

			td = DParser.ParseBasicType("D.foo");
			t = RS(td, ctxt);
			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			Assert.IsInstanceOfType((t as MemberSymbol).Base, typeof(PrimitiveType));
		}

		[TestMethod]
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
			Assert.IsInstanceOfType(t, typeof(TemplateParameterSymbol));
			Assert.IsInstanceOfType((t as DerivedDataType).Base, typeof(PrimitiveType));

			x = DParser.ParseExpression("s[1..3]");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(PointerType));
			t = (t as PointerType).Base;
			Assert.IsInstanceOfType(t, typeof(TemplateParameterSymbol));
			Assert.IsInstanceOfType((t as DerivedDataType).Base, typeof(PrimitiveType));
		}

		[TestMethod]
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
			Assert.IsInstanceOfType(t, typeof(TemplateParameterSymbol));
			Assert.IsInstanceOfType((t as DerivedDataType).Base, typeof(PrimitiveType));

			x = DParser.ParseExpression("s[1,2]");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(ArrayType));

			x = DParser.ParseExpression("s[1,2,3]");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(PointerType));
		}
	}
}
