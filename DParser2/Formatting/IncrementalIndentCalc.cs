using System;
using System.Collections.Generic;
using System.IO;

using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Formatting
{
	public class TextDocument
	{
		public virtual string Text{get{return string.Empty;}}
		
		public virtual int LocationToOffset(CodeLocation l)
		{
			return DocumentHelper.LocationToOffset(Text, l);
		}
		
		public virtual CodeLocation OffsetToLocation(int offset)
		{
			return DocumentHelper.OffsetToLocation(Text, offset);
		}
		
		public virtual char CharAt(int offset)
		{
			return Text[offset];
		}
		
		public virtual int TextLength
		{
			get{return Text.Length;}
		}
		
		public virtual int SpacesPerTab
		{
			get{return 4;}
		}
	}
	
	
	public class IncrementalIndentCalc
	{
		readonly Lexer lx;
		readonly int SpacersPerTab;
		/// <summary>
		/// If e.g. an ) occurred but there was no fitting anti-parenthesis, this value will be decreased by one tab unit.
		/// </summary>
		int startColumn;
		Stack<Indent> indentStack = new Stack<IncrementalIndentCalc.Indent>();
		
		class Indent
		{
			public readonly IndentReason reason;
			public readonly CodeLocation loc;
			
			public Indent(IndentReason r, CodeLocation loc)
			{
				this.reason = r;
				this.loc = loc;
			}
		}
		
		enum IndentReason
		{
			/// <summary>
			/// asdf &lt;linebreak>
			/// </summary>
			IncompleteStatement,
			/// <summary>
			/// case ... :
			/// </summary>
			CaseLabel,
			/// <summary>
			/// (...)
			/// </summary>
			ParenBlock,
			/// <summary>
			/// {...}
			/// </summary>
			CurlyBlock,
			/// <summary>
			/// [...]
			/// </summary>
			ArrayBlock
		}
		
		/// <summary>
		/// Calculates indent for a line.
		/// </summary>
		/// <returns>Not the number of tabs but the column number (starting at 1). Tabs & Spaces shall be calculated by the target application.</returns>
		public static int GetIndent(TextDocument doc, CodeLocation locationToIndent)
		{
			var offsetToIndent = doc.LocationToOffset(locationToIndent);
			int lineToStartStartOffsetSearchFrom = locationToIndent.Line;
			// Detect whether we're in a special code section (string, char, comment [single line, nested, block]).
			int lastRegionStart, lastRegionEnd;
			var context = CaretContextAnalyzer.GetTokenContext(doc.Text, offsetToIndent, out lastRegionStart, out lastRegionEnd);
			if(context == TokenContext.VerbatimString || context == TokenContext.CharLiteral) // Only verbatim strings?
				return 1;
			
			// If we're in a string or comment, enforce the indent of the line where
			if(context != TokenContext.None)
				lineToStartStartOffsetSearchFrom = doc.OffsetToLocation(lastRegionStart).Line;
			// else: Get last line where code occurs and get its indent
			
			int prevIndent;
			var lastLine = GetLastCodeLine(doc, lineToStartStartOffsetSearchFrom, out prevIndent);
			
			if(context != TokenContext.None)
				return prevIndent;
			
			var lastLineOffset = doc.LocationToOffset(new CodeLocation(1,lastLine));
			
			char c;
			for(; offsetToIndent < doc.TextLength; offsetToIndent++)
				if((c=doc.CharAt(offsetToIndent))=='\r' ||c=='\n')
					break;
			
			// Calculate the indent from there on using DLexer etc
			var iic = new IncrementalIndentCalc(new StringView(doc.Text,lastLineOffset, offsetToIndent - lastLineOffset), doc.SpacesPerTab, prevIndent);
			iic.Walkthrough();
			
			// Return calculated indent
			return iic.ActualIndent;
		}
		
		public static int GetLastCodeLine(TextDocument doc, int lineToStartFrom, out int indentCol)
		{
			while(lineToStartFrom > 0)
			{
				var o = doc.LocationToOffset(new CodeLocation(1,lineToStartFrom));
				char c='\0';
				indentCol = 1;
				var len = doc.TextLength;
				
				while(o < len && ((c = doc.CharAt(o)) == ' ' || c == '\t'))
				{
					if(c == '\t')
						indentCol += doc.SpacesPerTab;
					else
						indentCol++;
					
					o++;
				}
				
				if(c == '\n' || c == '\r' || o == len)				
					lineToStartFrom--;
				else
					return lineToStartFrom;
			}
			
			indentCol = 1;
			return 1;
		}
		
		IncrementalIndentCalc(TextReader tr, int spacersPerTab, int initialIndent = 0)
		{
			lx = new Lexer(tr);
			lx.NextToken();
			this.SpacersPerTab = spacersPerTab;
			this.startColumn = initialIndent;
		}
		
		public int ActualIndent
		{
			get{
				return 0;
			}
		}
		
		public void Walkthrough()
		{
			while(!lx.IsEOF)
			{
				switch(lx.laKind)
				{
					case DTokens.OpenParenthesis:
						
						break;
				}
			}
		}
	}
}
