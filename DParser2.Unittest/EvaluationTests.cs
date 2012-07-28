using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Unittest;
using D_Parser.Resolver.ExpressionSemantics;

namespace DParser2.Unittest
{
	[TestClass]
	public class EvaluationTests
	{
		public static ISymbolValue E(string expression, ISymbolValueProvider vp=null)
		{
			return Evaluation.EvaluateValue(DParser.ParseExpression(expression), vp);
		}

		public static PrimitiveValue GetPrimitiveValue(string literalCode,ISymbolValueProvider vp=null)
		{
			var v = E(literalCode,vp);

			Assert.IsInstanceOfType(v, typeof(PrimitiveValue));
			return (PrimitiveValue)v;
		}

		public static void TestPrimitive(string literal, int btToken, object val, ISymbolValueProvider vp=null)
		{
			var pv = GetPrimitiveValue(literal,vp);

			Assert.AreEqual(pv.BaseTypeToken, btToken);
			Assert.AreEqual(pv.Value, val);
		}

		public static void TestString(string literal, string content, bool ProvideObjModule = true)
		{
			ResolverContextStack ctxt = null;

			if (ProvideObjModule)
				ctxt = new ResolverContextStack(ResolutionTests.CreateCache(), new ResolverContext());

			var x = DParser.ParseExpression(literal);

			Assert.IsInstanceOfType(x, typeof(IdentifierExpression));
			var id = (IdentifierExpression)x;

			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOfType(v, typeof(ArrayValue));
			var av = (ArrayValue)v;
			Assert.IsTrue(av.IsString);

			Assert.AreEqual(av.StringValue, content);

			Assert.IsInstanceOfType(av.RepresentedType,typeof(ArrayType));
			var ar = (ArrayType)av.RepresentedType;

			switch (id.Subformat)
			{
				case LiteralSubformat.Utf8:
					Assert.AreEqual(ar.DeclarationOrExpressionBase.ToString(),"immutable(char)[]");
					break;
				case LiteralSubformat.Utf16:
					Assert.AreEqual(ar.DeclarationOrExpressionBase.ToString(), "immutable(wchar)[]");
					break;
				case LiteralSubformat.Utf32:
					Assert.AreEqual(ar.DeclarationOrExpressionBase.ToString(), "immutable(dchar)[]");
					break;
				default:
					Assert.Fail();
					return;
			}
		}

		public static void TestBool(string literal, bool v = true, ISymbolValueProvider vp =null)
		{
			var pv = GetPrimitiveValue(literal, vp);

			Assert.AreEqual(DTokens.Bool, pv.BaseTypeToken);

			if (v)
				Assert.AreEqual(1M, pv.Value,  literal +" must be true");
			else
				Assert.AreEqual(0M, pv.Value, literal + " must be false");
		}

		[TestMethod]
		public void TestMathOperations()
		{
			TestPrimitive("0", DTokens.Int, 0M);
			TestPrimitive("-1", DTokens.Int, -1M);
			TestPrimitive("-(1+4)", DTokens.Int, -5M);

			TestPrimitive("1+2", DTokens.Int, 3M);
			TestPrimitive("1-2", DTokens.Int, -1M);
			TestPrimitive("3*4", DTokens.Int, 12M);
			TestPrimitive("3/4", DTokens.Int, 0.75M);
			TestPrimitive("35 % 2", DTokens.Int, 1M);

			TestPrimitive("3*4 + 5", DTokens.Int, 17M);
			TestPrimitive("3+5*4", DTokens.Int, 23M);
		}

		[TestMethod]
		public void TestBooleanOps()
		{
			TestBool("true");
			TestBool("false || false", false);
			TestBool("true || false");
			TestBool("false || true");
			TestBool("true || true");
			TestBool("false && false", false);
			TestBool("false && true", false);
			TestBool("true && false", false);
			TestBool("true && true");

			TestBool("1==1");
			TestBool("0==0");
			TestBool("1!=1", false);
			TestBool("1!=0");

			TestBool("3 > 1");
			TestBool("1 > 2", false);
			TestBool("1 >= 0");
			TestBool("1 >= 1");
			TestBool("1 >= 2", false);
			TestBool("1 < 10");
			TestBool("3 < 1", false);
			TestBool("1 <= 2");
			TestBool("1 <= 1");
			TestBool("1 <= 0", false);

			//TestBool("float.nan !<>= 2");
			TestBool("2.3 <> 2.911");
			TestBool("2.1 <> 2.2");
			TestBool("2.3 <> 2.3", false);
			TestBool("1.4 <>= 1.3");
			TestBool("1.4 <>= 1.4");
			TestBool("1.4 <>= 1.5");
			//TestBool("float.nan <>= 3", false);

			TestBool("3 !> 1", false);
			TestBool("1 !> 2");
			TestBool("1 !>= 0", false);
			TestBool("1 !>= 1", false);
			TestBool("1 !>= 2");
			TestBool("1 !< 10", false);
			TestBool("3 !< 1");
			TestBool("3 !< 3");
			TestBool("1 !<= 2", false);
			TestBool("1 !<= 1", false);
			TestBool("1 !<= 0");
			//TestBool("float.nan !<= 3");
			TestBool("1.4 !<> 1.4");
			TestBool("1.4 !<> 1.5", false);

			TestBool("true ? true : false");
			TestBool("false ? true : false", false);
			TestBool("1 == 1 ? true : 2 == 1");
			TestBool("false && true ? false : true");
			TestBool("false && (true ? false: true)", false);
		}

