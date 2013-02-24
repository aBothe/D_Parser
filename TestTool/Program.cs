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
using System.IO;

namespace TestTool
{
	class Program
	{
		public static void Main (string[] args)
		{/*
			var sw2 = new Stopwatch();
			var code = File.ReadAllText(@"D:\D\dmd2\src\phobos\std\datetime.d");
			sw2.Start();
			var ast = DParser.ParseString(code, true);
			sw2.Stop();*/
			(new ResolutionTests()).TemplateTypeTuple1();
			return;
			// Indent testing
			/*var code = @"
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
			return;*/


			// Phobos & Druntime parsing
			UFCSCache.SingleThreaded = true;
			var pc = new ParseCache ();
			pc.EnableUfcsCaching = false;
			pc.ParsedDirectories.Add (@"/usr/include/dmd/phobos");
			pc.ParsedDirectories.Add (@"/usr/include/dmd/druntime/import");
			
			Console.WriteLine ("Begin parsing...");

			foreach (var dir in pc.ParsedDirectories) {
				var ppd = ThreadedDirectoryParser.Parse(dir, pc.Root, false);
				Console.WriteLine("Parsed {0} in {1}s", dir, ppd.TotalDuration);
			}
			Console.WriteLine("done.");
			Console.WriteLine();
			var uc = new UFCSCache();
			var pcl = ParseCacheList.Create(pc);
			var ccf = new ConditionalCompilationFlags(new[]{ "Windows", "D2" }, 1, true, null, 0);
			
			Console.WriteLine("Begin building ufcs cache...");
			var sw = new Stopwatch();
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