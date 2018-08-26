using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using D_Parser.Dom;

namespace D_Parser.Parser.Implementations
{
	internal class DParserStateContext : IDisposable
	{
		/// <summary>
		/// Modifiers for entire block
		/// </summary>
		public Stack<DAttribute> BlockAttributes = new Stack<DAttribute>();
		/// <summary>
		/// Modifiers for current expression only
		/// </summary>
		public Stack<DAttribute> DeclarationAttributes = new Stack<DAttribute>();

		public bool ParseStructureOnly = false;
		public Lexer Lexer;

		public readonly List<Comment> Comments = new List<Comment>();
		public StringBuilder PreviousComment = new StringBuilder();

		DToken _backupt = new DToken();
		public DToken t
		{
			[System.Diagnostics.DebuggerStepThrough]
			get
			{
				return Lexer.CurrentToken ?? _backupt;
			}
		}

		/// <summary>
		/// Used if the parser is unsure if there's a type or an expression - then, instead of throwing exceptions, the Type()-Methods will simply return null;
		/// </summary>
		public bool AllowWeakTypeParsing = false;

		public List<ParserError> ParseErrors = new List<ParserError>();
		public const int MaxParseErrorsBeforeFailure = 100;

		public DParserStateContext(Lexer lexer)
		{
			this.Lexer = lexer;
			Lexer.LexerErrors = ParseErrors;
		}

		public void Dispose()
		{
			BlockAttributes.Clear();
			BlockAttributes = null;
			DeclarationAttributes.Clear();
			DeclarationAttributes = null;
			Lexer.Dispose();
			Lexer = null;
			ParseErrors = null;
		}
	}
}
