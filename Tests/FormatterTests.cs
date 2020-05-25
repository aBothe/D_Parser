using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using D_Parser.Formatting;
using NUnit.Framework;

namespace Tests
{
	[TestFixture]
	public class FormatterTests
	{
		[Test]
		public void Formatting()
		{
			var o = DFormattingOptions.CreateDStandard();
			
			bool isTargetCode = false;
			
			var rawCode = string.Empty;
			var sb = new StringBuilder();
			var l = new List<Tuple<string,string>>();
			
			using(var st = Assembly.GetExecutingAssembly().GetManifestResourceStream("Tests.formatterTests.txt")){
				using(var r = new StreamReader(st))
				{
					int n;
					while((n=r.Read()) != -1)
					{
						if((n == ':' && r.Peek() == ':') || (n == '#' && r.Peek() == '#'))
						{
							r.ReadLine();
							
							if(n == '#')
							{								
								if(isTargetCode)
								{
									l.Add(new Tuple<string,string>(rawCode, sb.ToString().Trim()));
									sb.Clear();
								}
								else
								{
									rawCode = sb.ToString().Trim();
									sb.Clear();
								}
								
								isTargetCode = !isTargetCode;
							}
						}
						else if(n == '\r' || n == '\n')
						{
							sb.Append((char)n);
						}
						else
						{
							sb.Append((char)n);
							sb.AppendLine(r.ReadLine());
						}
					}
				}
			}
			
			foreach(var tup in l)
			{
				Fmt(tup.Item1, tup.Item2, o);
			}
		}
		
		public static void Fmt(string code, string targetCode, DFormattingOptions policy)
		{
			var formatOutput = Formatter.FormatCode(code, null, new TextDocument{Text = code},policy, TextEditorOptions.Default);
			
			Assert.AreEqual(targetCode, formatOutput.Trim());
		}
	}
}
