using System;

namespace D_Parser.Formatting
{
	public interface ITextEditorOptions
	{
		string EolMarker {get;}
		bool TabsToSpaces {get;}
		int TabSize{get;}
		bool KeepAlignmentSpaces { get; }
		
		int IndentSize {get;}
		int ContinuationIndent {get;}
		int LabelIndent {get;}
	}
	
	public class TextEditorOptions : ITextEditorOptions
	{
		public string EolMarker		{get;set;}
		public bool TabsToSpaces	{get;set;}
		public int TabSize			{get;set;}
		public int IndentSize		{get;set;}
		public int ContinuationIndent {get;set;}
		public int LabelIndent		{get;set;}
		public bool KeepAlignmentSpaces { get; set; }
		
		public static TextEditorOptions Default = new TextEditorOptions{ 
			EolMarker = Environment.NewLine,
			TabsToSpaces = false, 
			TabSize = 4, 
			IndentSize = 4, 
			ContinuationIndent = 4, 
			LabelIndent = 0,
			KeepAlignmentSpaces = true};
	}
}
