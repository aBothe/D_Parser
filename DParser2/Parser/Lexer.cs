﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using D_Parser.Dom;

namespace D_Parser.Parser
{
	public sealed class Lexer : IDisposable
	{
		#region Properties
		public bool endedWhileBeingInNonCodeSequence = false;
		TextReader reader;
		int Col = 1;
		int Line = 1;
		public byte laKind;

		DToken prevToken;
		DToken curToken;
		DToken lookaheadToken;
		DToken peekToken;

		StringBuilder sb = new StringBuilder();
		/// <asummary>
		/// used for the original value of strings (with escape sequences).
		/// </asummary>
		//StringBuilder originalValue = new StringBuilder();
		char[] escapeSequenceBuffer = new char[12];

		// The C# compiler has a fixed size length therefore we'll use a fixed size char array for identifiers
		// it's also faster than using a string builder.
		const int MAX_IDENTIFIER_LENGTH = 512;
		char[] identBuffer = new char[MAX_IDENTIFIER_LENGTH];

		public IList<ParserError> LexerErrors = new List<ParserError>();
		public List<ParserError> Tasks = new List<ParserError>();
		public HashSet<string> TaskTokens = new HashSet<string>();

		/// <summary>
		/// Set to false if normal block comments shall be logged, too.
		/// </summary>
		public bool OnlyEnlistDDocComments = true;

		/// <summary>
		/// A temporary storage for DDoc comments
		/// </summary>
		public List<Comment> Comments = new List<Comment>();

		public bool IsEOF
		{
			get
			{
				return lookaheadToken == null ||
					lookaheadToken.Kind == DTokens.EOF ||
					lookaheadToken.Kind == DTokens.__EOF__;
			}
		}

		Stack<DToken> laBackup = new Stack<DToken>();
		Stack<DToken> tokenPool = new Stack<DToken>(40);
		#endregion

		#region Token pooling
		DToken tok()
		{
			/*if(tokenPool.Count != 0 && tokenPool.Peek() == lookaheadToken)
			{
				return null;
			}*/
			return tokenPool.Count == 0 ? new DToken() : tokenPool.Pop();
		}
		void recyclePrevToken()
		{
			if (laBackup.Count == 0 && prevToken != null)
			{
				prevToken.next = null;
				//prevToken.LiteralValue = null;
				tokenPool.Push(prevToken);
			}
		}
		DToken Token(byte kind, int startLocation_Col, int startLocation_Line, int tokenLength,
			object literalValue,/* string value,*/ LiteralFormat literalFormat = 0, LiteralSubformat literalSubFormat = 0)
		{
			var tk = tok();
			tk.Line = startLocation_Line;
			tk.Column = startLocation_Col;
			tk.EndLineDifference = 0;
			tk.EndColumn = unchecked(startLocation_Col + tokenLength);

			tk.Kind = kind;
			tk.LiteralFormat = literalFormat;
			tk.Subformat = literalSubFormat;
			tk.LiteralValue = literalValue;
			tk.RawCodeRepresentation = null;

			return tk;
		}

		DToken Token(byte kind, int startLocation_Col, int startLocation_Line, int endLocation_Col, int endLocation_Line,
			object literalValue,/* string value,*/ LiteralFormat literalFormat = 0, LiteralSubformat literalSubFormat = 0, string rawCode = null)
		{
			var tk = tok();
			tk.Line = startLocation_Line;
			tk.Column = startLocation_Col;

			tk.EndLineDifference = (ushort)unchecked(endLocation_Line - startLocation_Line);
			tk.EndColumn = endLocation_Col;

			tk.Kind = kind;
			tk.LiteralFormat = literalFormat;
			tk.Subformat = literalSubFormat;
			tk.LiteralValue = literalValue;
			tk.RawCodeRepresentation = rawCode;

			return tk;
		}

		DToken Token(byte kind, int startLocation_Col, int startLocation_Line, int tokenLength = 1)
		{
			var tk = tok();
			tk.Kind = kind;

			tk.Line = startLocation_Line;
			tk.Column = startLocation_Col;
			tk.EndLineDifference = 0;
			tk.EndColumn = unchecked(startLocation_Col + tokenLength);

			tk.LiteralFormat = 0;
			tk.Subformat = 0;
			tk.LiteralValue = null;
			tk.RawCodeRepresentation = null;
			return tk;
		}

		DToken Token(byte kind, int col, int line, string val)
		{
			var tk = tok();
			tk.Kind = kind;

			tk.Line = line;
			tk.Column = col;
			tk.EndColumn = unchecked(col + (val == null ? 1 : val.Length));
			tk.LiteralValue = val;
			tk.LiteralFormat = 0;
			tk.Subformat = 0;
			tk.RawCodeRepresentation = null;
			return tk;
		}
		#endregion

		#region Constructor / Init
		public Lexer(TextReader reader)
		{
			this.reader = reader;
			if ((char)reader.Peek() == '#')
			{
				SkipToEndOfLine();
			}
		}

		public void SetInitialLocation(CodeLocation location)
		{
			if (curToken != null || lookaheadToken != null || peekToken != null)
				throw new InvalidOperationException();
			Line = location.Line;
			Col = location.Column;
		}

		public void Dispose()
		{
			reader = null;
			prevToken = curToken = lookaheadToken = peekToken = null;
			//sb = originalValue = null;
			escapeSequenceBuffer = null;
			identBuffer = null;
			LexerErrors = null;
			Comments = null;
			Tasks = null;
		}
		#endregion

		#region I/O

		int ReaderRead()
		{
			int val = reader.Read();
			if ((val == '\r' && reader.Peek() != '\n') || val == '\n')
			{
				++Line;
				Col = 1;
			}
			else if (val >= 0)
			{
				Col++;
			}
			return val;
		}
		int ReaderPeek()
		{
			return reader.Peek();
		}

		#endregion

		#region Token instance related
		public DToken LastToken
		{
			get { return prevToken; }
		}

		/// <summary>
		/// Get the current DToken.
		/// </summary>
		public DToken CurrentToken
		{
			get
			{
				return curToken;
			}
		}

		/// <summary>
		/// The next DToken (The <see cref="CurrentToken"/> after <see cref="NextToken"/> call) .
		/// </summary>
		public DToken LookAhead
		{
			get
			{
				return lookaheadToken;
			}
		}

		public DToken CurrentPeekToken
		{
			get { return peekToken; }
		}

		/// <summary>
		/// Must be called before a peek operation.
		/// </summary>
		public void StartPeek()
		{
			peekToken = lookaheadToken;
		}

		/// <summary>
		/// Gives back the next token. A second call to Peek() gives the next token after the last call for Peek() and so on.
		/// </summary>
		/// <returns>An <see cref="CurrentToken"/> object.</returns>
		public DToken Peek()
		{
			if (peekToken == null) StartPeek();
			if (peekToken.next == null)
				peekToken.next = Next();
			peekToken = peekToken.next;
			return peekToken;
		}

		public void PushLookAheadBackup()
		{
			laBackup.Push(lookaheadToken);
		}

		public void PopLookAheadBackup()
		{
			prevToken = null;
			curToken = null;
			laBackup.Pop();
			/*
			var bk = laBackup.Pop();
			
			if(laBackup.Count == 0)
			{
				while(bk != lookaheadToken)
				{
					var n = bk.next;
					bk.next = null;
					bk.LiteralValue = null;
					tokenPool.Push(bk);
					if((bk = n) == null)
						return;
				}
			}*/
		}

