using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HighPrecisionTimer;
using D_Parser;
using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Parser;
using D_Parser.Dom.Expressions;

namespace ParserTests
{
	public class ParseTests
	{
		public const string dmdDir = "A:\\D\\dmd2";

		public static void TestSingleFile(string file, bool skipFunctionBodies=false,bool dump=true)
		{
			var hp = new HighPrecTimer();

			hp.Start();
			var n = DParser.ParseFile(file,skipFunctionBodies);
			hp.Stop();
			Console.WriteLine((int)(hp.Duration * 1000) + " ms");

			printErrors(n);
			if(dump)Dump(n, "");
		}

		public static IAbstractSyntaxTree TestCode(string code)
		{
			var hp = new HighPrecTimer();

			hp.Start();
			var n = DParser.ParseString(code);
			hp.Stop();
			Console.WriteLine((int)( hp.Duration*1000) + " ms");

			printErrors(n);
			Dump(n, "");
			return n;
		}

		public static void TestSourcePackages(bool parseOuterStructureOnly=false, int repetitions=1)
		{
			Console.WriteLine("~ Library parsing test...");

			var Files = new Dictionary<string, string>();

			foreach (string fn in Directory.GetFiles(dmdDir + "\\src\\phobos", "*.d?", SearchOption.AllDirectories))
			{
				if (fn.EndsWith("phobos\\index.d")) continue;
				Files.Add(fn, File.ReadAllText(fn));
			}
			foreach (string fn in Directory.GetFiles(dmdDir + "\\src\\druntime\\import", "*.d?", SearchOption.AllDirectories))
			{
				Files.Add(fn, File.ReadAllText(fn));
			}

			if (repetitions <= 1)
			{
				var hp = new HighPrecTimer();

				int i = 0;
				bool errorsFound = false;

				hp.Start();

				foreach (string file in Files.Keys)
				{
					i++;
					var n = DParser.ParseString(Files[file], parseOuterStructureOnly);

					if (printErrors(n))
						errorsFound = true;
				}
				hp.Stop();

				if (!errorsFound)
					Console.WriteLine("--- No errors found ---");
				Console.WriteLine(Math.Round(hp.Duration, 3) + "s / " + Math.Round(hp.Duration * 1000 / Files.Count, 1).ToString() + "ms per file");
			}
			else
				for (int j = repetitions; j >= 1; j--)
				{
					var hp = new HighPrecTimer();
					Console.Write("{0}/{1} ",repetitions-j+1,repetitions);
					hp.Start();
					int i = 0;
					foreach (string file in Files.Keys)
					{
						i++;
						var n = DParser.ParseString(Files[file], parseOuterStructureOnly);
					}
					hp.Stop();
					Console.WriteLine(Math.Round(hp.Duration, 3) + "s | ~" + Math.Round(hp.Duration * 1000 / Files.Count, 1).ToString() + "ms per file");

				}
		}

		public static IExpression TestExpression(string e)
		{
			var ex = DParser.ParseExpression(e);

			Console.WriteLine(e+"\t>>>\t"+ ex);
			return ex;
		}

		public static decimal TestMathExpression(string mathExpression)
		{
			return 0;
			/*
			var ex = DParser.ParseExpression(mathExpression);

			if (ex == null || !ex.IsConstant)
			{
				Console.WriteLine("\""+mathExpression+"\" not a mathematical expression!");
				return 0;
			}

			Console.WriteLine(ex.ToString()+" = "+ex.DecValue);
			return ex.DecValue;*/
		}

		public static void TestTypeEval(string e)
		{
			var ex = DParser.ParseExpression(e);

			Console.WriteLine("Code:\t\t"+e);
			Console.WriteLine("Expression:\t"+ex.ToString());

			//var tr = ex.ExpressionTypeRepresentation;

			//Console.WriteLine("Type representation:\t"+tr.ToString());
		}

		public static void TestExpressionStartFinder(string code_untilCaretOffset)
		{
			var start = CaretContextAnalyzer.SearchExpressionStart(code_untilCaretOffset, code_untilCaretOffset.Length);

			var expressionCode = code_untilCaretOffset.Substring(start, code_untilCaretOffset.Length- start);

			Console.WriteLine("unfiltered:\t"+code_untilCaretOffset+"\nfiltered:\t" + expressionCode);

			if (string.IsNullOrWhiteSpace(expressionCode))
			{
				Console.WriteLine("No code to parse!");
				return;
			}

			var parser = DParser.Create(new StringReader(expressionCode));
			parser.Lexer.NextToken();

			if (parser.IsAssignExpression())
			{
				var expr = parser.AssignExpression();

				Console.WriteLine("expression:\t" + expr.ToString());
			}
			else
			{
				var type = parser.Type();

				Console.WriteLine("type:\t\t" + type.ToString());
			}
		}


		/// <summary>
		/// </summary>
		/// <returns>true if errors were found</returns>
		static bool printErrors(IAbstractSyntaxTree mod)
		{
			if (mod.ParseErrors.Count > 0)
			{
				Console.WriteLine(mod.ModuleName);

				foreach (var e in mod.ParseErrors)
					Console.WriteLine("Line " + e.Location.Line.ToString() + " Col " + e.Location.Column.ToString() + ": " + e.Message);

				return true;
			}
			return false;
		}

		static void Dump(INode n, string lev)
		{
			Console.WriteLine(lev + n.ToString());
			if (n is IBlockNode)
			{
				Console.WriteLine(lev + "{");
				foreach (var ch in n as IBlockNode)
				{
					Dump(ch, lev + "  ");
				}
				Console.WriteLine(lev + "}");
			}
		}
	}
}
