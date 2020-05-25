//
// NewIndentEngine.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2014 Alexander Bothe
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Text;
using D_Parser.Parser;

namespace D_Parser.Formatting.Indent
{
#if IGNORE
	class DIndentEngine : IStateMachineIndentEngine
	{
		#region Properties
		protected DFormattingOptions Policy;

		/// <summary>
		/// True if spaces shall be kept for aligning code.
		/// False if spaces shall be replaced by tabs and only the few remaining spaces are used for aligning.
		/// </summary>
		public readonly bool keepAlignmentSpaces;
		public readonly bool tabsToSpaces;
		public readonly int indentWidth;

		IndentState currentState;
		public IndentState IndentState {
			get {
				return currentState;
			}
		}
		public Inside Inside{ get{ return Inside.Empty; }}

		/// <inheritdoc />
		public string ThisLineIndent
		{
			get
			{
				// OPTION: IndentBlankLines
				// remove the indentation of this line if isLineStart is true
				//				if (!textEditorOptions.IndentBlankLines && isLineStart)
				//				{
				//					return string.Empty;
				//				}

				return currentState.ThisLineIndent.IndentString;
			}
		}

		/// <inheritdoc />
		public string NextLineIndent
		{
			get
			{
				return currentState.NextLineIndent.IndentString;
			}
		}

		/// <inheritdoc />
		public string CurrentIndent
		{
			get
			{
				return currentIndent.ToString();
			}
		}

		/// <inheritdoc />
		/// <remarks>
		///     This is set depending on the current <see cref="Location"/> and
		///     can change its value until the <see cref="newLineChar"/> char is
		///     pushed. If this is true, that doesn't necessarily mean that the
		///     current line has an incorrect indent (this can be determined
		///     only at the end of the current line).
		/// </remarks>
		public bool NeedsReindent
		{
			get
			{
				// return true if it's the first column of the line and it has an indent
				if (column == 1)
				{
					return ThisLineIndent.Length > 0;
				}

				// ignore incorrect indentations when there's only ws on this line
				if (isLineStart)
				{
					return false;
				}

				return ThisLineIndent != CurrentIndent.ToString();
			}
		}

		/// <inheritdoc />
		public int Offset
		{
			get
			{
				return offset;
			}
		}

		/// <inheritdoc />
		public int LineNumber {
			get {
				return line;
			}
		}

		/// <inheritdoc />
		public int LineOffset {
			get {
				return column;
			}
		}

		/// <inheritdoc />
		public bool EnableCustomIndentLevels
		{
			get;
			set;
		}
		#endregion 

		#region Fields
		/// <summary>
		///     Stores the last sequence of characters that can form a
		///     valid keyword or variable name.
		/// </summary>
		internal StringBuilder wordToken = new StringBuilder();

		/// <summary>
		///     Stores the previous sequence of chars that formed a
		///     valid keyword or variable name.
		/// </summary>
		internal string previousKeyword;

		/// <summary>
		///    Represents the number of pushed chars.
		/// </summary>
		internal int offset = 0;

		/// <summary>
		///    The current line number.
		/// </summary>
		internal int line = 1;

		/// <summary>
		///    The current column number.
		/// </summary>
		/// <remarks>
		///    One char can take up multiple columns (e.g. \t).
		/// </remarks>
		internal int column = 1;

		/// <summary>
		///    True if <see cref="char.IsWhiteSpace(char)"/> is true for all
		///    chars at the current line.
		/// </summary>
		internal bool isLineStart = true;

		/// <summary>
		///    True if <see cref="isLineStart"/> was true before the current
		///    <see cref="wordToken"/>.
		/// </summary>
		internal bool isLineStartBeforeWordToken = true;

		/// <summary>
		///    Current char that's being pushed.
		/// </summary>
		internal char currentChar = '\0';

		/// <summary>
		///    Last non-whitespace char that has been pushed.
		/// </summary>
		internal char previousChar = '\0';

		/// <summary>
		///     Represents the new line character.
		/// </summary>
		internal readonly char newLineChar;

		/// <summary>
		///    Previous new line char
		/// </summary>
		internal char previousNewline = '\0';

		/// <summary>
		///    Current indent level on this line.
		/// </summary>
		internal StringBuilder currentIndent = new StringBuilder();

		/// <summary>
		///     True if this line began in <see cref="VerbatimStringState"/>.
		/// </summary>
		internal bool lineBeganInsideVerbatimString = false;

		/// <summary>
		///     True if this line began in <see cref="MultiLineCommentState"/>.
		/// </summary>
		internal bool lineBeganInsideMultiLineComment = false;

		#endregion

		#region Constructor/Init/Lowlevel
		public DIndentEngine ()
		{
		}

		public IStateMachineIndentEngine Clone ()
		{
			var eng = new DIndentEngine ();

			return eng;
		}

		IDocumentIndentEngine IDocumentIndentEngine.Clone ()
		{
			return Clone ();
		}

		object ICloneable.Clone ()
		{
			return Clone ();
		}
		#endregion

		#region IDocumentIndentEngine implementation

		public void Push (char ch)
		{
			if (Lexer.IsIdentifierPart (ch) && (wordToken.Length != 0 || !Lexer.IsDigit (ch))) { // Don't allow digits as initial id char
				wordToken.Append (ch);
			} else if (wordToken.Length != 0) {
				var kw = DTokens.GetTokenID(wordToken.ToString());
				currentState.CheckKeyword (kw);
				previousKeyword = kw;
				wordToken.Length = 0;
				isLineStartBeforeWordToken = false;
			}

			switch (ch) {
				case '\n':
					if (previousNewline == '\r') {
						offset++;
						return;
					}
					goto case '\r';
				case '\r':
					currentState.Push(currentChar = newLineChar);
					offset++;

					previousNewline = ch;
					// there can be more than one chars that determine the EOL,
					// the engine uses only one of them defined with newLineChar
					if (currentChar != newLineChar)
					{
						return;
					}
					currentIndent.Length = 0;
					isLineStart = true;
					isLineStartBeforeWordToken = true;
					column = 1;
					line++;

					lineBeganInsideMultiLineComment = IsInsideMultiLineComment;
					lineBeganInsideVerbatimString = IsInsideVerbatimString;
					break;
				case ' ':
					currentState.Push (currentChar = ch);
					offset++;
					previousNewline = '\0';

					if(isLineStart)
						currentIndent.Append (ch);

					column++;
					break;
				case '\t':
					currentState.Push (currentChar = ch);
					offset++;
					previousNewline = '\0';

					if(isLineStart)
						currentIndent.Append (ch);

					var nextTabStop = (column - 1 + indentWidth) / indentWidth;
					column = 1 + nextTabStop * indentWidth;
					break;
				default:
					currentState.Push(currentChar = ch);
					offset++;
					previousNewline = '\0';

					previousChar = currentChar;
					isLineStart = false;

					column++;
					break;
			}
		}

		public void Reset ()
		{
			throw new NotImplementedException ();
		}
		#endregion
	}
#endif
}

