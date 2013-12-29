//
// DDocParser.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2013 Alexander Bothe
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
using System.Collections.Generic;
using System.Text;

namespace D_Parser.Parser
{
	public class DDocParser
	{
		string text;
		int nextOffset;

		StringBuilder sb = new StringBuilder();

		char Peek()
		{
			return nextOffset < text.Length ? text[nextOffset] : '\0';
		}

		char Read()
		{
			return nextOffset < text.Length ? text[nextOffset++] : '\0';
		}

		void Step()
		{
			nextOffset++;
		}

		public static void FindNextMacro(string text, int startOffset, 
			out int macroStart, 
			out int length, 
			out string macroName, 
			out Dictionary<string,string> macroParameters)
		{
			macroStart = text.IndexOf ("$(", startOffset);
			macroParameters = null;

			if (macroStart < 0) {
				macroName = null;
				length = 0;
				return;
			}

			var ddoc = new DDocParser { text = text, nextOffset = macroStart+2 };

			macroName = ddoc.MacroName ();

			if (!ddoc.Finished)
				macroParameters = ddoc.Parameters ();

			ddoc.Step ();

			length = Math.Min(ddoc.nextOffset,text.Length-1) - macroStart;
		}

		private DDocParser() {}

		string MacroName()
		{
			// Skip whitespaces
			while (char.IsWhiteSpace (Peek()))
				Step();

			sb.Clear ();
			while (char.IsLetterOrDigit (Peek()))
				sb.Append(Read());

			while (char.IsWhiteSpace (Peek()))
				Step();

			return sb.ToString ();
		}

		Dictionary<string,string> Parameters()
		{
			var l = new Dictionary<string,string> ();

			int parametersBegin = nextOffset;
			// $1 will represent the argument text up to the first comma, 
			// $2 from the first comma to the second comma, etc., up to $9.
			int paramBegin = nextOffset;
			int secondParamBegin = -1;
			int curParam = 1;
			while (!Finished) {
				switch(Peek())
				{
					case ',':
						if (curParam > 9) {
							Step ();
							break;
						}

						l ["$" + curParam.ToString ()] = text.Substring (paramBegin, nextOffset - paramBegin);

						Step ();

						if (curParam == 1) // Second parameter begins now -- will be $+ then
							secondParamBegin = nextOffset;
						paramBegin = nextOffset;

						curParam++;
						break;
					default:
						SkipParameterText ();
						break;
				}
			}

			nextOffset = Math.Min (nextOffset, text.Length-1);

			if(curParam > 1 && curParam < 9)
				l ["$" + curParam.ToString ()] = text.Substring (paramBegin, nextOffset - paramBegin);

			// Any text from the end of the identifier to the closing ‘)’ is the $0 argument.
			l ["$0"] = text.Substring (parametersBegin, nextOffset - parametersBegin);

			// $+ represents the text from the first comma to the closing ‘)’.
			if (secondParamBegin != -1)
				l ["$+"] = text.Substring (secondParamBegin, nextOffset-secondParamBegin);

			return l;
		}




		bool EOF { get{ return nextOffset >= text.Length; } }

		bool Finished
		{
			get{ return nextOffset >= text.Length || text[nextOffset] == ')'; }
		}

		void SkipParameterText()
		{
			switch (Peek ()) {
				case '(':
					SkipParentheses ();
					break;
				case '"':
				case '\'':
					SkipString ();
					break;
				case '<':
					Read ();
					if (Peek () == '!' &&
					    nextOffset + 2 < text.Length &&
					    text [nextOffset + 1] == '-' && text [nextOffset + 2] == '-') {
						Step (); // !
						Step (); // -
						Step (); // -
						SkipHtmlComment ();
					} else
						SkipParameterText ();
					break;
				case '\0':
				case ')':
					break;
				default:
					Step ();
					break;
			}
		}

		void SkipHtmlComment()
		{
			while (!Finished) {
				switch (Peek ()) {
					case '-':
						Read ();
						if (Peek () == '-' &&
						    nextOffset + 1 < text.Length &&
						    text [nextOffset + 1] == '>') {
							Step (); // -
							Step (); // >
							return;
						} else
							goto default;
						break;
					default:
						SkipParameterText ();
						break;
				}
			}
		}

		void SkipString()
		{
			var literalChar = Read ();

			char peekChar = '\0';
			while (!EOF && (peekChar = Peek ()) != literalChar) {
				switch (peekChar) {
					case '\\': // minimal escaping mechanism -- Is some D-like escaping required?
						Read ();
						if (Peek () == literalChar)
							Read ();
						break;
					case ')':
						Step ();
						break;
					default:
						SkipParameterText ();
						break;
				}
			}

			if (peekChar == literalChar)
				Step ();
		}

		void SkipParentheses()
		{
			// skip '('
			Step ();

			while (!Finished)
				SkipParameterText ();

			// skip ')'
			Step ();
		}
	}
}

