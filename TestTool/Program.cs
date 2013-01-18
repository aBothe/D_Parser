using System;
using D_Parser.Formatting;

namespace TestTool
{
	class Program
	{
		public static void Main(string[] args)
		{
			var code = @"
class A{

}";
			Console.WriteLine(code);
			Console.WriteLine("## Formatting ##");
			code = Formatter.FormatCode(code);
			Console.WriteLine(code);
			
			Console.WriteLine();
			Console.Write("Press any key to continue . . . ");
			Console.ReadKey(true);
		}
	}
}