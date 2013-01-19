﻿using System;
using System.Collections.Generic;
using System.Text;

using D_Parser.Dom;
using D_Parser.Parser;

namespace D_Parser.Formatting
{
	public enum FormattingMode {
		OnTheFly,
		Intrusive
	}
	
	public partial class DFormattingVisitor : DefaultDepthFirstVisitor
	{
		#region Change management
		sealed class TextReplaceAction
		{
			internal readonly int Offset;
			internal readonly int RemovalLength;
			internal readonly string NewText;
			internal TextReplaceAction DependsOn;

#if DEBUG
			internal readonly string StackTrace;
#endif

			public TextReplaceAction (int offset, int removalLength, string newText)
			{
				this.Offset = offset;
				this.RemovalLength = removalLength;
				this.NewText = newText ?? string.Empty;
				#if DEBUG
				this.StackTrace = Environment.StackTrace;
				#endif
			}
			
			public override bool Equals(object obj)
			{
				var other = obj as TextReplaceAction;
				if (other == null) {
					return false;
				}
				return this.Offset == other.Offset && this.RemovalLength == other.RemovalLength && this.NewText == other.NewText;
			}
			
			public override int GetHashCode()
			{
				return 0;
			}

			public override string ToString()
			{
				return string.Format("[TextReplaceAction: Offset={0}, RemovalLength={1}, NewText={2}]", Offset, RemovalLength, NewText);
			}
		}
		
		class ReplaceActionComparer : IComparer<TextReplaceAction>
		{
			public int Compare(DFormattingVisitor.TextReplaceAction x, DFormattingVisitor.TextReplaceAction y)
			{
				if(x.Offset == y.Offset)
					return 0;
				
				return y.Offset < x.Offset ? 1 : -1;
			}
		}
		
		TextReplaceAction AddChange(int offset, int removedChars, string insertedText)
		{
			if (removedChars == 0 && string.IsNullOrEmpty (insertedText))
				return null;
			var action = new TextReplaceAction (offset, removedChars, insertedText);
			changes.Add(action);
			return action;
		}
		
		public void ApplyChanges(Action<int, int, string> documentReplace, Func<int, int, string, bool> filter = null)
		{
			ApplyChanges(0, document.TextLength, documentReplace, filter);
		}
		
		public void ApplyChanges(int startOffset, int length, Action<int, int, string> documentReplace, Func<int, int, string, bool> filter = null)
		{
			int endOffset = startOffset + length;
			TextReplaceAction previousChange = null;
			int delta = 0;
			
			var depChanges = new List<TextReplaceAction> ();
			changes.Sort(new ReplaceActionComparer());
			foreach (var change in changes) {
				if (previousChange != null) {
					if (change.Equals(previousChange)) {
						// ignore duplicate changes
						continue;
					}
					if (change.Offset < previousChange.Offset + previousChange.RemovalLength) {
						#if DEBUG
						Console.WriteLine ("change 1:" + change + " at " + document.ToLocation (change.Offset));
						Console.WriteLine (change.StackTrace);

						Console.WriteLine ("change 2:" + previousChange + " at " + document.ToLocation (previousChange.Offset));
						Console.WriteLine (previousChange.StackTrace);
						#endif
						throw new InvalidOperationException ("Detected overlapping changes " + change + "/" + previousChange);
					}
				}
				previousChange = change;
				
				bool skipChange = change.Offset < startOffset || change.Offset > endOffset;
				skipChange |= filter != null && filter(change.Offset + delta, change.RemovalLength, change.NewText);
				skipChange &= !depChanges.Contains(change);

				if (!skipChange) {
					documentReplace(change.Offset + delta, change.RemovalLength, change.NewText);
					delta += change.NewText.Length - change.RemovalLength;
					if (change.DependsOn != null) {
						depChanges.Add(change.DependsOn);
					}
				}
			}
			changes.Clear();
		}
		#endregion
		
