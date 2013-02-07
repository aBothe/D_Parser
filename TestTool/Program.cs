using System;
using System.Diagnostics;
using D_Parser;
using D_Parser.Dom;
using D_Parser.Formatting;
using D_Parser.Misc;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using Tests;

namespace TestTool
{
	class Program
	{
		public static void Main(string[] args)
		{

			return;



			// Indent testing
			var code = @"
";
			var line = 4;
			var ind = D_Parser.Formatting.Indent.IndentEngineWrapper.CalculateIndent(code, line, false, 4);
			var o = DocumentHelper.LocationToOffset(code, line,1);
			
			var o2 = o;
			while(o2 < code.Length && code[o2] == ' ' || code[o2] == '\t')
				o2++;
			
			code = code.Substring(0,o) + ind + code.Substring(o2);
			Console.Write(code+"|");
			
			Console.ReadKey(true);
			return;


			// Phobos & Druntime parsing
			//UFCSCache.SingleThreaded = true;
			var pc = new ParseCache();
			pc.EnableUfcsCaching = false;
			pc.ParsedDirectories.Add(@"D:\D\dmd2\src\phobos");
			pc.ParsedDirectories.Add(@"D:\D\dmd2\src\druntime\import");
			
			Console.WriteLine("Begin parsing...");
			var sw = new Stopwatch();
			sw.Start();
			pc.BeginParse();
			pc.WaitForParserFinish();
			sw.Stop();
			Console.WriteLine("done. {0}ms needed.", sw.ElapsedMilliseconds);
			Console.WriteLine();
			var uc = new UFCSCache();
			var pcl = ParseCacheList.Create(pc);
			var ccf = new ConditionalCompilationFlags(new[]{ "Windows", "D2" }, 1, true, null, 0);
			
			Console.WriteLine("Begin building ufcs cache...");
			sw.Restart();
			uc.Update(pcl, ccf, pc);
			sw.Stop();
			Console.WriteLine("done. {0}ms needed.", sw.ElapsedMilliseconds);
			
			Console.WriteLine();
			Console.Write("Press any key to continue . . . ");
			Console.ReadKey(true);
		}
		
		static void formattingTests()
		{
			var policy = new DFormattingOptions();
			policy.TypeBlockBraces = BraceStyle.NextLine;
			policy.MultiVariableDeclPlacement = NewLinePlacement.SameLine;
			
			var code = @"
class A
{

this()
{
}

private:

int* privPtr = 2134;

public
{
	int pubInt;
}

//SomeDoc
void                main
() in{}
out(v){}
body{

	int a = 123;
	a++

;

}
@safe int[] 
d 
=
34;
int a=12,b=23,c;



void foo(string[] args) {}
}";
			Console.WriteLine("## Formatting ##");
			
			var ast = D_Parser.Parser.DParser.ParseString(code) as DModule;
			
			var sw = new Stopwatch();
			sw.Start();
			code = Formatter.FormatCode(code, ast, null, policy);
			sw.Stop();
			Console.WriteLine(code);
			Console.WriteLine("Took {0}ms", sw.Elapsed.TotalMilliseconds);
		}
	}
}