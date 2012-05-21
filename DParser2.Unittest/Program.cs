using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using HighPrecisionTimer;
using System.Threading;
using D_Parser;
using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Parser;

namespace ParserTests
{
	class Program
	{
		public static string curFile = @"A:\D\dmd2\src\phobos\std\range.d";
		public static void Main(string[] args)
		{
			//FormatterTest.RunTests();
			//ParseTests.TestSourcePackages(false);
			//EvaluationTests.Run();
			ResolutionTests.Run();

			Console.WriteLine("~ Tests finished");
			//Console.ReadKey();
		}

		static void a()
		{
			var fcon=File.ReadAllText(curFile);
			var lx = new Lexer(new StringReader(fcon));

			var hp = new HighPrecTimer();

			hp.Start();

			lx.NextToken();

			while (lx.LookAhead.Kind != DTokens.EOF)
			{
				lx.NextToken();
			}

			hp.Stop();

			Console.WriteLine(Math.Round(hp.Duration, 3) + "s");
		}

		static void b()
		{
			string input =
@"is(T[0] == This[]) && is(T==class) && is(T[0] T)";

			//var mod = ParseTests.TestCode(input);
			//ParseTests.TestExpression(input);
			//ParseTests.TestExpressionStartFinder(input);
		}

		static void c()
		{
			string input = "";
			while (true)
			{
				input = Console.ReadLine();

				if (input == "q")
					return;

				var code = input;

				ParseTests.TestMathExpression(code);
			}
		}

		static void d()
		{
			ParseTests.TestSingleFile(curFile,true, false);
		}
	}
}
