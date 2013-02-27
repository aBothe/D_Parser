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
			
			var eng = new IndentEngine(options ?? DFormattingOptions.CreateDStandard(), textStyle.TabsToSpaces, textStyle.IndentSize, textStyle.KeepAlignmentSpaces);
			var replaceActions = new List<DFormattingVisitor.TextReplaceAction>();
			
			int originalIndent = 0;
			bool hadLineBreak = true;
			
			int n = 0;
			for (int i = 0; i <= endOffset && (n = code.Read()) != -1; i++)
			{
				if(n == '\r' || n == '\n')
				{
					if (i >= startOffset && !eng.LineBeganInsideString)
						replaceActions.Add(new DFormattingVisitor.TextReplaceAction(eng.Position - eng.LineOffset, originalIndent, eng.ThisLineIndent));
					
					hadLineBreak = true;
					originalIndent = 0;

					if (code.Peek() == '\n')
					{
						eng.Push((char)code.Read());
						i++;
					}
				}
				else if(hadLineBreak)
				{
					if(n == ' ' || n== '\t')
						originalIndent++;
					else
						hadLineBreak = false;

					// If there's code left, format the last line of the selection either
					if (i == endOffset && formatLastLine)
						endOffset++;
				}

				eng.Push((char)n);
			}

			// Also indent the last line if we're at the EOF.
			if (code.Peek() == -1 || (formatLastLine && n != '\r' && n != '\n'))
			{
				if(!eng.LineBeganInsideString)
					replaceActions.Add(new DFormattingVisitor.TextReplaceAction(eng.Position - eng.LineOffset, originalIndent, eng.ThisLineIndent));
			}
			
			// Perform replacements from the back of the document to the front - to ensure offset consistency
			for(int k = replaceActions.Count - 1; k>=0; k--)
			{
				var rep = replaceActions[k];
				if(rep.RemovalLength > 0 || rep.NewText.Length != 0)
					documentReplace(rep.Offset, rep.RemovalLength, rep.NewText);
			}
		}
	}
}