		public void RestoreLookAheadBackup()
		{
			prevToken = null;
			curToken = null;

			lookaheadToken = laBackup.Pop();
			laKind = lookaheadToken.Kind;

			StartPeek();
			Peek();
		}

		/// <summary>
		/// Reads the next token and gives it back.
		/// </summary>
		/// <returns>An <see cref="CurrentToken"/> object.</returns>
		public void NextToken()
		{
			if (lookaheadToken == null)
			{
				lookaheadToken = Next();
				laKind = lookaheadToken.Kind;
				Peek();
			}
			else
			{
				recyclePrevToken();
				prevToken = curToken;

				curToken = lookaheadToken;

				if (lookaheadToken.next == null)
					lookaheadToken.next = Next();

				lookaheadToken = lookaheadToken.next;
				laKind = lookaheadToken.Kind;
				StartPeek();
				Peek();
			}
		}

		bool HandleLineEnd(char ch)
		{
			// Handle MS-DOS or MacOS line ends.
			if (ch == '\r')
			{
				if (reader.Peek() == '\n')
				{ // MS-DOS line end '\r\n'
					ReaderRead(); // LineBreak (); called by ReaderRead ();
					return true;
				}
				else
				{ // assume MacOS line end which is '\r'
					return true;
				}
			}
			if (ch == '\n')
				return true;
			return false;
		}

		void SkipToEndOfLine()
		{
			int nextChar;
			while ((nextChar = reader.Read()) != -1)
			{
				if (nextChar == '\r')
				{
					if (reader.Peek() == '\n')
						reader.Read();
					nextChar = '\n';
				}
				if (nextChar == '\n')
				{
					++Line;
					Col = 1;
					break;
				}
			}
		}

		string ReadToEndOfLine()
		{
			TaskParseState taskState = new TaskParseState(TaskTokens);

			sb.Length = 0;
			int nextChar;
			while ((nextChar = reader.Read()) != -1)
			{
				taskState.doNextChar(nextChar, Col + sb.Length, sb);

				char ch = (char)nextChar;

				if (nextChar == '\r')
				{
					if (reader.Peek() == '\n')
						reader.Read();
					nextChar = '\n';
				}
				// Return read string, if EOL is reached
				if (nextChar == '\n')
				{
					taskState.endOfLineOrComment(Line, sb, Tasks);

					++Line;
					Col = 1;
					return sb.ToString();
				}

				sb.Append(ch);
			}

			taskState.endOfLineOrComment(Line, sb, Tasks);

			// Got EOF before EOL
			Col += sb.Length;
			return sb.ToString();
		}

		/// <summary>
		/// Skips to the end of the current code block.
		/// For this, the lexer must have read the next token AFTER the token opening the
		/// block (so that Lexer.DToken is the block-opening token, not Lexer.LookAhead).
		/// After the call, Lexer.LookAhead will be the block-closing token.
		/// </summary>
		public void SkipCurrentBlock()
		{
			int braceCount = 0;
			// Scan already parsed tokens
			var tok = lookaheadToken;
			while (tok != null)
			{
				if (tok.Kind == DTokens.OpenCurlyBrace)
					braceCount++;
				else if (tok.Kind == DTokens.CloseCurlyBrace)
				{
					braceCount--;
					if (braceCount < 0)
					{
						lookaheadToken = tok;
						laKind = tok.Kind;
						return;
					}
				}
				tok = tok.next;
			}

			// Scan/proceed tokens rawly (skip them only until braceCount<0)
			//recyclePrevToken();
			prevToken = LookAhead;
			int nextChar;
			while ((nextChar = ReaderRead()) != -1)
			{
				switch (nextChar)
				{
					// Handle line ends
					case '\r':
					case '\n':
						HandleLineEnd((char)nextChar);
						break;

					// Handle comments
					case '/':
						int peek = ReaderPeek();
						if (peek == '/' || peek == '*' || peek == '+')
						{
							ReadComment();
							continue;
						}
						break;

					// handle string literals
					case 'r':
						int pk = ReaderPeek();
						if (pk == '"')
						{
							ReaderRead();
							ReadVerbatimString('"');
						}
						break;
					case '`':
						ReadVerbatimString(nextChar);
						break;
					case '"':
						ReadString(nextChar);
						break;
					case '\'':
						ReadChar();
						break;

					case '{':
						braceCount++;
						continue;
					case '}':
						braceCount--;
						if (braceCount < 0)
						{
							lookaheadToken = Token(laKind = DTokens.CloseCurlyBrace, Col - 1, Line);
							StartPeek();
							Peek();
							return;
						}
						break;
				}
			}
		}
		#endregion

		#region Actual tokenizing
		DToken Next()
		{
			int next;
			int nextChar;
			char ch;
			bool hadLineEnd = false;
			int x = Col - 1;
			int y = Line;
			if (Line == 1 && Col == 1) hadLineEnd = true; // beginning of document

			while ((nextChar = ReaderRead()) != -1)
			{
				DToken token;

				switch (nextChar)
				{
					case ' ':
					case '\t':
						continue;
					case '\r':
					case '\n':
						if (hadLineEnd)
						{
							// second line end before getting to a token
							// -> here was a blank line
							//specialTracker.AddEndOfLine(new Location(Col, Line));
						}
						HandleLineEnd((char)nextChar);
						hadLineEnd = true;
						continue;
					case '/':
						int peek = ReaderPeek();
						if (peek == '/' || peek == '*' || peek == '+')
						{
							ReadComment();
							continue;
						}
						else
						{
							token = ReadOperator('/');
						}
						break;
					case 'r':
						peek = ReaderPeek();
						if (peek == '"')
						{
							ReaderRead();
							token = ReadVerbatimString(peek);
							break;
						}
						else
							goto default;
					case '`':
						token = ReadVerbatimString(nextChar);
						break;
					case '"':
						token = ReadString(nextChar);
						break;
					case '\\':
						// http://digitalmars.com/d/1.0/lex.html#EscapeSequence
						// - It's actually deprecated, but parse such literals anyway
						string surr = "";
						x = Col - 1;
						y = Line;
						var lit = ReadEscapeSequence(out ch, out surr);
						token = Token(DTokens.Literal, x, y, lit.Length + 1, lit/*, ch.ToString()*/, LiteralFormat.StringLiteral);
						token.RawCodeRepresentation = ch.ToString();
						OnError(y, x, "Escape sequence strings are deprecated!");
						break;
					case '\'':
						token = ReadChar();
						break;
					case '@':
						token = Token(DTokens.At, Col - 1, Line, 1);
						break;
					case '#':
						if ((token = ReadSpecialTokenSequence()) != null)
							break;
						continue;
					default:
						ch = (char)nextChar;

						if (ch == 'x')
						{
							peek = ReaderPeek();
							if (peek == '"') // HexString
							{
								ReaderRead(); // Skip the "

								var numString = new StringBuilder();

								while ((next = ReaderRead()) != -1)
								{
									ch = (char)next;

									if (IsHex(ch))
										numString.Append(ch);
									else if (!Char.IsWhiteSpace(ch))
										break;
								}

								return Token(DTokens.Literal, Col - 1, Line, numString.Length + 1, ParseFloatValue(numString, 16), /*numString,*/ LiteralFormat.Scalar);
							}
						}
						else if (ch == 'q') // Token strings
						{
							switch (ReaderPeek())
							{
								case '{':
									return ReadTokenStringLiteral_CurlyInit();
								case '"'/* q"{{ ...}}   }}"*/:
									return ReadTokenStringLiteral_IdentInit();
							}
						}

						if (IsLetter(ch) || ch == '\\')
						{
							x = Col - 1; // Col was incremented above, but we want the start of the identifier
							y = Line;
							bool canBeKeyword;
							var s = ReadIdent(ch, out canBeKeyword);
							if (canBeKeyword)
							{
								// A micro-optimization..
								if (s.Length >= 8 && s[0] == '_' && s[1] == '_')
								{
									LiteralFormat literalFormat = 0;
									var subFormat = LiteralSubformat.Utf8;
									object literalValue = null;

									// Fill in static string surrogates directly
									if (s == "__VENDOR__")
									{
										literalFormat = LiteralFormat.StringLiteral;
										literalValue = "D Parser v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3) + " by Alexander Bothe";
									}
									else if (s == "__VERSION__")
									{
										subFormat = LiteralSubformat.Integer;
										var lexerVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
										literalFormat = LiteralFormat.Scalar;
										literalValue = lexerVersion.Major * 1000 + lexerVersion.Minor;
									}

									if (literalFormat != 0)
										return Token(DTokens.Literal, x, y, s.Length,
											literalValue,
											//literalValue is string ? (string)literalValue : literalValue.ToString(),
											literalFormat,
											subFormat);
								}

								byte key;
								if (DTokens.Keywords_Lookup.TryGetValue(s, out key))
									return Token(key, x, y, s.Length);
							}
							return Token(DTokens.Identifier, x, y, s);
						}
						else if (IsDigit(ch))
							token = ReadDigit(ch, Col - 1);
						else
							token = ReadOperator(ch);
						break;
				}

				// try error recovery (token = null -> continue with next char)
				if (token != null)
				{
					//token.prev = base.curToken;
					return token;
				}
				else
				{
					OnError(Line, Col, "Invalid character");
					//StopLexing();
					break;
				}
			}

			return Token(DTokens.EOF, Col, Line, 0);
		}

