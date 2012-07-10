using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using D_Parser.Dom.Expressions;
using D_Parser.Evaluation;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Unittest;

namespace DParser2.Unittest
{
	[TestClass]
	public class EvaluationTests
	{
		public static ISymbolValue E(string expression, ISymbolValueProvider vp=null)
		{
			return ExpressionEvaluator.Evaluate(DParser.ParseExpression(expression), vp);
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

			var v = ExpressionEvaluator.Evaluate(x, new StandardValueProvider(ctxt));

			Assert.IsInstanceOfType(v, typeof(ArrayValue));
			var av = (ArrayValue)v;
			Assert.IsTrue(av.IsString);

			Assert.AreEqual(av.StringValue, content);

			Assert.IsNotNull(av.RepresentedType);
			ArrayType ar = null;

			if (ProvideObjModule)
			{
				Assert.IsInstanceOfType(av.RepresentedType, typeof(AliasedType));
				var s = av.RepresentedType as AliasedType;
				ar = (ArrayType)s.Base;
			}
			else
				ar = (ArrayType)av.RepresentedType;

			Assert.IsNotNull(ar);

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

		[TestMethod]
		public void TestPrimitives()
		{
			TestPrimitive("1", DTokens.Int, 1);
			TestPrimitive("1.0", DTokens.Double, 1.0);
			TestPrimitive("1f",DTokens.Float, 1);
			TestPrimitive("'c'",DTokens.Char, 'c');

			TestString("\"asdf\"", "asdf", true);
			TestString("\"asdf\"c", "asdf", true);
			TestString("\"asdf\"w", "asdf", true);
			TestString("\"asdf\"d", "asdf", true);

			TestString("\"asdf\"", "asdf", false);
			TestString("\"asdf\"c", "asdf", false);
			TestString("\"asdf\"w", "asdf", false);
			TestString("\"asdf\"d", "asdf", false);

			var ex = DParser.ParseExpression("['a','s','d','f']");
			var v = ExpressionEvaluator.Evaluate(ex, null);

			Assert.IsInstanceOfType(v, typeof(ArrayValue));
			var ar = (ArrayValue)v;
			Assert.AreEqual(ar.Elements.Length, 4);

			foreach (var ev in ar.Elements)
				Assert.IsInstanceOfType(ev,typeof(PrimitiveValue));


			ex = DParser.ParseExpression("[\"KeyA\":12, \"KeyB\":33, \"KeyC\":44]");
			v = ExpressionEvaluator.Evaluate(ex, new StandardValueProvider(null));

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

			TestPrimitive("a", DTokens.Int, 234, vp);
			TestPrimitive("b", DTokens.Int, 123, vp);
			TestPrimitive("c", DTokens.Int, 125, vp);
			TestPrimitive("d", DTokens.Int, 126, vp);

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

			var v = ExpressionEvaluator.Evaluate(e, vp);

			Assert.IsInstanceOfType(v, typeof(PrimitiveValue));
			var pv = (PrimitiveValue)v;
			Assert.AreEqual(pv.BaseTypeToken, DTokens.Bool, "Type of 'is()' result must be bool");
			Assert.IsInstanceOfType(pv.Value, typeof(bool));

			return (bool)pv.Value;
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
