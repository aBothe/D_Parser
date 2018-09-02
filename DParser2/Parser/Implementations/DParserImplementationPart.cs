using System;
using System.Collections.Generic;
using System.Text;
using D_Parser.Dom;

namespace D_Parser.Parser.Implementations
{
	internal class DParserImplementationPart
	{
		readonly DParserStateContext stateContext;

		public DParserImplementationPart(DParserStateContext stateContext)
		{
			this.stateContext = stateContext;
			Lexer = stateContext.Lexer;
		}

		/// <summary>
		/// Modifiers for entire block
		/// </summary>
		protected Stack<DAttribute> BlockAttributes
		{
			get { return stateContext.BlockAttributes; }
			set { stateContext.BlockAttributes = value; }
		}
		/// <summary>
		/// Modifiers for current expression only
		/// </summary>
		protected Stack<DAttribute> DeclarationAttributes => stateContext.DeclarationAttributes;

		protected bool AllowWeakTypeParsing {
			get { return stateContext.AllowWeakTypeParsing; }
			set { stateContext.AllowWeakTypeParsing = value; }
		}
		protected bool ParseStructureOnly => stateContext.ParseStructureOnly;
		protected readonly Lexer Lexer;

		protected List<Comment> Comments => stateContext.Comments;

		protected DToken t => stateContext.t;

		/// <summary>
		/// lookAhead token
		/// </summary>
		protected DToken la
		{
			get
			{
				return Lexer.LookAhead;
			}
		}

		protected byte laKind { get { return Lexer.laKind; } }

		protected bool IsEOF
		{
			get { return Lexer.IsEOF; }
		}

		protected List<ParserError> ParseErrors => stateContext.ParseErrors;


		protected bool OverPeekBrackets(byte OpenBracketKind, bool LAIsOpenBracket = false)
		{
			int CloseBracket = DTokens.CloseParenthesis;

			if (OpenBracketKind == DTokens.OpenSquareBracket)
				CloseBracket = DTokens.CloseSquareBracket;
			else if (OpenBracketKind == DTokens.OpenCurlyBrace)
				CloseBracket = DTokens.CloseCurlyBrace;

			var pk = Lexer.CurrentPeekToken;
			int i = LAIsOpenBracket ? 1 : 0;
			while (pk.Kind != DTokens.EOF)
			{
				if (pk.Kind == OpenBracketKind)
					i++;
				else if (pk.Kind == CloseBracket)
				{
					i--;
					if (i <= 0)
					{
						Peek();
						return true;
					}
				}
				pk = Peek();
			}
			return false;
		}

		protected bool Expect(byte n)
		{
			if (laKind == n)
			{
				Step();
				return true;
			}
			else
			{
				SynErr(n, DTokens.GetTokenString(n) + " expected, " + DTokens.GetTokenString(laKind) + " found!");
			}
			return false;
		}

		/// <summary>
		/// Retrieve string value of current token
		/// </summary>
		protected string strVal
		{
			get
			{
				if (t.Kind == DTokens.Identifier || t.Kind == DTokens.Literal)
					return t.Value;
				return DTokens.GetTokenString(t.Kind);
			}
		}

		protected DToken Peek()
		{
			return Lexer.Peek();
		}

		protected DToken Peek(int n)
		{
			Lexer.StartPeek();
			DToken x = la;
			while (n > 0)
			{
				x = Lexer.Peek();
				n--;
			}
			return x;
		}

		protected void Step()
		{
			Lexer.NextToken();
		}

		#region Error handlers
		protected void SynErr(byte n, string msg)
		{
			if (ParseErrors.Count > DParserStateContext.MaxParseErrorsBeforeFailure)
			{
				return;
				throw new TooManyErrorsException();
			}
			else if (ParseErrors.Count == DParserStateContext.MaxParseErrorsBeforeFailure)
				msg = "Too many errors - stop parsing";

			ParseErrors.Add(new ParserError(false, msg, n, la.Location));
		}
		protected void SynErr(byte n)
		{
			SynErr(n, DTokens.GetTokenString(n) + " expected" + (t != null ? (", " + DTokens.GetTokenString(t.Kind) + " found") : ""));
		}

		protected void SemErr(byte n, string msg)
		{
			ParseErrors.Add(new ParserError(true, msg, n, la.Location));
		}


		/*protected void SemErr(int n)
{
   ParseErrors.Add(new ParserError(true, DTokens.GetTokenString(n) + " expected" + (t != null ? (", " + DTokens.GetTokenString(t.Kind) + " found") : ""), n, t == null ? la.Location : t.EndLocation));
}*/
		#endregion

		#region Comments
		protected StringBuilder PreviousComment
		{
			get { return stateContext.PreviousComment; }
			set { stateContext.PreviousComment = value; }
		}

		protected string GetComments()
		{
			var sb = new StringBuilder();

			foreach (var c in Lexer.Comments)
			{
				if (c.CommentType.HasFlag(Comment.Type.Documentation))
					sb.AppendLine(c.CommentText);
			}

			Comments.AddRange(Lexer.Comments);
			Lexer.Comments.Clear();

			sb.Trim();

			if (sb.Length == 0)
				return string.Empty;

			// Overwrite only if comment is not 'ditto'
			if (sb.Length != 5 || sb.ToString().ToLowerInvariant() != "ditto")
				PreviousComment = sb;

			return PreviousComment.ToString();
		}

		/// <summary>
		/// Returns the pre- and post-declaration comment
		/// </summary>
		/// <returns></returns>
		protected string CheckForPostSemicolonComment()
		{
			if (t == null)
				return string.Empty;

			int ExpectedLine = t.Line;

			var ret = new StringBuilder();

			int i = 0;
			foreach (var c in Lexer.Comments)
			{
				if (c.CommentType.HasFlag(Comment.Type.Documentation))
				{
					// Ignore ddoc comments made e.g. in int a /** ignored comment */, b,c; 
					// , whereas this method is called as t is the final semicolon
					if (c.EndPosition <= t.Location)
					{
						i++;
						Comments.Add(c);
						continue;
					}
					else if (c.StartPosition.Line > ExpectedLine)
						break;

					ret.AppendLine(c.CommentText);
				}

				i++;
				Comments.Add(c);
			}
			Lexer.Comments.RemoveRange(0, i);

			if (ret.Length == 0)
				return string.Empty;

			ret.Trim();

			// Add post-declaration string if comment text is 'ditto'
			if (ret.Length == 5 && ret.ToString().ToLowerInvariant() == "ditto")
				return PreviousComment.ToString();

			// Append post-semicolon comment string to previously read comments
			if (PreviousComment.Length != 0) // If no previous comment given, do not insert a new-line
				return (PreviousComment = ret).ToString();

			ret.Insert(0, Environment.NewLine);

			PreviousComment.Append(ret.ToString());
			return ret.ToString();
		}

		#endregion
	}
}