		DToken ReadTokenStringLiteral_IdentInit()
		{
			int x = Col - 1;
			int y = Line;

			ReaderRead(); // Skip "
			var tokenString = new StringBuilder();

			// Parse EOS
			byte eosToken;
			int eosTokenCount = 1;
			byte k;

			string eosIdentifier = null;
			var eos = new List<byte>();

			var tk = Next();
			k = tk.Kind;
			switch (tk.Kind)
			{
				case DTokens.Identifier:
					eosToken = DTokens.Identifier;
					eosIdentifier = (string)tk.LiteralValue;
					break;
				case DTokens.OpenCurlyBrace:
					eosToken = DTokens.CloseCurlyBrace;
					break;
				case DTokens.OpenSquareBracket:
					eosToken = DTokens.CloseSquareBracket;
					break;
				case DTokens.OpenParenthesis:
					eosToken = DTokens.CloseParenthesis;
					break;
				case DTokens.LessThan:
					eosToken = DTokens.GreaterThan;
					break;
				//TODO: << and <<<
				default:
					eosToken = DTokens.INVALID;
					break;
			}

			if (eosToken != 0 && eosToken != DTokens.Identifier)
				while ((tk = Next()).Kind == k)
					eosTokenCount++;

			int readEosTokenCount = 0;
			DToken backupToken = null;
			do
			{
				TokenStringParsing_AppendToken(tk, tokenString);
				next:
				switch (ReaderPeek())
				{
					case '\'':
					case '"':
					case '`': // TODO r" " missing
						tokenString.Append((char)ReaderRead());
						goto next;
				}

				tk = Next();

				if (tk.Kind == eosToken)
				{
					if (eosToken == DTokens.Identifier)
					{
						if ((string)tk.LiteralValue == eosIdentifier &&
							ReaderPeek() == (char)'\"')
						{
							ReaderRead();
							break;
						}
					}
					else if (++readEosTokenCount == eosTokenCount)
					{
						if (ReaderPeek() == (char)'\"')
						{
							ReaderRead();
							break;
						}
					}
					else
					{
						if (backupToken == null)
							backupToken = tk;
						else
							backupToken.next = tk;
						goto next;
					}
				}

				while (backupToken != null)
				{
					TokenStringParsing_AppendToken(backupToken, tokenString);
					backupToken = backupToken.Next;
				}
				readEosTokenCount = 0;
			}
			while (ReaderPeek() != -1);

			var subFmt = LiteralSubformat.Utf8;
			TryReadExplicitStringFormat(out subFmt);
			return Token(DTokens.Literal, x, y, Col, Line, tokenString.ToString().Trim(), LiteralFormat.VerbatimStringLiteral, subFmt);
		}

		DToken ReadTokenStringLiteral_CurlyInit()
		{
			int x = Col - 1;
			int y = Line;

			ReaderRead(); // Skip {
			var tokenString = new StringBuilder();
			int bracketLevel = 1;

			while (bracketLevel > 0 && ReaderPeek() != -1)
			{
				var tk = Next();
				switch (tk.Kind)
				{
					case DTokens.OpenCurlyBrace:
						bracketLevel++;
						break;
					case DTokens.CloseCurlyBrace:
						if (--bracketLevel < 1)
							continue;
						break;
				}

				TokenStringParsing_AppendToken(tk, tokenString);
			}

			var subFmt = LiteralSubformat.Utf8;
			TryReadExplicitStringFormat(out subFmt);
			return Token(DTokens.Literal, x, y, Col, Line, tokenString.ToString().Trim(), LiteralFormat.VerbatimStringLiteral, subFmt);
		}

		void TokenStringParsing_AppendToken(DToken tk, StringBuilder tokenString)
		{
			switch (tk.Kind)
			{
				case DTokens.Identifier:
					tokenString.Append(tk.LiteralValue);
					break;
				case DTokens.Literal:
					switch (tk.LiteralFormat)
					{
						case LiteralFormat.CharLiteral:
							tokenString.Append('\'').Append(tk.LiteralValue).Append('\'');
							break;
						case LiteralFormat.None:
						case LiteralFormat.Scalar:
						case LiteralFormat.FloatingPoint:
							tokenString.Append(tk.LiteralValue);
							break;
						case LiteralFormat.StringLiteral:
							tokenString.Append('\"').Append(tk.LiteralValue).Append('\"');
							break;
						case LiteralFormat.VerbatimStringLiteral:
							//TODO: More specific distinguishment between verbatim string types
							tokenString.Append('`').Append(tk.LiteralValue).Append('`');
							break;
					}

					TokenStringParsing_AppendLiteralSubFormat(tk, tokenString);
					break;
				default:
					tokenString.Append(DTokens.GetTokenString(tk.Kind));
					break;
			}

			tokenString.Append(' ');
		}

		void TokenStringParsing_AppendLiteralSubFormat(DToken tk, StringBuilder sb)
		{
			switch (tk.Subformat)
			{
				case LiteralSubformat.Utf8:
					break;
				case LiteralSubformat.Utf16:
					sb.Append('w');
					break;
				case LiteralSubformat.Utf32:
					sb.Append('d');
					break;
					//TODO
			}
		}

