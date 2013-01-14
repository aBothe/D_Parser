using System;
using System.Collections.Generic;
using System.IO;

using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Formatting
{
	public class GenericTextDocument
	{
		readonly string text;
		public GenericTextDocument(){ text = string.Empty; }
		public GenericTextDocument(string txt) { this.text = txt; }
		
		public virtual string Text{get{return text;}}
		
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
		#region Properties
		readonly Lexer lx;
		readonly int SpacersPerTab;
		/// <summary>
		/// If e.g. an ) occurred but there was no fitting anti-parenthesis, this value will be decreased by one tab unit.
		/// </summary>
		int startColumn;
		readonly int lastLine;
		List<Indent> indentStack = new List<IncrementalIndentCalc.Indent>();
		#endregion
		
		#region Internal structs
		class Indent
		{
			/// <summary>
			/// CodeLocation.Empty, if not.
			/// </summary>
			public CodeLocation followedIncompleteStatement;
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
			None=0,
			
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
			ArrayBlock,
			
			Block = IndentReason.ArrayBlock | IndentReason.CurlyBlock | IndentReason.ParenBlock,
		}
		#endregion
		
		#region I/O
		/// <summary>
		/// Calculates indent for a line.
		/// </summary>
		/// <returns>Not the number of tabs but the column number (starting at 1). Tabs & Spaces shall be calculated by the target application.</returns>
		public static int GetIndent(GenericTextDocument doc, CodeLocation locationToIndent)
		{
			if(doc.TextLength == 0 || locationToIndent.Line < 2)
				return 0;
			
			var offsetToIndent = doc.LocationToOffset(locationToIndent);
			int lineToStartStartOffsetSearchFrom = locationToIndent.Line -1;
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
			
			var lastLineLocation = new CodeLocation(1,lastLine);
			var lastLineOffset = doc.LocationToOffset(lastLineLocation);
			
			char c;
			for(; offsetToIndent < doc.TextLength; offsetToIndent++)
				if((c=doc.CharAt(offsetToIndent))=='\r' ||c=='\n')
					break;
			
			// Calculate the indent from there on using DLexer etc
			var iic = new IncrementalIndentCalc(new StringView(doc.Text,lastLineOffset, offsetToIndent - lastLineOffset),
			                                    locationToIndent.Line, doc.SpacesPerTab, lastLineLocation, prevIndent);
			iic.Walkthrough();
			
			// Return calculated indent
			return iic.ActualIndent;
		}
		
		public static int GetLastCodeLine(GenericTextDocument doc, int lineToStartFrom, out int indentCol)
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
		
		public static void CalcIndentTabsAndSpaces(int column, int spacersPerTab, out int tabs, out int spaces)
		{
			// Make a text length (0-based) out of column (1-based)
			if(column > 1)
				column--;
			
			if(spacersPerTab == 0)
			{
				tabs = spaces = 0;
				return;
			}
			tabs = column / spacersPerTab;
			spaces = column % spacersPerTab;
		}
		#endregion
		
		IncrementalIndentCalc(TextReader tr, int lastLine, int spacersPerTab, CodeLocation initialLocation, int initialIndent = 0)
		{
			lx = new Lexer(tr);
			lx.SetInitialLocation(initialLocation);
			lx.NextToken();
			this.lastLine = lastLine;
			this.SpacersPerTab = spacersPerTab;
			this.startColumn = initialIndent;
		}
		
		public int ActualIndent
		{
			get{
				if(indentStack.Count == 0)
					return startColumn;
				
				var ind = indentStack[indentStack.Count-1];
				
				if(ind.reason == IndentReason.IncompleteStatement)
				{
					if(ind.loc.Line == lx.LookAhead.Line)
						ind = indentStack[indentStack.Count-2];
					
					return ind.loc.Column + SpacersPerTab;
				}
				return ind.loc.Column;
			}
		}
		
		void _push(IndentReason r, IndentReason indentsToPop = IndentReason.None, bool popOneIndent = false)
		{
			var ind = new Indent(r, lx.LookAhead.Location) { 
			                	followedIncompleteStatement = LastIndentReason == IndentReason.IncompleteStatement ? 
			                		indentStack[indentStack.Count-1].loc : CodeLocation.Empty };
			
			if(popOneIndent)
				popOne(indentsToPop);
			else
				popAll(indentsToPop);
			
			indentStack.Add(ind);
		}
		
		void _pop()
		{
			if(indentStack.Count == 0){
				startColumn -= SpacersPerTab;
				if(startColumn<1)
					startColumn=1;
			}
			else
				indentStack.RemoveAt(indentStack.Count-1);
		}
		
		void popOne(IndentReason conditions)
		{
			IndentReason ir;
			if(indentStack.Count == 0 || (conditions & (ir=LastIndentReason)) == ir)
				_pop();
		}
		
		void popAll(IndentReason conditions)
		{
			if(indentStack.Count == 0)
			{
				_pop();
				return;
			}
			
			IndentReason ir;
			while(indentStack.Count != 0 && (conditions & (ir=LastIndentReason)) == ir)
				_pop();
		}
		
		void Push(IndentReason r)
		{
			IndentReason ir;
			switch(r)
			{
				case IndentReason.CaseLabel:
					_push(r,IndentReason.IncompleteStatement | IndentReason.CaseLabel);
					return;
					
				case IndentReason.ArrayBlock:
				case IndentReason.CurlyBlock:
				case IndentReason.ParenBlock:
					_push(r,IndentReason.IncompleteStatement,true);
					return;
					
				case IndentReason.IncompleteStatement:
					if(!IsLastLine && (ir = LastIndentReason) != IndentReason.IncompleteStatement && ir != IndentReason.CaseLabel)
						_push(r);
					return;
			}
		}
		
		void Pop(IndentReason r)
		{
			switch(r)
			{
				case IndentReason.ArrayBlock:
				case IndentReason.ParenBlock:
					popAll(IndentReason.IncompleteStatement | IndentReason.CaseLabel);
					popOne(r);
					
					// void ... (...)
					//     | <- How is this done?
					if(lx.Peek().Line > lx.LookAhead.Line){
						lx.NextToken();
						_push(IndentReason.IncompleteStatement);
					}
					return;
				case IndentReason.CurlyBlock:
					popAll(IndentReason.IncompleteStatement | IndentReason.CaseLabel);
					popOne(r);
					return;
				
				case IndentReason.IncompleteStatement:
					popAll(r);
					return;
				case IndentReason.CaseLabel:
					popOne(r);
					return;
			}
		}
		
		bool IsLastLine
		{
			get{ return lx.LookAhead.Line == lastLine; }
		}
		
		IndentReason LastIndentReason
		{
			get{return indentStack.Count == 0 ? IndentReason.None : indentStack[indentStack.Count-1].reason;}
		}
		
		public void Walkthrough()
		{
			while(!lx.IsEOF)
			{
				switch(lx.laKind)
				{
					case DTokens.OpenParenthesis:
						Push(IndentReason.ParenBlock);
						break;
					case DTokens.CloseParenthesis:
						Pop(IndentReason.ParenBlock);
						break;
					case DTokens.OpenCurlyBrace:
						Push(IndentReason.CurlyBlock);
						break;
					case DTokens.CloseCurlyBrace:
						Pop(IndentReason.CurlyBlock);
						break;
					case DTokens.OpenSquareBracket:
						Push(IndentReason.ArrayBlock);
						break;
					case DTokens.CloseSquareBracket:
						Pop(IndentReason.ArrayBlock);
						break;
					case DTokens.Default:
					case DTokens.Case:
						Push(IndentReason.CaseLabel);
						break;
					case DTokens.Colon:
						Pop(IndentReason.IncompleteStatement);
						break;
					//case DTokens.Comma:
					case DTokens.Semicolon:
						if(!IsLastLine)
							Pop(IndentReason.IncompleteStatement);
						break;
					default:
						if(LastIndentReason != IndentReason.IncompleteStatement)
							Push(IndentReason.IncompleteStatement);
						break;
				}
				
				lx.NextToken();
			}
		}
	}
}
