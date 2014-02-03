using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D_Parser.Parser;
using D_Parser.Misc;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.ExpressionSemantics;

namespace Tests
{
	[TestFixture]
	public class UFCSTests
	{
		[Test]
		public void BasicResolution()
		{
			var ctxt = ResolutionTests.CreateCtxt("modA",@"module modA;
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
			Assert.That (t, Is.TypeOf (typeof(ArrayType)));

			x = DParser.ParseExpression ("globI.foo()");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));

			x = DParser.ParseExpression ("globStr.writeln()");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.TypeOf (typeof(PrimitiveType)));
			Assert.That ((t as PrimitiveType).TypeToken, Is.EqualTo(DTokens.Void));
		}
	}
}
