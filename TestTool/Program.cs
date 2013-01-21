using System;
using System.Diagnostics;
using D_Parser.Dom;
using D_Parser.Formatting;

namespace TestTool
{
	class Program
	{
		public static void Main(string[] args)
		{
			var policy = new DFormattingOptions();
			policy.TypeBlockBraces = BraceStyle.NextLine;
			policy.MultiVariableDeclPlacement = NewLinePlacement.SameLine;
			
			var code = @"
class A
{

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



void foo() {}
}";
			Console.WriteLine("## Formatting ##");
			
			var ast = D_Parser.Parser.DParser.ParseString(code) as DModule;
			
			var sw = new Stopwatch();
			sw.Start();
			code = Formatter.FormatCode(code, ast, null, policy);
			sw.Stop();
			Console.WriteLine(code);
			Console.WriteLine("Took {0}ms", sw.Elapsed.TotalMilliseconds);
			
			Console.WriteLine();
			Console.Write("Press any key to continue . . . ");
			Console.ReadKey(true);
		}
	}
}