		string ReadIdent(char ch, out bool canBeKeyword)
		{
			int peek;
			int curPos = 0;
			canBeKeyword = true;
			while (true)
			{
				if (ch == '\\')
				{
					peek = ReaderPeek();
					if (peek != 'u' && peek != 'U')
					{
						OnError(Line, Col, "Identifiers can only contain unicode escape sequences");
					}
					canBeKeyword = false;
					string surrogatePair;
					ReadEscapeSequence(out ch, out surrogatePair);
					if (surrogatePair != null && surrogatePair.Length > 0)
					{
						if (!IsIdentifierPart(surrogatePair[0]))
						{
							OnError(Line, Col, "Unicode escape sequences in identifiers cannot be used to represent characters that are invalid in identifiers");
						}
						for (int i = 0; i < surrogatePair.Length - 1; i++)
						{
							if (curPos < MAX_IDENTIFIER_LENGTH)
							{
								identBuffer[curPos++] = surrogatePair[i];
							}
						}
						ch = surrogatePair[surrogatePair.Length - 1];
					}
					else
					{
						if (!IsIdentifierPart(ch))
						{
							OnError(Line, Col, "Unicode escape sequences in identifiers cannot be used to represent characters that are invalid in identifiers");
						}
					}
				}

				if (curPos < MAX_IDENTIFIER_LENGTH)
				{
					identBuffer[curPos++] = ch;
				}
				else
				{
					OnError(Line, Col, String.Format("Identifier too long"));
					while (IsIdentifierPart(ReaderPeek()))
					{
						ReaderRead();
					}
					break;
				}
				peek = ReaderPeek();
				if (IsIdentifierPart(peek) || peek == '\\')
				{
					ch = (char)ReaderRead();
				}
				else
				{
					break;
				}
			}
			return new String(identBuffer, 0, curPos);
		}

		DToken ReadDigit(char ch, int x)
		{
			if (!IsDigit(ch) && ch != '.')
			{
				OnError(Line, x, "Digit literals can only start with a digit (0-9) or a dot ('.')!");
				return null;
			}

			unchecked
			{ // prevent exception when ReaderPeek() = -1 is cast to char
				int y = Line;
				sb.Length = 0;
				sb.Append(ch);
				//string prefix = null;
				string expSuffix = "";
				string suffix = null;
				int exponent = 0;

				bool HasDot = false;
				LiteralSubformat subFmt = 0;
				bool isFloat = false;
				bool isImaginary = false;
				//bool isUnsigned = false;
				//bool isLong = false;
				byte numBase = 0; // Set it to 0 initially - it'll be set to another value later for sure

				char peek = (char)ReaderPeek();

				// At first, check pre-comma values
				if (ch == '0')
				{
					if (peek == 'x' || peek == 'X') // Hex values
					{
						//prefix = "0x";
						ReaderRead(); // skip 'x'
						sb.Length = 0; // Remove '0' from 0x prefix from the stringvalue
						numBase = 16;

						peek = (char)ReaderPeek();
						while (IsHex(peek) || peek == '_')
						{
							if (peek != '_')
								sb.Append((char)ReaderRead());
							else ReaderRead();
							peek = (char)ReaderPeek();
						}
					}
					else if (peek == 'b' || peek == 'B') // Bin values
					{
						//prefix = "0b";
						ReaderRead(); // skip 'b'
						sb.Length = 0;
						numBase = 2;

						peek = (char)ReaderPeek();
						while (IsBin(peek) || peek == '_')
						{
							if (peek != '_')
								sb.Append((char)ReaderRead());
							else ReaderRead();
							peek = (char)ReaderPeek();
						}
					}
					// Oct values have been removed in dmd 2.053
					/*else if (IsOct(peek) || peek == '_') // Oct values
					{
						NumBase = 8;
						prefix = "0";
						sb.Length = 0;

						while (IsOct(peek) || peek == '_')
						{
							if (peek != '_')
								sb.Append((char)ReaderRead());
							else ReaderRead();
							peek = (char)ReaderPeek();
						}
					}*/
					else
						numBase = 10; // Enables pre-comma parsing .. in this case we'd 000 literals or something like that
				}

				if (numBase == 10 || (ch != '.' && numBase == 0)) // Only allow further digits for 10-based integers, not for binary or hex values
				{
					numBase = 10;
					while (IsDigit(peek) || peek == '_')
					{
						if (peek != '_')
							sb.Append((char)ReaderRead());
						else ReaderRead();
						peek = (char)ReaderPeek();
					}
				}

				#region Read digits that occur after a comma
				DToken nextToken = null; // if we accidently read a 'dot'
				bool AllowSuffixes = true;
				if ((numBase == 0 && ch == '.') || peek == '.')
				{
					if (ch != '.') ReaderRead();
					else
					{
						numBase = 10;
						sb.Length = 0;
						sb.Append('0');
					}
					peek = (char)ReaderPeek();
					if (!IsLegalDigit(peek, numBase))
					{
						if (peek == '.')
						{
							ReaderRead();
							nextToken = Token(DTokens.DoubleDot, Col - 1, Line, 2);
						}
						else if (IsIdentifierPart(peek))
							nextToken = Token(DTokens.Dot, Col - 1, Line, 1);

						AllowSuffixes = false;
					}
					else
					{
						HasDot = true;
						sb.Append('.');

						do
						{
							if (peek == '_')
								ReaderRead();
							else
								sb.Append((char)ReaderRead());
							peek = (char)ReaderPeek();
						}
						while (IsLegalDigit(peek, numBase));
					}
				}
				#endregion

				#region Exponents
				if ((numBase == 16) ? (peek == 'p' || peek == 'P') : (peek == 'e' || peek == 'E'))
				{ // read exponent
					string suff = peek.ToString();
					ReaderRead();
					peek = (char)ReaderPeek();

					if (peek == '-' || peek == '+')
						expSuffix += (char)ReaderRead();
					peek = (char)ReaderPeek();
					while ((peek >= '0' && peek <= '9') || peek == '_')
					{ // read exponent value
						if (peek == '_')
							ReaderRead();
						else
							expSuffix += (char)ReaderRead();
						peek = (char)ReaderPeek();
					}

					// Exponents just can be decimal integers
					int.TryParse(expSuffix, out exponent);
					expSuffix = suff + expSuffix;
					peek = (char)ReaderPeek();
				}
				#endregion

				#region Suffixes
				if (!HasDot)
				{
					unsigned:
					if (peek == 'u' || peek == 'U')
					{
						ReaderRead();
						suffix += "u";
						subFmt |= LiteralSubformat.Unsigned;
						//isUnsigned = true;
						peek = (char)ReaderPeek();
					}

					if (peek == 'L')
					{
						subFmt |= LiteralSubformat.Long;
						ReaderRead();
						suffix += "L";
						//isLong = true;
						peek = (char)ReaderPeek();
						if (!subFmt.HasFlag(LiteralSubformat.Unsigned) && (peek == 'u' || peek == 'U'))
							goto unsigned;
					}
				}

				if (HasDot || AllowSuffixes)
				{
					if (peek == 'f' || peek == 'F')
					{ // float value
						ReaderRead();
						suffix += "f";
						isFloat = true;
						subFmt |= LiteralSubformat.Float;
						peek = (char)ReaderPeek();
					}
					else if (peek == 'L')
					{ // real value
						ReaderRead();
						//isLong = true;
						suffix += 'L';
						subFmt |= LiteralSubformat.Real;
						peek = (char)ReaderPeek();
					}
				}

				if (peek == 'i')
				{ // imaginary value
					ReaderRead();
					suffix += "i";

					subFmt |= LiteralSubformat.Imaginary;
					isImaginary = true;
				}
				#endregion

				#region Parse the digit string
				var num = ParseFloatValue(sb, numBase);

				if (exponent != 0)
				{
					try
					{
						num *= (decimal)Math.Pow(numBase == 16 ? 2 : 10, exponent);
					}
					catch (OverflowException)
					{
						num = decimal.MaxValue;
						//HACK: Don't register these exceptions. The user will notice the issues at least when compiling stuff.
						//LexerErrors.Add(new ParserError(false, "Too huge exponent", DTokens.Literal, new CodeLocation(x,y)));
					}
				}
				#endregion

				var token = Token(DTokens.Literal, x, y, Col - x/*stringValue.Length*/, num,/* stringValue,*/
					HasDot || isFloat || isImaginary ? (LiteralFormat.FloatingPoint | LiteralFormat.Scalar) : LiteralFormat.Scalar,
					subFmt);

				if (token != null)
					token.next = nextToken;

				return token;
			}
		}

