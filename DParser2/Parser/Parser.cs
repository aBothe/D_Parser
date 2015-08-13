using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using System;
namespace D_Parser.Parser
{
    /// <summary>
    /// Parser for D Code
    /// </summary>
    public partial class DParser:DTokens, IDisposable
	{
		#region Properties
		/// <summary>
		/// Holds document structure
		/// </summary>
		DModule doc;

		public DModule Document
		{
			get { return doc; }
		}

		/// <summary>
		/// Modifiers for entire block
		/// </summary>
		Stack<DAttribute> BlockAttributes = new Stack<DAttribute>();
		/// <summary>
		/// Modifiers for current expression only
		/// </summary>
		Stack<DAttribute> DeclarationAttributes = new Stack<DAttribute>();

		bool ParseStructureOnly = false;
		public Lexer Lexer;

		public readonly List<Comment> Comments = new List<Comment>();

		DToken _backupt = new DToken();
		DToken t
		{
			[System.Diagnostics.DebuggerStepThrough]
			get
			{
				return Lexer.CurrentToken ?? _backupt;
			}
		}

		/// <summary>
		/// lookAhead token
		/// </summary>
		public DToken la
		{
			get
			{
				return Lexer.LookAhead;
			}
		}
		
		public byte laKind {get{return Lexer.laKind;}}

		public bool IsEOF
		{
			get { return Lexer.IsEOF; }
		}

		public List<ParserError> ParseErrors = new List<ParserError>();
		public const int MaxParseErrorsBeforeFailure = 100;
		#endregion

		public void Dispose()
		{
			doc = null;
			BlockAttributes.Clear();
			BlockAttributes = null;
			DeclarationAttributes.Clear();
			DeclarationAttributes = null;
			Lexer.Dispose();
			Lexer = null;
			ParseErrors = null;
		}

		public DParser(Lexer lexer)
		{
			this.Lexer = lexer;
			Lexer.LexerErrors = ParseErrors;
		}

		#region External interface
		public static BlockStatement ParseBlockStatement(string Code, INode ParentNode = null)
		{
			return ParseBlockStatement(Code, CodeLocation.Empty, ParentNode);
		}

		public static BlockStatement ParseBlockStatement(string Code, CodeLocation initialLocation, INode ParentNode = null)
		{
			var p = Create(new StringReader(Code));
			p.Lexer.SetInitialLocation(initialLocation);
			p.Step();

			return p.BlockStatement(ParentNode);
		}

        public static IExpression ParseExpression(string Code)
        {
            var p = Create(new StringReader(Code));
            p.Step();
            return p.Expression();
        }

		public static DBlockNode ParseDeclDefs(string Code)
		{
			var p = Create(new StringReader(Code));
			p.Step();
			var block = new DBlockNode();
			while (!p.IsEOF)
			{
				p.DeclDef(block);
			}

			block.EndLocation = p.la.Location;
			return block;
		}

		public static IExpression ParseAssignExpression(string Code)
		{
			var p = Create(new StringReader(Code));
			p.Step();
			return p.AssignExpression();
		}

		public static ITypeDeclaration ParseBasicType(string code)
		{
			DToken tk;
			return ParseBasicType (code, out tk);
		}

        public static ITypeDeclaration ParseBasicType(string Code,out DToken OptionalToken)
        {
            OptionalToken = null;

            var p = Create(new StringReader(Code));
            p.Step();
            // Exception: If we haven't got any basic types as our first token, return this token via OptionalToken
            if (!p.IsBasicType() || p.laKind == __LINE__ || p.laKind == __FILE__)
            {
                p.Step();
                p.Peek(1);
                OptionalToken = p.t;

                // Only if a dot follows a 'this' or 'super' token we go on parsing; Return otherwise
                if (!((p.t.Kind == This || p.t.Kind == Super) && p.laKind == Dot))
                    return null;
            }
            
			var bt= p.BasicType(null);
			p.ParseBasicType2 (ref bt, p.doc);
            return bt;
        }

        public static DModule ParseString(string ModuleCode,bool SkipFunctionBodies=false, bool KeepComments = true)
        {
            using(var sr = new StringReader(ModuleCode))
        	{
            	using(var p = Create(sr))
            		return p.Parse(SkipFunctionBodies, KeepComments);
        	}
        }

        public static DModule ParseFile(string File, bool SkipFunctionBodies=false, bool KeepComments = true)
        {
        	using(var s = new StreamReader(File)){
	            var p=Create(s);
	            var m = p.Parse(SkipFunctionBodies, KeepComments);
	            m.FileName = File;
	            if(string.IsNullOrEmpty(m.ModuleName))
					m.ModuleName = Path.GetFileNameWithoutExtension(File);
				s.Close();
				return m;
        	}
        }
        
        public static DMethod ParseMethodDeclarationHeader(string headerCode, out ITypeDeclaration identifierChain)
        {
        	using(var sr = new StringReader(headerCode))
        	{
	            var p = Create(sr);
	            p.Step();
	            
	            var n = new DMethod();
	            p.CheckForStorageClasses(p.doc);
	            p.ApplyAttributes(n);
	            p.FunctionAttributes(n);
	            
	            n.Type = p.Type(null);
	            
	            identifierChain = p.IdentifierList();
	            if(identifierChain is IdentifierDeclaration)
					n.NameHash = (identifierChain as IdentifierDeclaration).IdHash;
	            
	            p.Parameters(n);
	            
	            return n;
        	}
        }

        public static DParser Create(TextReader tr)
        {
			return new DParser(new Lexer(tr));
        }
		#endregion

