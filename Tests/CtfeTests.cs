using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using NUnit.Framework;
using D_Parser.Dom;
using D_Parser.Misc;

namespace Tests
{
	[TestFixture]
	public class CtfeTests
	{
		[Test]
		public void ReturnStmt()
		{
			var ctxt = ResolutionTests.CreateCtxt("A", @"module A;
int foo() { return 123; }
string foo(string s) { return s ~ ""gh""; }

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

			x = DParser.ParseExpression("foo");
			v = Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			pv = v as PrimitiveValue;
			Assert.That(pv.BaseTypeToken, Is.EqualTo(DTokens.Int));
			Assert.That(pv.Value, Is.EqualTo(123M));

			x = DParser.ParseExpression("foo(\"asdf\")");
			v = Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v, Is.TypeOf(typeof(ArrayValue)));
			av = v as ArrayValue;
			Assert.That(av.IsString, Is.True);
			Assert.That(av.StringValue, Is.EqualTo("asdfgh"));
		}
	}
}
