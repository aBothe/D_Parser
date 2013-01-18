using System;
using D_Parser.Formatting;

namespace TestTool
{
	class Program
	{
		public static void Main(string[] args)
		{
			var policy = new DFormattingOptions();
			policy.TypeBlockBraces = BraceStyle.EndOfLine;
			
			var code = @"
class A
// someCommentttt

{

//class B {}

}";
			Console.WriteLine(code);
			Console.WriteLine("## Formatting ##");
			
			code = Formatter.FormatCode(code, null, policy);
			Console.WriteLine(code);
			
			Console.WriteLine();
			Console.Write("Press any key to continue . . . ");
			Console.ReadKey(true);
		}
	}
}