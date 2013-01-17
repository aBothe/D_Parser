using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Formatting.Indent
{
	public class IndentEngineWrapper
	{
		public static string CalculateIndent(string code, int line, bool tabsToSpaces = false, int indentWidth = 4)
		{
			using(var sr = new StringReader(code))
				return CalculateIndent(sr, line, tabsToSpaces, indentWidth);
		}
		
		public static string CalculateIndent(TextReader code, int line, bool tabsToSpaces = false, int indentWidth = 4)
		{
			if(line < 2)
				return string.Empty;
			
			var eng = new IndentEngine(DFormattingOptions.CreateDStandard(), tabsToSpaces, indentWidth);
			
			int curLine = 1;
			const int lf = (int)'\n';
			const int cr = (int)'\r';
			int c;
			while((c = code.Read()) != -1)
			{
				if(c == lf || c == cr)
				{
					if(c == cr && code.Peek() == lf)
						code.Read();
					
					if(++curLine > line)
						break;
				}
				
				eng.Push((char)c);
			}
			
			return eng.NewLineIndent;
		}
	}
}