		#region Properties
		readonly DModule ast;
		DFormattingOptions policy;
		IDocumentAdapter document;
		List<TextReplaceAction> changes = new List<TextReplaceAction>();
		FormattingIndentStack curIndent;
		readonly ITextEditorOptions options;
		
		public FormattingMode FormattingMode {
			get;
			set;
		}

		public bool HadErrors {
			get;
			set;
		}

		public CodeLocation FormattingStartLocation = CodeLocation.Empty;
		public CodeLocation FormattingEndLocation = CodeLocation.Empty;
		public bool CheckFormattingBoundaries = false;
		#endregion
		
		#region Constructor / Init
		public DFormattingVisitor(DFormattingOptions policy, IDocumentAdapter document, DModule ast, ITextEditorOptions options = null)
		{
			if (policy == null) {
				throw new ArgumentNullException("policy");
			}
			if (document == null) {
				throw new ArgumentNullException("document");
			}
			this.ast = ast;
			this.policy = policy;
			this.document = document;
			this.options = options ?? TextEditorOptions.Default;
			curIndent = new FormattingIndentStack(this.options);
		}
		
		/// <summary>
		/// Use this method for letting this visitor visit the syntax tree.
		/// </summary>
		public void WalkThroughAst()
		{
			ast.Accept(this);
		}
		#endregion
		
		#region Formatting helpers
		void EnforceBraceStyle(BraceStyle braceStyle, CodeLocation lBrace, int rBraceLine, int rBraceColumn)
		{
			if (lBrace.IsEmpty)
				return;
			
			int lbraceOffset = document.ToOffset(lBrace);
			int rbraceOffset = document.ToOffset(rBraceLine, rBraceColumn);
			
			int whitespaceStart = SearchWhitespaceStart(lbraceOffset);
			int whitespaceEnd = SearchWhitespaceLineStart(rbraceOffset);
			string startIndent = "";
			string endIndent = "";
			switch (braceStyle) {
				case BraceStyle.DoNotChange:
					startIndent = endIndent = null;
					break;
				case BraceStyle.EndOfLineWithoutSpace:
					startIndent = "";
					endIndent = IsLineIsEmptyUpToEol(rbraceOffset) ? curIndent.IndentString : this.options.EolMarker + curIndent.IndentString;
					break;
				case BraceStyle.EndOfLine:
					int lastNonWs;
					var lastComments = GetCommentsBefore(lBrace, out lastNonWs);
					if(lastComments.Count != 0)
					{
						// delete old bracket
						AddChange(whitespaceStart, lbraceOffset - whitespaceStart + 1, "");
					
						lbraceOffset = whitespaceStart = lastNonWs + 1;
						startIndent = " {";
					} else {
						startIndent = " ";
					}
					endIndent = IsLineIsEmptyUpToEol(rbraceOffset) ? curIndent.IndentString : this.options.EolMarker + curIndent.IndentString;
					break;
				case BraceStyle.NextLine:
					startIndent = this.options.EolMarker + curIndent.IndentString;
					endIndent = IsLineIsEmptyUpToEol(rbraceOffset) ? curIndent.IndentString : this.options.EolMarker + curIndent.IndentString;
					break;
				case BraceStyle.NextLineShifted2:
				case BraceStyle.NextLineShifted:
					curIndent.Push(IndentType.Block);
					startIndent = this.options.EolMarker + curIndent.IndentString;
					endIndent = IsLineIsEmptyUpToEol(rbraceOffset) ? curIndent.IndentString : this.options.EolMarker + curIndent.IndentString;
					curIndent.Pop ();
					break;
			}
			
			if (lbraceOffset > 0 && startIndent != null) {
				AddChange(whitespaceStart, lbraceOffset - whitespaceStart, startIndent);
			}
			if (rbraceOffset > 0 && endIndent != null) {
				AddChange(whitespaceEnd, rbraceOffset - whitespaceEnd, endIndent);
			}
		}
		