		[TestMethod]
		public void TestPrimitives()
		{
			TestPrimitive("1", DTokens.Int, 1M);
			TestPrimitive("1.0", DTokens.Double, 1.0M);
			TestPrimitive("1f",DTokens.Float, 1M);
			TestPrimitive("1e+3", DTokens.Int, 1000M);
			TestPrimitive("1.0e+2", DTokens.Double, 100M);
			TestPrimitive("'c'",DTokens.Char, (decimal)(int)'c');

			TestString("\"asdf\"", "asdf", true);
			TestString("\"asdf\"c", "asdf", true);
			TestString("\"asdf\"w", "asdf", true);
			TestString("\"asdf\"d", "asdf", true);

			TestString("\"asdf\"", "asdf", false);
			TestString("\"asdf\"c", "asdf", false);
			TestString("\"asdf\"w", "asdf", false);
			TestString("\"asdf\"d", "asdf", false);

			var ex = DParser.ParseExpression("['a','s','d','f']");
			var v = Evaluation.EvaluateValue(ex, (ResolverContextStack)null);

			Assert.IsInstanceOfType(v, typeof(ArrayValue));
			var ar = (ArrayValue)v;
			Assert.AreEqual(ar.Elements.Length, 4);

			foreach (var ev in ar.Elements)
				Assert.IsInstanceOfType(ev,typeof(PrimitiveValue));


			ex = DParser.ParseExpression("[\"KeyA\":12, \"KeyB\":33, \"KeyC\":44]");
			v = Evaluation.EvaluateValue(ex, (ResolverContextStack)null);

			Assert.IsInstanceOfType(v, typeof(AssociativeArrayValue));
			var aa = (AssociativeArrayValue)v;
			Assert.AreEqual(aa.Elements.Count, 3);
		}

		[TestMethod]
		public void TestConstEval()
		{
			var pcl = ResolutionTests.CreateCache(@"module modA;
const a= 234;
enum b=123;
const int c=125;
enum int d=126;
");
			var vp = new StandardValueProvider(new ResolverContextStack(pcl, new ResolverContext { ScopedBlock=pcl[0]["modA"] }));

			TestPrimitive("a", DTokens.Int, 234M, vp);
			TestPrimitive("b", DTokens.Int, 123M, vp);
			TestPrimitive("c", DTokens.Int, 125M, vp);
			TestPrimitive("d", DTokens.Int, 126M, vp);

		}

		[TestMethod]
		public void TestArrays()
		{
			var vp = new StandardValueProvider(null);

			var v = E("[11,22,33,43][1]", vp);
			Assert.IsInstanceOfType(v, typeof(PrimitiveValue));
			Assert.AreEqual(((PrimitiveValue)v).Value, 22);

			v = E("[11,22,33,44,55,66,77,88,99,100][1..3]", vp);

			Assert.IsInstanceOfType(v, typeof(ArrayValue));
			var av=(ArrayValue)v;
			Assert.AreEqual(av.Elements.Length,2);
			Assert.AreEqual((av.Elements[0] as PrimitiveValue).Value,22);
		}

		[TestMethod]
		public void TestAccessExpression()
		{
			var pcl = ResolutionTests.CreateCache(@"module modA;

class A
{
	const int someProp=3;
}

A a;");

			var vp = new StandardValueProvider(new ResolverContextStack(pcl, new ResolverContext{ ScopedBlock = pcl[0]["modA"] }));
			/*
			var v = E("a.someProp", vp);
			Assert.IsInstanceOfType(v, typeof(PrimitiveValue));
			Assert.AreEqual(((PrimitiveValue)v).Value,3);
			*/
			var v = E("A.someProp", vp);
			Assert.IsInstanceOfType(v, typeof(PrimitiveValue));
			Assert.AreEqual(((PrimitiveValue)v).Value, 3);
		}

		public static bool EvalIsExpression(string IsExpressionCode, ISymbolValueProvider vp)
		{
			var e = DParser.ParseExpression("is("+IsExpressionCode+")");

			var v = Evaluation.EvaluateValue(e, vp);

			Assert.IsInstanceOfType(v, typeof(PrimitiveValue));
			var pv = (PrimitiveValue)v;
			Assert.AreEqual(pv.BaseTypeToken, DTokens.Bool, "Type of 'is(" + IsExpressionCode + ")' result must be bool");
			return pv.Value == 1M;
		}

		[TestMethod]
		public void TestIsExpression()
		{
			var pcl = ResolutionTests.CreateCache(@"module modA;
class A {}
class B : A {}
class C : A {}
");

			var vp = new StandardValueProvider(new ResolverContextStack(pcl, new ResolverContext { ScopedBlock=pcl[0]["modA"] }));

			Assert.IsTrue(EvalIsExpression("char*[] T : U[], U : V*, V", vp));
			Assert.IsTrue(EvalIsExpression("string T : U[], U : immutable(V), V : char", vp));
			Assert.IsFalse(EvalIsExpression("int[10] X : X[Y], int Y : 5",vp));

			Assert.IsTrue(EvalIsExpression("bool : bool", vp));
			Assert.IsTrue(EvalIsExpression("bool == bool", vp));
			Assert.IsTrue(EvalIsExpression("C : A", vp));
			Assert.IsTrue(EvalIsExpression("C : C", vp));
			Assert.IsFalse(EvalIsExpression("C == A", vp));
			Assert.IsTrue(EvalIsExpression("immutable(char) == immutable", vp));
			Assert.IsFalse(EvalIsExpression("string == immutable", vp));
			Assert.IsTrue(EvalIsExpression("A == class", vp));
		}
	}
}
