using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser.Implementations;

namespace D_Parser.Parser
{
	/// <summary>
	/// Parser for D Code
	/// </summary>
	public class DParser : IDisposable
	{
		readonly DParserStateContext stateContext;
		internal readonly DParserParts parserParts;

		public DParser(Lexer lexer)
		{
			stateContext = new DParserStateContext(lexer);
			parserParts = new DParserParts(stateContext);
		}

		#region External interface
		public static BlockStatement ParseBlockStatement(string Code, INode ParentNode = null)
		{
			return ParseBlockStatement(Code, CodeLocation.Empty, ParentNode);
		}

		public static BlockStatement ParseBlockStatement(string Code, CodeLocation initialLocation, INode ParentNode = null)
		{
			var p = Create(new StringReader(Code));
			p.stateContext.Lexer.SetInitialLocation(initialLocation);
			p.stateContext.Lexer.NextToken();

			return p.parserParts.statementParser.BlockStatement(ParentNode);
		}

		public static IExpression ParseExpression(string Code)
		{
			var p = Create(new StringReader(Code));
			p.stateContext.Lexer.NextToken();
			return p.parserParts.expressionsParser.Expression();
		}

		public static DBlockNode ParseDeclDefs(string Code)
		{
			var p = Create(new StringReader(Code));
			p.stateContext.Lexer.NextToken();
			var block = new DBlockNode();
			while (!p.stateContext.Lexer.IsEOF)
			{
				p.parserParts.modulesParser.DeclDef(block);
			}

			block.EndLocation = p.stateContext.Lexer.LookAhead.Location;
			return block;
		}

		public static IExpression ParseAssignExpression(string Code)
		{
			var p = Create(new StringReader(Code));
			p.stateContext.Lexer.NextToken();
			return p.parserParts.expressionsParser.AssignExpression();
		}

		public static ITypeDeclaration ParseBasicType(string code)
		{
			DToken tk;
			return ParseBasicType(code, out tk);
		}

		public static ITypeDeclaration ParseBasicType(string Code, out DToken OptionalToken)
		{
			OptionalToken = null;

			var p = Create(new StringReader(Code));
			var lexer = p.stateContext.Lexer;
			lexer.NextToken();
			// Exception: If we haven't got any basic types as our first token, return this token via OptionalToken
			if (!p.parserParts.declarationParser.IsBasicType()
				|| lexer.LookAhead.Kind == DTokens.__LINE__
				|| lexer.LookAhead.Kind == DTokens.__FILE__)
			{
				lexer.NextToken();
				lexer.StartPeek();
				lexer.Peek();
				OptionalToken = p.stateContext.t;

				// Only if a dot follows a 'this' or 'super' token we go on parsing; Return otherwise
				if (!((OptionalToken.Kind == DTokens.This || OptionalToken.Kind == DTokens.Super)
					&& lexer.LookAhead.Kind == DTokens.Dot))
					return null;
			}

			var scope = new DModule();
			var bt = p.parserParts.declarationParser.BasicType(scope);
			p.parserParts.declarationParser.ParseBasicType2(ref bt, scope);
			return bt;
		}

		public static DModule ParseString(string ModuleCode, bool SkipFunctionBodies = false,
										  bool KeepComments = true, string[] taskTokens = null)
		{
			using (var sr = new StringReader(ModuleCode))
			using (var p = Create(sr))
				return p.Parse(SkipFunctionBodies, KeepComments, taskTokens);
		}

		public static DModule ParseFile(string File, bool SkipFunctionBodies = false, bool KeepComments = true)
		{
			using (var s = new StreamReader(File))
			{
				var p = Create(s);
				var m = p.Parse(SkipFunctionBodies, KeepComments);
				m.FileName = File;
				if (string.IsNullOrEmpty(m.ModuleName))
					m.ModuleName = Path.GetFileNameWithoutExtension(File);
				s.Close();
				return m;
			}
		}

		public static DMethod ParseMethodDeclarationHeader(string headerCode, out ITypeDeclaration identifierChain)
		{
			using (var sr = new StringReader(headerCode))
			{
				var p = Create(sr);
				p.stateContext.Lexer.NextToken();

				var n = new DMethod();
				var scope = new DModule();
				p.parserParts.declarationParser.CheckForStorageClasses(scope);
				p.parserParts.declarationParser.ApplyAttributes(n);
				p.parserParts.attributesParser.FunctionAttributes(n);

				n.Type = p.parserParts.declarationParser.Type(scope);

				identifierChain = p.parserParts.declarationParser.IdentifierList();
				if (identifierChain is IdentifierDeclaration)
					n.NameHash = (identifierChain as IdentifierDeclaration).IdHash;

				p.parserParts.declarationParser.Parameters(n);

				return n;
			}
		}

		public static DParser Create(TextReader tr)
		{
			return new DParser(new Lexer(tr));
		}
		#endregion

		/// <summary>
		/// Initializes and proceed parse procedure
		/// </summary>
		/// <param name="parseStructureOnly">If true, all statements and non-declarations are ignored. Useful for analysing libraries.</param>
		/// <param name="keepComments">If true, all (ddoc + non-ddoc) comment regions will be added to the returned module instance's properties.<br/>
		/// This property is disabled by default, as non-ddoc-comments are mostly needed for meta operations on the code (e.g. formatting).</param>
		/// <param name="taskTokens">Allows providing custom in-code task prefixes (e.g. ´@TODO´) that shall be provided in the <pre>Tasks</pre>-property.</param>
		/// <returns>Completely parsed module structure</returns>
		public DModule Parse(bool parseStructureOnly = false, bool keepComments = true, IEnumerable<string> taskTokens = null)
		{
			if(taskTokens != null)
				stateContext.Lexer.TaskTokens = new HashSet<string>(taskTokens);

			stateContext.ParseStructureOnly = parseStructureOnly;
			if (keepComments)
				stateContext.Lexer.OnlyEnlistDDocComments = false;

			var doc = parserParts.modulesParser.Root();
			doc.ParseErrors = new ReadOnlyCollection<ParserError>(stateContext.ParseErrors);
			doc.Tasks = new ReadOnlyCollection<ParserError>(stateContext.Lexer.Tasks);
			if (keepComments)
				doc.Comments = stateContext.Comments.ToArray();

			return doc;
		}

		public void Dispose()
		{
			stateContext.Dispose();
		}
	}

	public class TooManyErrorsException : Exception
	{
		public TooManyErrorsException() : base("Too many errors") { }
	}
}