		/// <summary>
		/// Returns a comment chain that is located right before 'where'
		/// </summary>
		List<Comment> GetCommentsBefore(CodeLocation where, out int firstNonWhiteSpaceOccurence)
		{
			firstNonWhiteSpaceOccurence = -1;
			var l = new List<Comment>();
			if(ast.Comments == null || ast.Comments.Length == 0)
				return l;
			
			int lastComment=0;
			
			for(; lastComment < ast.Comments.Length; lastComment++)
			{
				if(ast.Comments[lastComment].EndPosition > where)
				{
					break;
				}
			}
			
			lastComment--;
			
			if(lastComment < 0)
				return l;
			
			// Ensure that there is nothing between where and the comments end
			var whereOffset= document.ToOffset(where);
			for(; lastComment >= 0; lastComment--)
			{
				var comm = ast.Comments[lastComment];
				for(int i = document.ToOffset(comm.EndPosition); i < whereOffset; i++)
				{
					var c = document[i];
					if(c == ' ' || c == '\t' || c=='\r' || c == '\n')
						continue;
					goto ret;
				}
				
				l.Add(comm);
				whereOffset= document.ToOffset(comm.StartPosition);
			}
			
		ret:
			firstNonWhiteSpaceOccurence = SearchWhitespaceStart(whereOffset) - 1;
			
			return l;
		}
		#endregion
		
		#region Indentation helpers
		string nextStatementIndent = null;

		void FixStatementIndentation(CodeLocation location)
		{
			int offset = document.ToOffset(location);
			if (offset <= 0) {
				Console.WriteLine("possible wrong offset");
				Console.WriteLine(Environment.StackTrace);
				return;
			}
			bool isEmpty = IsLineIsEmptyUpToEol(offset);
			int lineStart = SearchWhitespaceLineStart(offset);
			string indentString = nextStatementIndent == null ? (isEmpty ? "" : this.options.EolMarker) + this.curIndent.IndentString : nextStatementIndent;
			nextStatementIndent = null;
			AddChange(lineStart, offset - lineStart, indentString);
		}

		void FixIndentation(CodeLocation location, int relOffset = 0)
		{
			if (location.Line < 1) {
				Console.WriteLine("Invalid location " + location);
				Console.WriteLine(Environment.StackTrace);
				return;
			}
			
			string lineIndent = GetIndentation(location.Line);
			string indentString = this.curIndent.IndentString;
			if (indentString != lineIndent && location.Column - 1 + relOffset == lineIndent.Length) {
				AddChange(document.ToOffset(location.Line, 1), lineIndent.Length, indentString);
			}
		}

		void FixIndentationForceNewLine(CodeLocation location, int offset = -1)
		{
			string lineIndent = GetIndentation(location.Line);
			string indentString = this.curIndent.IndentString;
			if (location.Column - 1 == lineIndent.Length) {
				AddChange(document.ToOffset(location.Line, 1), lineIndent.Length, indentString);
			} else {
				if(offset < 0)
					offset = document.ToOffset(location);
				int start = SearchWhitespaceLineStart(offset);
				if (start > 0) {
					char ch = document[start - 1];
					if (ch == '\n') {
						start--;
						if (start > 1 && document[start - 1] == '\r') {
							start--;
						}
					} else if (ch == '\r') {
						start--;
					}
					AddChange(start, offset - start, this.options.EolMarker + indentString);
				}
			}
		}
		
		string GetIndentation(int lineNumber)
		{
			var i = document.ToOffset(lineNumber, 1);
			var b = new StringBuilder ();
			int endOffset = document.TextLength;
			if(i>0)
			for (; i < endOffset; i++) {
				char c = document[i];
				if (!IsSpacing(c)) {
					break;
				}
				b.Append(c);
			}
			return b.ToString();
		}
		#endregion
		