		void TryReadExplicitStringFormat(out LiteralSubformat subFmt)
		{
			switch ((char)this.ReaderPeek())
			{
				case 'c':
					ReaderRead();
					break;
				case 'd':
					subFmt = LiteralSubformat.Utf32;
					ReaderRead();
					return;
				case 'w':
					subFmt = LiteralSubformat.Utf16;
					ReaderRead();
					return;
			}

			subFmt = LiteralSubformat.Utf8;
		}

		DToken ReadString(int initialChar)
		{
			int x = Col - 1;
			int y = Line;

			sb.Length = 0;
			//originalValue.Length = 0;
			//originalValue.Append((char)initialChar);
			bool doneNormally = false;
			int nextChar;
			var subFmt = LiteralSubformat.Utf8;
			while ((nextChar = ReaderRead()) != -1)
			{
				char ch = (char)nextChar;

				if (nextChar == initialChar)
				{
					doneNormally = true;
					//originalValue.Append((char)nextChar);
					// Skip string literals
					TryReadExplicitStringFormat(out subFmt);
					break;
				}
				HandleLineEnd(ch);
				if (ch == '\\')
				{
					//originalValue.Append('\\');
					string surrogatePair;

					//originalValue.Append(ReadEscapeSequence(out ch, out surrogatePair));
					sb.Append('\\').Append(ReadEscapeSequence(out ch, out surrogatePair));
					/*if (surrogatePair != null)
					{
						sb.Append(surrogatePair);
					}
					else
					{
						sb.Append(ch);
					}*/
				}
				else
				{
					//originalValue.Append(ch);
					sb.Append(ch);
				}
			}

			if (!doneNormally)
			{
				OnError(y, x, String.Format("End of file reached inside string literal"));
			}

			return Token(DTokens.Literal, x, y, Col, Line, /*originalValue.ToString(),*/ sb.ToString(), LiteralFormat.StringLiteral, subFmt);
		}

		DToken ReadVerbatimString(int EndingChar)
		{
			sb.Length = 0;
			//originalValue.Length = 0;
			int x = Col - 2; // r and " already read
			int y = Line;
			int nextChar;

			if (EndingChar == (int)'"')
			{
				//originalValue.Append("r\"");
			}
			else
			{
				//originalValue.Append((char)EndingChar);
				x = Col - 1;
			}
			while ((nextChar = ReaderRead()) != -1)
			{
				char ch = (char)nextChar;

				if (nextChar == EndingChar)
				{
					if (ReaderPeek() != (char)EndingChar)
					{
						//originalValue.Append((char)EndingChar);
						break;
					}
					//originalValue.Append((char)EndingChar);					originalValue.Append((char)EndingChar);
					sb.Append((char)EndingChar);
					ReaderRead();
				}
				else if (HandleLineEnd(ch))
				{
					sb.Append("\r\n");
					//originalValue.Append("\r\n");
				}
				else
				{
					sb.Append(ch);
					//originalValue.Append(ch);
				}
			}

			if (nextChar == -1)
			{
				OnError(y, x, String.Format("End of file reached inside verbatim string literal"));
			}

			// Suffix literal check
			LiteralSubformat subFmt;
			TryReadExplicitStringFormat(out subFmt);

			return Token(DTokens.Literal, x, y, Col, Line, /*originalValue.ToString(),*/ sb.ToString(), LiteralFormat.VerbatimStringLiteral, subFmt);
		}

		/// <summary>
		/// reads an escape sequence
		/// </summary>
		/// <param name="ch">The character represented by the escape sequence,
		/// or '\0' if there was an error or the escape sequence represents a character that
		/// can be represented only be a suggorate pair</param>
		/// <param name="surrogatePair">Null, except when the character represented
		/// by the escape sequence can only be represented by a surrogate pair (then the string
		/// contains the surrogate pair)</param>
		/// <returns>The escape sequence</returns>
		string ReadEscapeSequence(out char ch, out string surrogatePair)
		{
			surrogatePair = null;

			int nextChar = ReaderRead();
			if (nextChar == -1)
			{
				OnError(Line, Col, String.Format("End of file reached inside escape sequence"));
				ch = '\0';
				return String.Empty;
			}
			int number;
			char c = (char)nextChar;
			int curPos = 1;
			escapeSequenceBuffer[0] = c;
			switch (c)
			{
				case '\'':
					ch = '\'';
					break;
				case '\"':
					ch = '\"';
					break;
				case '?':
					ch = '?';
					return "\\?"; // Literal question mark
				case '\\':
					ch = '\\';
					break;
				/*case '0':
					ch = '\0';
					break;*/
				case 'a':
					ch = '\a'; // Bell (alert)
					break;
				case 'b':
					ch = '\b'; // Backspace
					break;
				case 'f':
					ch = '\f'; // Formfeed
					break;
				case 'n':
					ch = '\n';
					break;
				case 'r':
					ch = '\r';
					break;
				case 't':
					ch = '\t';
					break;
				case 'v':
					ch = '\v'; // Vertical tab
					break;
				case 'u':
				case 'x':
					// 16 bit unicode character
					c = (char)ReaderRead();
					number = GetHexNumber(c);
					escapeSequenceBuffer[curPos++] = c;

					if (number < 0)
					{
						OnError(Line, Col - 1, String.Format("Invalid char in literal : {0}", c));
					}
					for (int i = 0; i < 3; ++i)
					{
						if (IsHex((char)ReaderPeek()))
						{
							c = (char)ReaderRead();
							int idx = GetHexNumber(c);
							escapeSequenceBuffer[curPos++] = c;
							number = 16 * number + idx;
						}
						else
						{
							break;
						}
					}
					ch = (char)number;
					break;
				case 'U':
					// 32 bit unicode character
					number = 0;
					for (int i = 0; i < 8; ++i)
					{
						if (IsHex((char)ReaderPeek()))
						{
							c = (char)ReaderRead();
							int idx = GetHexNumber(c);
							escapeSequenceBuffer[curPos++] = c;
							number = 16 * number + idx;
						}
						else
						{
							OnError(Line, Col - 1, String.Format("Invalid char in literal : {0}", (char)ReaderPeek()));
							break;
						}
					}
					if (number > 0xffff)
					{
						ch = '\0';
						surrogatePair = char.ConvertFromUtf32(number);
					}
					else
					{
						ch = (char)number;
					}
					break;

				// NamedCharacterEntities
				case '&':
					string charEntity = "";

					while (true)
					{
						nextChar = ReaderRead();

						if (nextChar < 0)
						{
							OnError(Line, Col - 1, "EOF reached within named char entity");
							ch = '\0';
							return string.Empty;
						}

						c = (char)nextChar;

						if (c == ';')
							break;

						if (IsIdentifierPart(c))
							charEntity += c;
						else
						{
							OnError(Line, Col - 1, "Unexpected character found in named char entity: " + c);
							ch = '\0';
							return string.Empty;
						}
					}

					if (string.IsNullOrEmpty(charEntity))
					{
						OnError(Line, Col - 1, "Empty named character entities not allowed");
						ch = '\0';
						return string.Empty;
					}

					//TODO: Performance improvement
					//var ret=System.Web.HttpUtility.HtmlDecode("&"+charEntity+";");

					ch = '#';//ret[0];

					return "&" + charEntity + ";";
				default:

					// Max 3 following octal digits
					if (IsOct(c))
					{
						// Parse+Convert oct to dec integer
						int oct = GetHexNumber(c);

						for (int i = 0; i < 2; ++i)
						{
							if (IsOct((char)ReaderPeek()))
							{
								c = (char)ReaderRead();
								escapeSequenceBuffer[curPos++] = c;

								int idx = GetHexNumber(c);
								oct = 8 * oct + idx;
							}
							else
								break;
						}

						// Convert integer to character
						if (oct > 0xffff)
						{
							ch = '\0';
							surrogatePair = char.ConvertFromUtf32(oct);
						}
						else
						{
							ch = (char)oct;
						}

					}
					else
					{
						OnError(Line, Col, String.Format("Unexpected escape sequence : {0}", c));
						ch = '\0';
					}
					break;
			}
			return new String(escapeSequenceBuffer, 0, curPos);
		}

