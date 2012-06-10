using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using D_Parser.Dom.Expressions;
using D_Parser.Evaluation;
using D_Parser.Parser;

namespace DParser2.Unittest
{
	[TestClass]
	public class EvaluationTests
	{
		[TestMethod]
		public void TestPrimitives()
		{
			var ex=DParser.ParseExpression("1");
			var v = ExpressionEvaluator.Evaluate(ex, null);

			Assert.IsInstanceOfType(v, typeof(PrimitiveValue));
			var pv = (PrimitiveValue)v;

			Assert.AreEqual(pv.BaseTypeToken, DTokens.Int);
			Assert.AreEqual(pv.Value, (int)1);
		}
	}
}
