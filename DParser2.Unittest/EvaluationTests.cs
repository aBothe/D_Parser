using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ParserTests
{
	public class EvaluationTests
	{
		public static void Run()
		{
			Console.WriteLine("~ Evaluation tests...");

			var dic = new Dictionary<string, object>
			{
				// Primary expressions
				{"null",null},
				{"1",(int)1},
				{"-1",-1},
				{"1.2",1.2},
				{"'a'",'a'},
				{"\"derp\"","derp"},
				{"true",true},
				{"false",false},
			};

			foreach (var kv in dic)
				Check(kv.Key, kv.Value);
		}

		public static void Check(string code, object compValue)
		{
			var x = D_Parser.Parser.DParser.ParseExpression(code);

			var v = D_Parser.Evaluation.ExpressionEvaluator.Evaluate(x, null);
			
			if (v == null || v.Value == null)
			{
				Console.WriteLine();
				Console.WriteLine(code + "  returned null");
			}
			else if (!v.Value.Equals(compValue))
			{
				Console.WriteLine();
				Console.WriteLine(code + "  -->");
				Console.WriteLine(v.Value + " [evaluated]");
				Console.WriteLine(compValue.ToString() + " [target value]");
			}
		}
	}
}
