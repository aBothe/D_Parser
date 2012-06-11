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
		public static PrimitiveValue GetPrimitiveValue(string literalCode)
		{
			var ex = DParser.ParseExpression(literalCode);
			var v = ExpressionEvaluator.Evaluate(ex, null);

			Assert.IsInstanceOfType(v, typeof(PrimitiveValue));
			return (PrimitiveValue)v;
		}

		public static void TestPrimitive(string literal, int btToken, object val)
		{
			var pv = GetPrimitiveValue(literal);

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
			ArrayResult ar = null;

			if (ProvideObjModule)
			{
				Assert.IsInstanceOfType(av.RepresentedType, typeof(AliasResult));
				var s = av.RepresentedType as AliasResult;
				ar = (ArrayResult)s.MemberBaseTypes[0];
			}
			else
				ar = (ArrayResult)av.RepresentedType;

			Assert.IsNotNull(ar);

			switch (id.Subformat)
			{
				case LiteralSubformat.Utf8:
					Assert.AreEqual(ar.ArrayDeclaration.ToString(),"immutable(char)[]");
					break;
				case LiteralSubformat.Utf16:
					Assert.AreEqual(ar.ArrayDeclaration.ToString(), "immutable(wchar)[]");
					break;
				case LiteralSubformat.Utf32:
					Assert.AreEqual(ar.ArrayDeclaration.ToString(), "immutable(dchar)[]");
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
		}
	}
}