		DToken ReadChar()
		{
			int x = Col - 1;
			int y = Line;
			int nextChar = ReaderRead();
			if (nextChar == -1)
			{
				OnError(y, x, String.Format("End of file reached inside character literal"));
				return null;
			}
			char ch = (char)nextChar;
			char chValue = ch;
			string escapeSequence = null;
			string surrogatePair = null;
			if (ch == '\\')
			{
				escapeSequence = ReadEscapeSequence(out chValue, out surrogatePair);
				if (surrogatePair != null)
				{
					// Although we'll pass back a string as literal value, it's originally handled as char literal!
				}
			}
			else if (ch == '\'')
			{
				return Token(DTokens.Literal, x, y, 2, char.MinValue, "''", LiteralFormat.CharLiteral);
			}
			else if (ch >= 0xd800 && ch <= 0xdbff)
			{
				int nextch = reader.Read();
				if (nextch < 0xdc00 || nextch > 0xdfff)
					OnError(Line, Col, "Invalid unicode character");

				int dchar = (((ch & 0x3ff) << 10) | (nextch & 0x3ff)) + 0x10000;
				surrogatePair = new String((char)ch, 1) + (char)nextch; // String.Empty.dchar;
			}

			unchecked
			{
				if ((char)ReaderPeek() != '\'')
					OnError(y, x, String.Format("Char not terminated"));

				int n;
				while (!((n = ReaderRead()) == '\'' || n == -1)) ;
			}
			return Token(DTokens.Literal, x, y, Col, Line, string.IsNullOrEmpty(surrogatePair) ? (object)chValue : surrogatePair, LiteralFormat.CharLiteral, surrogatePair == null ? LiteralSubformat.Utf8 : LiteralSubformat.Utf16, escapeSequence);
		}

		DToken ReadOperator(char ch)
		{
			int x = Col - 1;
			int y = Line;
			switch (ch)
			{
				case '+':
					switch (ReaderPeek())
					{
						case '+':
							ReaderRead();
							return Token(DTokens.Increment, x, y);
						case '=':
							ReaderRead();
							return Token(DTokens.PlusAssign, x, y);
					}
					return Token(DTokens.Plus, x, y);
				case '-':
					switch (ReaderPeek())
					{
						case '-':
							ReaderRead();
							return Token(DTokens.Decrement, x, y);
						case '=':
							ReaderRead();
							return Token(DTokens.MinusAssign, x, y);
						case '>':
							ReaderRead();
							return Token(DTokens.TildeAssign, x, y);
					}
					return Token(DTokens.Minus, x, y);
				case '*':
					switch (ReaderPeek())
					{
						case '=':
							ReaderRead();
							return Token(DTokens.TimesAssign, x, y, 2);
						default:
							break;
					}
					return Token(DTokens.Times, x, y);
				case '/':
					switch (ReaderPeek())
					{
						case '=':
							ReaderRead();
							return Token(DTokens.DivAssign, x, y, 2);
					}
					return Token(DTokens.Div, x, y);
				case '%':
					switch (ReaderPeek())
					{
						case '=':
							ReaderRead();
							return Token(DTokens.ModAssign, x, y, 2);
					}
					return Token(DTokens.Mod, x, y);
				case '&':
					switch (ReaderPeek())
					{
						case '&':
							ReaderRead();
							return Token(DTokens.LogicalAnd, x, y, 2);
						case '=':
							ReaderRead();
							return Token(DTokens.BitwiseAndAssign, x, y, 2);
					}
					return Token(DTokens.BitwiseAnd, x, y);
				case '|':
					switch (ReaderPeek())
					{
						case '|':
							ReaderRead();
							return Token(DTokens.LogicalOr, x, y, 2);
						case '=':
							ReaderRead();
							return Token(DTokens.BitwiseOrAssign, x, y, 2);
					}
					return Token(DTokens.BitwiseOr, x, y);
				case '^':
					switch (ReaderPeek())
					{
						case '=':
							ReaderRead();
							return Token(DTokens.XorAssign, x, y, 2);
						case '^':
							ReaderRead();
							if (ReaderPeek() == '=')
							{
								ReaderRead();
								return Token(DTokens.PowAssign, x, y, 3);
							}
							return Token(DTokens.Pow, x, y, 2);
					}
					return Token(DTokens.Xor, x, y);
				case '!':
					switch (ReaderPeek())
					{
						case '=':
							ReaderRead();
							return Token(DTokens.NotEqual, x, y, 2); // !=

						case '<':
							ReaderRead();
							switch (ReaderPeek())
							{
								case '=':
									ReaderRead();
									return Token(DTokens.UnorderedOrGreater, x, y, 3); // !<=
								case '>':
									ReaderRead();
									switch (ReaderPeek())
									{
										case '=':
											ReaderRead();
											return Token(DTokens.Unordered, x, y, 4); // !<>=
									}
									return Token(DTokens.UnorderedOrEqual, x, y, 3); // !<>
							}
							return Token(DTokens.UnorderedGreaterOrEqual, x, y, 2); // !<

						case '>':
							ReaderRead();
							switch (ReaderPeek())
							{
								case '=':
									ReaderRead();
									return Token(DTokens.UnorderedOrLess, x, y, 3); // !>=
								default:
									break;
							}
							return Token(DTokens.UnorderedLessOrEqual, x, y, 2); // !>

					}
					return Token(DTokens.Not, x, y);
				case '~':
					switch (ReaderPeek())
					{
						case '=':
							ReaderRead();
							return Token(DTokens.TildeAssign, x, y, 2);
					}
					return Token(DTokens.Tilde, x, y);
				case '=':
					switch (ReaderPeek())
					{
						case '=':
							ReaderRead();
							return Token(DTokens.Equal, x, y, 2);
						case '>':
							ReaderRead();
							return Token(DTokens.GoesTo, x, y, 2);
					}
					return Token(DTokens.Assign, x, y);
				case '<':
					switch (ReaderPeek())
					{
						case '<':
							ReaderRead();
							switch (ReaderPeek())
							{
								case '=':
									ReaderRead();
									return Token(DTokens.ShiftLeftAssign, x, y, 3);
								default:
									break;
							}
							return Token(DTokens.ShiftLeft, x, y, 2);
						case '>':
							ReaderRead();
							switch (ReaderPeek())
							{
								case '=':
									ReaderRead();
									return Token(DTokens.LessEqualOrGreater, x, y, 3);
								default:
									break;
							}
							return Token(DTokens.LessOrGreater, x, y, 2);
						case '=':
							ReaderRead();
							return Token(DTokens.LessEqual, x, y, 2);
					}
					return Token(DTokens.LessThan, x, y);
				case '>':
					switch (ReaderPeek())
					{
						case '>':
							ReaderRead();
							if (ReaderPeek() != -1)
							{
								switch ((char)ReaderPeek())
								{
									case '=':
										ReaderRead();
										return Token(DTokens.ShiftRightAssign, x, y, 3);
									case '>':
										ReaderRead();
										if (ReaderPeek() != -1)
										{
											switch ((char)ReaderPeek())
											{
												case '=':
													ReaderRead();
													return Token(DTokens.TripleRightShiftAssign, x, y, 4);
											}
											return Token(DTokens.ShiftRightUnsigned, x, y, 3); // >>>
										}
										break;
								}
							}
							return Token(DTokens.ShiftRight, x, y, 2);
						case '=':
							ReaderRead();
							return Token(DTokens.GreaterEqual, x, y, 2);
					}
					return Token(DTokens.GreaterThan, x, y);
				case '?':
					return Token(DTokens.Question, x, y);
				case '$':
					return Token(DTokens.Dollar, x, y);
				case ';':
					return Token(DTokens.Semicolon, x, y);
				case ':':
					return Token(DTokens.Colon, x, y);
				case ',':
					return Token(DTokens.Comma, x, y);
				case '.':
					// Prevent OverflowException when ReaderPeek returns -1
					int tmp = ReaderPeek();
					if (tmp > 0 && IsDigit((char)tmp))
						return ReadDigit('.', Col - 1);
					else if (tmp == (int)'.')
					{
						ReaderRead();
						if ((char)ReaderPeek() == '.') // Triple dot
						{
							ReaderRead();
							return Token(DTokens.TripleDot, x, y, 3);
						}
						return Token(DTokens.DoubleDot, x, y, 2);
					}
					return Token(DTokens.Dot, x, y);
				case ')':
					return Token(DTokens.CloseParenthesis, x, y);
				case '(':
					return Token(DTokens.OpenParenthesis, x, y);
				case ']':
					return Token(DTokens.CloseSquareBracket, x, y);
				case '[':
					return Token(DTokens.OpenSquareBracket, x, y);
				case '}':
					return Token(DTokens.CloseCurlyBrace, x, y);
				case '{':
					return Token(DTokens.OpenCurlyBrace, x, y);
				default:
					return null;
			}
		}