		#region Helper methods
		bool InsideFormattingRegion(ISyntaxRegion sr)
		{
			return CheckFormattingBoundaries && FormattingStartLocation <= sr.Location && FormattingEndLocation >= sr.EndLocation;
		}
		
		int SearchWhitespaceStart(int startOffset)
		{
			if (startOffset < 0) {
				throw new ArgumentOutOfRangeException ("startoffset", "value : " + startOffset);
			}
			for (int offset = startOffset - 1; offset >= 0; offset--) {
				char ch = document[offset];
				if (!char.IsWhiteSpace(ch)) {
					return offset + 1;
				}
			}
			return 0;
		}

		int SearchWhitespaceEnd(int startOffset)
		{
			if (startOffset > document.TextLength) {
				throw new ArgumentOutOfRangeException ("startoffset", "value : " + startOffset);
			}
			for (int offset = startOffset + 1; offset < document.TextLength; offset++) {
				char ch = document[offset];
				if (!char.IsWhiteSpace(ch)) {
					return offset + 1;
				}
			}
			return document.TextLength - 1;
		}

		int SearchWhitespaceLineStart(int startOffset)
		{
			if (startOffset < 0) {
				throw new ArgumentOutOfRangeException ("startoffset", "value : " + startOffset);
			}
			for (int offset = startOffset - 1; offset >= 0; offset--) {
				char ch = document[offset];
				if (ch != ' ' && ch != '\t') {
					return offset + 1;
				}
			}
			return 0;
		}

		
		public bool IsLineIsEmptyUpToEol(CodeLocation startLocation)
		{
			return IsLineIsEmptyUpToEol(document.ToOffset(startLocation) - 1);
		}
		
		/// <summary>
		/// Counts backward from startOffset and returns true, if the entire line only consists of white spaces
		/// </summary>
		bool IsLineIsEmptyUpToEol(int startOffset)
		{
			for (startOffset--; startOffset >= 0; startOffset--) {
				char ch = document[startOffset];
				if (!IsSpacing(ch))
					return ch == '\n' || ch == '\r';
			}
			return true;
		}
		
		static bool IsSpacing(char ch)
		{
			return ch == ' ' || ch == '\t';
		}
		
		bool IsSpacing(int startOffset, int endOffset)
		{
			for (; startOffset < endOffset; startOffset++) {
				if (!IsSpacing(document[startOffset])) {
					return false;
				}
			}
			return true;
		}
		
		int SearchLastNonWsChar(int startOffset, int endOffset)
		{
			startOffset = System.Math.Max(0, startOffset);
			endOffset = System.Math.Max(startOffset, endOffset);
			if (startOffset >= endOffset) {
				return startOffset;
			}
			int result = -1;
			bool inBlockComment = false;
			int inNestedComment = 0;
			var textLength = document.TextLength;
			
			for (int i = startOffset; i < endOffset && i < textLength; i++) {
				char ch = document[i];
				if (IsSpacing(ch)) {
					continue;
				}
				
				char peek;
				if(i + 1 < textLength)
					peek = document[i + 1];
				else
					peek='\0';
				
				if(ch == '/')
				{
					if(peek == '/')
						return result;
					else if(peek == '*' && inNestedComment < 1)
					{
						inBlockComment = true;
						i++;
						continue;
					}
					else if(peek == '+')
					{
						inNestedComment++;
						i++;
						continue;
					}
				}
				
				if(peek == '/')
				{
					if(ch == '*' && inBlockComment)
					{
						inBlockComment = false;
						i++;
						continue;
					}
					else if(ch == '+' && inNestedComment > 0)
					{
						inNestedComment--;
						i++;
						continue;
					}
				}

				if (!inBlockComment && inNestedComment == 1) {
					result = i;
				}
			}
			return result;
		}
		#endregion
		
		// See DFormattingVisitor.Impl.cs for actual visiting
	}
}
