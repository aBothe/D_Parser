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
		const string traceFile = "trace.log";

		public static void Main (string[] args)
		{
			var rt = new ResolutionTests ();
			rt.SetupEnvironment ();

			Trace.Listeners.Clear ();
			Trace.AutoFlush = false;
			Trace.UseGlobalLock = false;
			if(File.Exists(traceFile))
				File.Delete (traceFile);
			Trace.Listeners.Add (new TextWriterTraceListener (traceFile));

			/*
			var sw2 = new Stopwatch();
			var code = File.ReadAllText(@"B:\Programs\D\dmd2\src\phobos\std\datetime.d");
			sw2.Start();
			var ast = DParser.ParseString(code, true);
			sw2.Stop();
			Console.WriteLine (sw2.ElapsedMilliseconds);
			return;*/
			//DParser.ParseString (@"");
			//(new ResolutionTests ()).BasicResolution0 ();
			//(new IndentationTests ()).TestIssue576 ();		
			//(new IndentationTests ()).SwitchIndentation ();

			//BotanProfil.Run ();
			rt.LooseResolution2();

			Trace.Flush ();
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

			Console.WriteLine ("Begin parsing...");

			var dirs = Environment.OSVersion.Platform == PlatformID.Unix ? new[] { @"/usr/include/dlang" } : new[] { @"B:\Programs\D\dmd2\src\phobos", @"B:\Programs\D\dmd2\src\druntime\import" };
			var skipFunctionBodies = false;


			var dirsLeft = dirs.Length;
			var ev=new System.Threading.AutoResetEvent(false);

			if (skipFunctionBodies)	Console.WriteLine ("Skipping method bodies!");
			GlobalParseCache.BeginAddOrUpdatePaths(dirs, skipFunctionBodies, (pc) =>
			{
				Console.WriteLine("{0}/{1}ms", pc.Duration, pc.ParseDuration);
				ev.Set();
			});

			ev.WaitOne();

			Console.WriteLine("done.");
			Console.WriteLine();
			var pcw = new ParseCacheView(dirs);
			var ccf = new ConditionalCompilationFlags(new[]{ Environment.OSVersion.Platform == PlatformID.Unix ?"Posix":"Windows", "D2" }, 1, true, null, 0);

			Console.WriteLine ("Dump parse errors:");
			int modCount = 0;
			foreach (var dir in dirs)
				foreach (var mod in GlobalParseCache.EnumModulesRecursively(dir)) {
					modCount++;
					if (mod.ParseErrors.Count > 0) {
						Console.WriteLine (" "+mod.FileName);
						Console.WriteLine ("  ("+mod.ModuleName+")");

						foreach (var err in mod.ParseErrors) {
							Console.WriteLine ("({0}):", err.Location.ToString ());
							Console.WriteLine ("\t"+err.Message);
						}
					}
				}

			Console.WriteLine("{0} modules parsed.", modCount);

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