		void ReadComment()
		{
			switch (ReaderRead())
			{
				case '+':
					if (ReaderPeek() == '+')// DDoc
						ReadMultiLineComment(Comment.Type.Documentation | Comment.Type.Block, true);
					else
						ReadMultiLineComment(Comment.Type.Block, true);
					break;
				case '*':
					if (ReaderPeek() == '*')// DDoc
						ReadMultiLineComment(Comment.Type.Documentation | Comment.Type.Block, false);
					else
						ReadMultiLineComment(Comment.Type.Block, false);
					break;
				case '/':
					if (ReaderPeek() == '/')// DDoc
					{
						ReaderRead();
						ReadSingleLineComment(Comment.Type.Documentation | Comment.Type.SingleLine);
					}
					else
						ReadSingleLineComment(Comment.Type.SingleLine);
					break;
				default:
					OnError(Line, Col, String.Format("Error while reading comment"));
					break;
			}
		}

		void ReadSingleLineComment(Comment.Type commentType)
		{
			int tagLen = ((commentType & Comment.Type.Documentation) != 0 ? 3 : 2);
			var st = new CodeLocation(Col - tagLen, Line);
			string comm = ReadToEndOfLine();
			var end = new CodeLocation(st.Column + tagLen + comm.Length, Line);

			if (reader.Peek() == -1 && st.Line == end.Line)
				endedWhileBeingInNonCodeSequence = true;

			if ((commentType & Comment.Type.Documentation) != 0 || !OnlyEnlistDDocComments)
				Comments.Add(new Comment(commentType, comm.TrimStart('/', ' ', '\t'), st.Column < 2, st, end));
		}

		struct TaskParseState
		{
			readonly HashSet<string> TaskTokens;
			int taskStart;
			int taskCol;
			string task;

			public TaskParseState(HashSet<string> tokens)
			{
				TaskTokens = tokens;
				taskStart = -1;
				taskCol = 0;
				task = null;
			}

			public void doNextChar(int nextChar, int Col, StringBuilder scCurWord)
			{
				if (TaskTokens.Count != 0 && task == null)
				{
					if (IsIdentifierPart(nextChar) || nextChar == '@')
					{
						if (taskStart == -1)
						{
							taskCol = Col;
							taskStart = scCurWord.Length;
						}
					}
					else if (taskStart >= 0)
					{
						string word = scCurWord.ToString(taskStart, scCurWord.Length - taskStart);
						if (TaskTokens.Contains(word))
							task = word;
						if (task == null)
							taskStart = -1;
					}
				}
			}

			public void endOfLineOrComment(int Line, StringBuilder scCurWord, List<ParserError> Tasks)
			{
				if (task != null)
				{
					string comment = scCurWord.ToString(taskStart, scCurWord.Length - taskStart);
					Tasks.Add(new ParserError(false, comment, 0, new CodeLocation(taskCol, Line)));
					taskStart = -1;
					task = null;
				}
			}
		}

		void ReadMultiLineComment(Comment.Type commentType, bool isNestingComment)
		{
			int nestedCommentDepth = 1;
			int nextChar;
			CodeLocation st = new CodeLocation(Col - ((commentType & Comment.Type.Documentation) != 0 ? 3 : 2), Line);
			StringBuilder scCurWord = new StringBuilder(); // current word, (scTag == null) or comment (when scTag != null)
			bool hadLineEnd = false;

			TaskParseState taskState = new TaskParseState(TaskTokens);

			while ((nextChar = ReaderRead()) != -1)
			{
				taskState.doNextChar(nextChar, Col, scCurWord);

				char ch = (char)nextChar;

				// Catch deeper-nesting comments
				if (isNestingComment && ch == '/' && ReaderPeek() == '+')
				{
					nestedCommentDepth++;
					ReaderRead();
				}

				// End of multiline comment reached ?
				if ((isNestingComment ? ch == '+' : ch == '*') && ReaderPeek() == '/')
				{
					taskState.endOfLineOrComment(Line, scCurWord, Tasks);

					ReaderRead(); // Skip "*" or "+"

					if (nestedCommentDepth > 1)
						nestedCommentDepth--;
					else
					{
						if (commentType.HasFlag(Comment.Type.Documentation) || !OnlyEnlistDDocComments)
							Comments.Add(new Comment(commentType, scCurWord.ToString().Trim(ch, ' ', '\t', '\r', '\n', isNestingComment ? '+' : '*'), st.Column < 2, st, new CodeLocation(Col, Line)));
						return;
					}
				}

				if (HandleLineEnd(ch))
				{
					taskState.endOfLineOrComment(Line - 1, scCurWord, Tasks);
					scCurWord.AppendLine();
					hadLineEnd = true;
				}

				// Skip intial white spaces, leading + as well as *
				else if (hadLineEnd)
				{
					if (char.IsWhiteSpace(ch) || (isNestingComment ? ch == '+' : ch == '*'))
					{ }
					else
					{
						scCurWord.Append(ch);
						hadLineEnd = false;
					}
				}
				else
					scCurWord.Append(ch);
			}

			taskState.endOfLineOrComment(Line, scCurWord, Tasks);

			// Reached EOF before end of multiline comment.
			if (commentType.HasFlag(Comment.Type.Documentation) || !OnlyEnlistDDocComments)
				Comments.Add(new Comment(commentType, scCurWord.ToString().Trim(), st.Column < 2, st, new CodeLocation(Col, Line)));

			OnError(Line, Col, String.Format("Reached EOF before the end of a multiline comment"));
			endedWhileBeingInNonCodeSequence = true;
		}