		void PushAttribute(DAttribute attr, bool BlockAttributes)
		{
			var stk=BlockAttributes?this.BlockAttributes:this.DeclarationAttributes;

			var m = attr as Modifier;
			if(m!=null)
			// If attr would change the accessability of an item, remove all previously found (so the most near attribute that's next to the item is significant)
			if (DTokens.IsVisibilityModifier(m.Token))
				Modifier.CleanupAccessorAttributes(stk, m.Token);
			else
				Modifier.RemoveFromStack(stk, m.Token);

			stk.Push(attr);
		}

        void ApplyAttributes(DNode n)
        {
        	n.Attributes = GetCurrentAttributeSet();
        }
        
        DAttribute[] GetCurrentAttributeSet_Array()
        {
        	var attrs = GetCurrentAttributeSet();
        	return attrs.Count == 0 ? null : attrs.ToArray();
        }
		
        List<DAttribute> GetCurrentAttributeSet()
        {
			var vis = D_Parser.Dom.Visitors.AstElementHashingVisitor.Instance;
			var keys = new List<long>();
        	var attrs = new List<DAttribute>();
			Modifier lastVisModifier = null;

			long key;
			int i;

			foreach (var a in BlockAttributes){
				// ISSUE: Theoretically, when having two identically written but semantically different UDA attributes, the first one will become overridden.
				key = a.Accept(vis);
				if ((i = keys.IndexOf(key)) > -1)
					attrs[i] = a;
				else
				{
					keys.Add(key);
					attrs.Insert(0,a);
				}
			}

			foreach (var a in DeclarationAttributes)
			{
				key = a.Accept(vis);
				if ((i = keys.IndexOf(key)) > -1)
					attrs[i] = a;
				else
				{
					keys.Add(key);
					attrs.Insert(0,a);
				}
			}
			DeclarationAttributes.Clear();

			for (i = attrs.Count - 1; i >= 0; i--)
			{
				var m = attrs[i] as Modifier;
				if (m != null)
				{
					// If accessor already in attribute array, remove it
					if (DTokens.IsVisibilityModifier(m.Token))
					{
						lastVisModifier = m;
						// Temporarily remove all vis modifiers and add the last one again
						attrs.RemoveAt(i);
						//keys.RemoveAt(i); -- No need to touch keys anymore
						continue;
					}
				}
			}

			if (lastVisModifier != null)
				attrs.Insert(0, lastVisModifier);

            return attrs;
        }

		bool OverPeekBrackets(byte OpenBracketKind,bool LAIsOpenBracket = false)
        {
            int CloseBracket = CloseParenthesis;

            if (OpenBracketKind == OpenSquareBracket) 
				CloseBracket = CloseSquareBracket;
            else if (OpenBracketKind == OpenCurlyBrace) 
				CloseBracket = CloseCurlyBrace;

			var pk = Lexer.CurrentPeekToken;
            int i = LAIsOpenBracket?1:0;
            while (pk.Kind != EOF)
            {
                if (pk.Kind== OpenBracketKind)
                    i++;
                else if (pk.Kind== CloseBracket)
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

        private bool Expect(byte n)
        {
			if (laKind == n)
			{
				Step();
				return true; 
			}
			else
			{
				SynErr(n, DTokens.GetTokenString(n) + " expected, "+DTokens.GetTokenString(laKind)+" found!");
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

        DToken Peek()
        {
            return Lexer.Peek();
        }

        DToken Peek(int n)
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

		public void Step()
		{ 
			Lexer.NextToken();
		}

        [DebuggerStepThrough]
        public DModule Parse()
        {
            return Parse(false);
        }

        /// <summary>
        /// Initializes and proceed parse procedure
        /// </summary>
        /// <param name="ParseStructureOnly">If true, all statements and non-declarations are ignored - useful for analysing libraries</param>
        /// <returns>Completely parsed module structure</returns>
        public DModule Parse(bool ParseStructureOnly, bool KeepComments = true)
        {
            this.ParseStructureOnly = ParseStructureOnly;
            if(KeepComments)
            	Lexer.OnlyEnlistDDocComments = false;
            doc=Root();
			doc.ParseErrors = new System.Collections.ObjectModel.ReadOnlyCollection<ParserError>(ParseErrors);
			if(KeepComments){
				doc.Comments = Comments.ToArray();
			}
			
            return doc;
        }
        
        #region Error handlers
        void SynErr(byte n, string msg)
        {
			if (ParseErrors.Count > MaxParseErrorsBeforeFailure)
			{
				return;
				throw new TooManyErrorsException();
			}
			else if (ParseErrors.Count == MaxParseErrorsBeforeFailure)
				msg = "Too many errors - stop parsing";

			ParseErrors.Add(new ParserError(false,msg,n,la.Location));
        }
        void SynErr(byte n)
		{
			SynErr(n, DTokens.GetTokenString(n) + " expected" + (t!=null?(", "+DTokens.GetTokenString(t.Kind)+" found"):""));
        }

        void SemErr(byte n, string msg)
        {
			ParseErrors.Add(new ParserError(true, msg, n, la.Location));
        }
        /*void SemErr(int n)
        {
			ParseErrors.Add(new ParserError(true, DTokens.GetTokenString(n) + " expected" + (t != null ? (", " + DTokens.GetTokenString(t.Kind) + " found") : ""), n, t == null ? la.Location : t.EndLocation));
        }*/
        #endregion
	}

	public class TooManyErrorsException : Exception
	{
		public TooManyErrorsException() : base("Too many errors") { }
	}
}
