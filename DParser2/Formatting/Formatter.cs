using System;
using System.Text;
using D_Parser.Dom;
using D_Parser.Parser;

namespace D_Parser.Formatting
{
	public class Formatter
	{
		public static string FormatCode(string code, DModule ast = null, IDocumentAdapter document = null, DFormattingOptions options = null, ITextEditorOptions textStyle = null)
		{
			options = options ?? DFormattingOptions.CreateDStandard();
			textStyle = textStyle ?? TextEditorOptions.Default;
			ast = ast ?? DParser.ParseString(code) as DModule;
			
			var formattingVisitor = new DFormattingVisitor(options, document ?? new TextDocument{ Text = code }, ast, textStyle);
			
			formattingVisitor.WalkThroughAst();
			
			var sb = new StringBuilder(code);
			
			formattingVisitor.ApplyChanges((int start, int length, string insertedText) => {
			                               	sb.Remove(start,length);
			                               	sb.Insert(start,insertedText);
			                               });
			
			return sb.ToString();
		}
	}
}
