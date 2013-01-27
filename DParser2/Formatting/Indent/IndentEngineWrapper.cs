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
			
			return eng.ThisLineIndent;
		}
	
		public static void CorrectIndent(TextReader code, int startOffset, int endOffset, Action<int, int, string> documentReplace, DFormattingOptions options = null, ITextEditorOptions textStyle = null, bool formatLastLine = true)
		{
			textStyle = textStyle ?? TextEditorOptions.Default;
			
			var eng = new IndentEngine(options ?? DFormattingOptions.CreateDStandard(), textStyle.TabsToSpaces, textStyle.IndentSize);
			var replaceActions = new List<DFormattingVisitor.TextReplaceAction>();
			
			int originalIndent = 0;
			bool hadLineBreak = true;
			int n = 0;
			
			var i = startOffset;
			while(i > 0){
				n = code.Read();
				i--;
				eng.Push((char)n);
				
				if(n == '\r')
				{
					if(code.Peek() == '\n'){
						code.Read();
						eng.Push('\n');
					}
					hadLineBreak = true;
					originalIndent = 0;
				}
				else if(n == '\n')
				{
					hadLineBreak = true;
					originalIndent = 0;
				}
				else if(hadLineBreak)
				{
					if(n == ' ' || n== '\t')
						originalIndent++;
					else
						hadLineBreak = false;
				}
			}
			
			i = endOffset - startOffset;
			while(i > 0)
			{
				n = code.Read();
				i--;

				if(n == '\r')
				{
					hadLineBreak = true;
					replaceActions.Add(new DFormattingVisitor.TextReplaceAction(eng.Position - eng.LineOffset,originalIndent, eng.ThisLineIndent));
					originalIndent = 0;
					
					eng.Push('\r');
					if(code.Peek() == '\n'){
						code.Read();
						eng.Push('\n');
					}
					
					continue;
				}
				else if(n == '\n')
				{
					hadLineBreak = true;
					replaceActions.Add(new DFormattingVisitor.TextReplaceAction(eng.Position - eng.LineOffset,originalIndent, eng.ThisLineIndent));
					originalIndent = 0;
					
					eng.Push('\n');
					continue;
				}
				else if(hadLineBreak)
				{
					if(n == ' ' || n == '\t')
						originalIndent++;
					else
						hadLineBreak = false;
				}
				
				eng.Push((char)n);
			}
			
			// If there's code left, format the last line of the selection either
			if(formatLastLine && code.Peek() > 0)
			{
				while((n=code.Read()) > 0 && n != '\r' && n != '\n')
					eng.Push((char)n);
				
				replaceActions.Add(new DFormattingVisitor.TextReplaceAction(eng.Position - eng.LineOffset,originalIndent, eng.ThisLineIndent));
			}
			
			// Perform replacements from the back of the document to the front - to ensure offset consistency
			for(int k = replaceActions.Count - 1; k!=0; k--)
			{
				var rep = replaceActions[k];
				documentReplace(rep.Offset, rep.RemovalLength, rep.NewText);
			}
		}
	}
}