		/// <summary>
		/// http://dlang.org/lex.html#SpecialTokenSequence
		/// </summary>
		DToken ReadSpecialTokenSequence()
		{
			int x = Col - 1;
			int startLine = Line;

			var nextToken = Next();

			if (nextToken.Kind != DTokens.Identifier)
			{
				OnError(nextToken.Line, nextToken.Column, "Identifier expected");
				return null;
			}

			switch (nextToken.Value)
			{
				/*
				 * This sets the source line number to IntegerLiteral, 
				 * and optionally the source file name to Filespec, 
				 * beginning with the next line of source text. 
				 * The source file and line number is used for printing error messages 
				 * and for mapping generated code back to the source for the symbolic debugging output.
				 */
				case "line":
					nextToken = Next();

					// nextToken can be __LINE__ or 123 now.

					//TODO: How to handle this then properly? Only set Line to digit's value?
					var prevLine = Line;
					nextToken = Next(); //ISSUE: Successive #line tokens will be skipped here - perhaps detect if there's a special token following?

					if (nextToken.Line > prevLine ||
						nextToken.Kind != DTokens.Literal || nextToken.LiteralFormat != LiteralFormat.StringLiteral)
						return nextToken;
					break;
				default:
					OnError(nextToken.Line, nextToken.Column, "Invalid special token sequence");
					break;
			}

			return null;
		}
		#endregion

		void OnError(int line, int col, string message)
		{
			endedWhileBeingInNonCodeSequence |= reader.Peek() < 0;
			if (LexerErrors != null)
				LexerErrors.Add(new ParserError(false, message, CurrentToken != null ? CurrentToken.Kind : -1, new CodeLocation(col, line)));
		}

		#region Helpers
		/// <summary>
		/// Also accepts underscores!
		/// </summary>
		public static bool IsLetter(int ch)
		{
			switch (ch)
			{
				case 'a':
				case 'A':
				case 'b':
				case 'B':
				case 'c':
				case 'C':
				case 'd':
				case 'D':
				case 'e':
				case 'E':
				case 'f':
				case 'F':
				case 'g':
				case 'G':
				case 'h':
				case 'H':
				case 'i':
				case 'I':
				case 'j':
				case 'J':
				case 'k':
				case 'K':
				case 'l':
				case 'L':
				case 'm':
				case 'M':
				case 'n':
				case 'N':
				case 'o':
				case 'O':
				case 'p':
				case 'P':
				case 'q':
				case 'Q':
				case 'r':
				case 'R':
				case 's':
				case 'S':
				case 't':
				case 'T':
				case 'u':
				case 'U':
				case 'v':
				case 'V':
				case 'w':
				case 'W':
				case 'x':
				case 'X':
				case 'y':
				case 'Y':
				case 'z':
				case 'Z':
				case '_':
					return true;
				case ' ':
				case '@':
				case '/':
				case '(':
				case ')':
				case '[':
				case ']':
				case '{':
				case '}':
				case '=':
				case '\"':
				case '\'':
				case -1:
					return false;
				default:
					return char.IsLetter((char)ch);
			}
		}

		public static bool IsIdentifierPart(int ch)
		{
			return IsDigit((char)ch) || IsLetter(ch);
		}

		public static bool IsDigit(char digit)
		{
			switch (digit)
			{
				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
					return true;
				default:
					return false;
			}
		}

		public static bool IsOct(char digit)
		{
			return IsDigit(digit) && digit != '9' && digit != '8';
		}

		public static bool IsHex(char digit)
		{
			switch (digit)
			{
				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
				case 'a':
				case 'A':
				case 'b':
				case 'B':
				case 'c':
				case 'C':
				case 'd':
				case 'D':
				case 'e':
				case 'E':
				case 'f':
				case 'F':
					return true;
				default:
					return false;
			}
		}

		public static bool IsBin(char digit)
		{
			return digit == '0' || digit == '1';
		}

		/// <summary>
		/// Tests if digit <para>d</para> is allowed in the specified numerical base.
		/// If <para>NumBase</para> is 10, only digits from 0 to 9 would be allowed.
		/// If NumBase=2, 0 and 1 are legal.
		/// If NumBase=8, 0 to 7 are legal.
		/// If NumBase=16, 0 to 9 and a to f are allowed.
		/// Note: Underscores ('_') are legal everytime!
		/// </summary>
		/// <param name="d"></param>
		/// <param name="NumBase"></param>
		/// <returns></returns>
		public static bool IsLegalDigit(char d, int NumBase)
		{
			if (d == '_')
				return true;
			switch (NumBase)
			{
				case 2:
					return IsBin(d);
				case 10:
					return IsDigit(d);
				case 16:
					return IsHex(d);
				default:
					return false;
			}
		}

		public static int GetHexNumber(char digit)
		{
			switch (digit)
			{
				case '0':
					return 0;
				case '1':
					return 1;
				case '2':
					return 2;
				case '3':
					return 3;
				case '4':
					return 4;
				case '5':
					return 5;
				case '6':
					return 6;
				case '7':
					return 7;
				case '8':
					return 8;
				case '9':
					return 9;
				case 'a':
				case 'A':
					return 10;
				case 'b':
				case 'B':
					return 11;
				case 'c':
				case 'C':
					return 12;
				case 'd':
				case 'D':
					return 13;
				case 'e':
				case 'E':
					return 14;
				case 'f':
				case 'F':
					return 15;
				default:
					//errors.Error(line, col, String.Format("Invalid hex number '" + digit + "'"));
					return 0;
			}
		}

		public static decimal ParseFloatValue(StringBuilder digit, byte numBase)
		{
			decimal ret = 0M;

			int commaPos = -1;
			// Determine optional comma index
			for (int i = digit.Length - 1; i >= 0; i--)
				if (digit[i] == '.')
				{
					commaPos = i;
					break;
				}

			// Calculate pre-comma part
			int maxPreCommaIndex = commaPos > -1 ? commaPos : digit.Length;
			for (int i = 0; i < maxPreCommaIndex; i++)
			{
				ret += GetHexNumber(digit[i]);
				ret *= numBase;
			}
			ret /= numBase;

			if (commaPos > -1)
			{
				// Calculate & Add floating-point part
				decimal postCommaMantissa = 0M;
				for (int i = digit.Length - 1; i > commaPos; i--)
				{
					postCommaMantissa += GetHexNumber(digit[i]);
					postCommaMantissa /= numBase;
				}
				ret += postCommaMantissa;
			}

			return ret;
		}

		#endregion
	}
}
