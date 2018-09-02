using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;

namespace D_Parser.Parser.Implementations
{
	class DStatementParser : DParserImplementationPart
	{
		readonly DParserParts parserParts;
		readonly DAsmStatementParser asmStatementParser;

		public DStatementParser(DParserStateContext stateContext, DParserParts parserParts)
			: base(stateContext) {
			this.parserParts = parserParts;
			asmStatementParser = new DAsmStatementParser(stateContext, parserParts);
		}

		#region Statements
		void IfCondition(IfStatement par, IBlockNode scope)
		{
			var wkType = AllowWeakTypeParsing;
			AllowWeakTypeParsing = true;

			Lexer.PushLookAheadBackup();

			ITypeDeclaration tp;
			if (laKind == DTokens.Auto)
			{
				Step();
				tp = new DTokenDeclaration(DTokens.Auto) { Location = t.Location, EndLocation = t.EndLocation };
			}
			else
				tp = parserParts.declarationParser.Type(scope);

			AllowWeakTypeParsing = wkType;

			if (tp != null && ((laKind == DTokens.Identifier &&
				(Peek(1).Kind == DTokens.Assign || Lexer.CurrentPeekToken.Kind == DTokens.CloseParenthesis)) || // if(a * b * c) is an expression, if(a * b = 123) may be a pointer variable
				(IsEOF && tp.InnerDeclaration == null))) // if(inst. is an expression, TODO if(int. not
			{
				Lexer.PopLookAheadBackup();
				var dv = parserParts.declarationParser.Declarator(tp, false, par.ParentNode) as DVariable;
				if (dv == null)
				{
					SynErr(t.Kind, "Invalid node type! - Variable expected!");
					return;
				}

				if (laKind == DTokens.Assign)
				{
					Step();
					dv.Location = tp.Location;
					dv.Initializer = parserParts.expressionsParser.Expression(scope);
					dv.EndLocation = t.EndLocation;
				}

				par.IfVariable = dv;
				return;
			}

			Lexer.RestoreLookAheadBackup();
			par.IfCondition = parserParts.expressionsParser.Expression(scope);
		}

		public bool IsStatement
		{
			get
			{
				switch (laKind)
				{
					case DTokens.OpenCurlyBrace:
					case DTokens.If:
					case DTokens.While:
					case DTokens.Do:
					case DTokens.For:
					case DTokens.Foreach:
					case DTokens.Foreach_Reverse:
					case DTokens.Switch:
					case DTokens.Case:
					case DTokens.Default:
					case DTokens.Continue:
					case DTokens.Break:
					case DTokens.Return:
					case DTokens.Goto:
					case DTokens.With:
					case DTokens.Synchronized:
					case DTokens.Try:
					case DTokens.Throw:
					case DTokens.Scope:
					case DTokens.Asm:
					case DTokens.Pragma:
					case DTokens.Mixin:
					case DTokens.Version:
					case DTokens.Debug:
					case DTokens.Assert:
					case DTokens.Volatile:
						return true;
					case DTokens.Static:
						return Lexer.CurrentPeekToken.Kind == DTokens.If || Lexer.CurrentPeekToken.Kind == DTokens.Assert;
					case DTokens.Final:
						return Lexer.CurrentPeekToken.Kind == DTokens.Switch;
					case DTokens.Identifier:
						return Peek(1).Kind == DTokens.Colon;
					default:
						return false;
				}
			}
		}

		public IStatement Statement(bool BlocksAllowed = true, bool EmptyAllowed = true, IBlockNode Scope = null, IStatement Parent = null)
		{
			switch (laKind)
			{
				case DTokens.Semicolon:
					if (!EmptyAllowed)
						goto default;
					Step();
					return null;
				case DTokens.OpenCurlyBrace:
					if (!BlocksAllowed)
						goto default;
					return BlockStatement(Scope, Parent);
				// LabeledStatement (loc:... goto loc;)
				case DTokens.Identifier:
					if (Lexer.CurrentPeekToken.Kind != DTokens.Colon)
						goto default;
					Step();

					var ls = new LabeledStatement() { Location = t.Location, Identifier = t.Value, Parent = Parent };
					Step();
					ls.EndLocation = t.EndLocation;

					return ls;
				// IfStatement
				case DTokens.If:
					Step();

					var iS = new IfStatement { Location = t.Location, Parent = Parent };

					Expect(DTokens.OpenParenthesis);
					// IfCondition
					IfCondition(iS, Scope);

					// ThenStatement
					if (Expect(DTokens.CloseParenthesis))
						iS.ThenStatement = Statement(Scope: Scope, Parent: iS);

					// ElseStatement
					if (laKind == (DTokens.Else))
					{
						Step();
						iS.ElseStatement = Statement(Scope: Scope, Parent: iS);
					}

					if (t != null)
						iS.EndLocation = t.EndLocation;

					return iS;
				// Conditions
				case DTokens.Version:
				case DTokens.Debug:
					return StmtCondition(Parent, Scope);
				case DTokens.Static:
					if (Lexer.CurrentPeekToken.Kind == DTokens.If)
						return StmtCondition(Parent, Scope);
					else if (Lexer.CurrentPeekToken.Kind == DTokens.Assert)
						goto case DTokens.Assert;
					else if (Lexer.CurrentPeekToken.Kind == DTokens.Foreach || Lexer.CurrentPeekToken.Kind == DTokens.Foreach_Reverse)
					{
						Step();
						return ForeachStatement(Scope, Parent, true);
					}
					else if (Lexer.CurrentPeekToken.Kind == DTokens.Import)
						goto case DTokens.Import;
					goto default;
				case DTokens.For:
					return ForStatement(Scope, Parent);
				case DTokens.Foreach:
				case DTokens.Foreach_Reverse:
					return ForeachStatement(Scope, Parent, false);
				case DTokens.While:
					Step();

					var ws = new WhileStatement() { Location = t.Location, Parent = Parent };

					Expect(DTokens.OpenParenthesis);
					ws.Condition = parserParts.expressionsParser.Expression(Scope);
					Expect(DTokens.CloseParenthesis);

					if (!IsEOF)
					{
						ws.ScopedStatement = Statement(Scope: Scope, Parent: ws);
						ws.EndLocation = t.EndLocation;
					}

					return ws;
				case DTokens.Do:
					Step();

					var dws = new WhileStatement() { Location = t.Location, Parent = Parent };
					if (!IsEOF)
						dws.ScopedStatement = Statement(true, false, Scope, dws);

					if (Expect(DTokens.While) && Expect(DTokens.OpenParenthesis))
					{
						dws.Condition = parserParts.expressionsParser.Expression(Scope);
						Expect(DTokens.CloseParenthesis);
						Expect(DTokens.Semicolon);

						dws.EndLocation = t.EndLocation;
					}

					return dws;
				// [Final] SwitchStatement
				case DTokens.Final:
					if (Lexer.CurrentPeekToken.Kind != DTokens.Switch)
						goto default;
					goto case DTokens.Switch;
				case DTokens.Switch:
					var ss = new SwitchStatement { Location = la.Location, Parent = Parent };
					if (laKind == (DTokens.Final))
					{
						ss.IsFinal = true;
						Step();
					}
					Step();
					Expect(DTokens.OpenParenthesis);
					ss.SwitchExpression = parserParts.expressionsParser.Expression(Scope);
					Expect(DTokens.CloseParenthesis);

					if (!IsEOF)
						ss.ScopedStatement = Statement(Scope: Scope, Parent: ss);
					ss.EndLocation = t.EndLocation;

					return ss;
				case DTokens.Case:
					Step();

					var sscs = new SwitchStatement.CaseStatement() { Location = la.Location, Parent = Parent };
					sscs.ArgumentList = parserParts.expressionsParser.Expression(Scope);

					Expect(DTokens.Colon);

					// CaseRangeStatement
					if (laKind == DTokens.DoubleDot)
					{
						Step();
						Expect(DTokens.Case);
						sscs.LastExpression = parserParts.expressionsParser.AssignExpression();
						Expect(DTokens.Colon);
					}

					var sscssl = new List<IStatement>();

					while (laKind != DTokens.Case && laKind != DTokens.Default && laKind != DTokens.CloseCurlyBrace && !IsEOF)
					{
						var stmt = Statement(Scope: Scope, Parent: sscs);

						if (stmt != null)
						{
							stmt.Parent = sscs;
							sscssl.Add(stmt);
						}
					}

					sscs.ScopeStatementList = sscssl.ToArray();
					sscs.EndLocation = t.EndLocation;

					return sscs;
				case DTokens.Default:
					Step();

					var ssds = new SwitchStatement.DefaultStatement()
					{
						Location = la.Location,
						Parent = Parent
					};

					Expect(DTokens.Colon);

					var ssdssl = new List<IStatement>();

					while (laKind != DTokens.Case && laKind != DTokens.Default && laKind != DTokens.CloseCurlyBrace && !IsEOF)
					{
						var stmt = Statement(Scope: Scope, Parent: ssds);

						if (stmt != null)
						{
							stmt.Parent = ssds;
							ssdssl.Add(stmt);
						}
					}

					ssds.ScopeStatementList = ssdssl.ToArray();
					ssds.EndLocation = t.EndLocation;

					return ssds;
				case DTokens.Continue:
					Step();
					var cs = new ContinueStatement() { Location = t.Location, Parent = Parent };
					if (laKind == (DTokens.Identifier))
					{
						Step();
						cs.Identifier = t.Value;
					}
					else if (IsEOF)
						cs.IdentifierHash = DTokens.IncompleteIdHash;

					Expect(DTokens.Semicolon);
					cs.EndLocation = t.EndLocation;

					return cs;
				case DTokens.Break:
					Step();
					var bs = new BreakStatement() { Location = t.Location, Parent = Parent };

					if (laKind == (DTokens.Identifier))
					{
						Step();
						bs.Identifier = t.Value;
					}
					else if (IsEOF)
						bs.IdentifierHash = DTokens.IncompleteIdHash;

					Expect(DTokens.Semicolon);

					bs.EndLocation = t.EndLocation;

					return bs;
				case DTokens.Return:
					Step();
					var rs = new ReturnStatement() { Location = t.Location, Parent = Parent };

					if (laKind != (DTokens.Semicolon))
						rs.ReturnExpression = parserParts.expressionsParser.Expression(Scope);

					Expect(DTokens.Semicolon);
					rs.EndLocation = t.EndLocation;

					return rs;
				case DTokens.Goto:
					Step();
					var gs = new GotoStatement() { Location = t.Location, Parent = Parent };

					switch (laKind)
					{
						case DTokens.Identifier:
							Step();
							gs.StmtType = GotoStatement.GotoStmtType.Identifier;
							gs.LabelIdentifier = t.Value;
							break;
						case DTokens.Default:
							Step();
							gs.StmtType = GotoStatement.GotoStmtType.Default;
							break;
						case DTokens.Case:
							Step();
							gs.StmtType = GotoStatement.GotoStmtType.Case;

							if (laKind != (DTokens.Semicolon))
								gs.CaseExpression = parserParts.expressionsParser.Expression(Scope);
							break;
						default:
							if (IsEOF)
								gs.LabelIdentifierHash = DTokens.IncompleteIdHash;
							break;
					}
					Expect(DTokens.Semicolon);
					gs.EndLocation = t.EndLocation;

					return gs;
				case DTokens.With:
					Step();

					var wS = new WithStatement() { Location = t.Location, Parent = Parent };

					if (Expect(DTokens.OpenParenthesis))
					{
						// Symbol
						wS.WithExpression = parserParts.expressionsParser.Expression(Scope);

						Expect(DTokens.CloseParenthesis);

						if (!IsEOF)
							wS.ScopedStatement = Statement(Scope: Scope, Parent: wS);
					}
					wS.EndLocation = t.EndLocation;
					return wS;
				case DTokens.Synchronized:
					Step();
					var syncS = new SynchronizedStatement() { Location = t.Location, Parent = Parent };

					if (laKind == (DTokens.OpenParenthesis))
					{
						Step();
						syncS.SyncExpression = parserParts.expressionsParser.Expression(Scope);
						Expect(DTokens.CloseParenthesis);
					}

					if (!IsEOF)
						syncS.ScopedStatement = Statement(Scope: Scope, Parent: syncS);
					syncS.EndLocation = t.EndLocation;

					return syncS;
				case DTokens.Try:
					Step();

					var ts = new TryStatement() { Location = t.Location, Parent = Parent };

					ts.ScopedStatement = Statement(Scope: Scope, Parent: ts);

					if (!(laKind == (DTokens.Catch) || laKind == (DTokens.Finally)))
						SemErr(DTokens.Catch, "At least one catch or a finally block expected!");

					var catches = new List<TryStatement.CatchStatement>();
					// Catches
					while (laKind == (DTokens.Catch))
					{
						Step();

						var c = new TryStatement.CatchStatement() { Location = t.Location, Parent = ts };

						// CatchParameter
						if (laKind == (DTokens.OpenParenthesis))
						{
							Step();

							if (laKind == DTokens.CloseParenthesis || IsEOF)
							{
								SemErr(DTokens.CloseParenthesis, "Catch parameter expected, not ')'");
								Step();
							}
							else
							{
								var catchVar = new DVariable { Parent = Scope, Location = t.Location };

								Lexer.PushLookAheadBackup();
								catchVar.Type = parserParts.declarationParser.BasicType(Scope);
								if (laKind == DTokens.CloseParenthesis)
								{
									Lexer.RestoreLookAheadBackup();
									catchVar.Type = new IdentifierDeclaration("Exception") { InnerDeclaration = new IdentifierDeclaration("object") };
								}
								else
									Lexer.PopLookAheadBackup();

								if (Expect(DTokens.Identifier))
								{
									catchVar.Name = t.Value;
									catchVar.NameLocation = t.Location;
									Expect(DTokens.CloseParenthesis);
								}
								else if (IsEOF)
									catchVar.NameHash = DTokens.IncompleteIdHash;

								catchVar.EndLocation = t.EndLocation;
								c.CatchParameter = catchVar;
							}
						}

						if (!IsEOF)
							c.ScopedStatement = Statement(Scope: Scope, Parent: c);
						c.EndLocation = t.EndLocation;

						catches.Add(c);
					}

					if (catches.Count > 0)
						ts.Catches = catches.ToArray();

					if (laKind == (DTokens.Finally))
					{
						Step();

						var f = new TryStatement.FinallyStatement() { Location = t.Location, Parent = Parent };

						f.ScopedStatement = Statement();
						f.EndLocation = t.EndLocation;

						ts.FinallyStmt = f;
					}

					ts.EndLocation = t.EndLocation;
					return ts;
				case DTokens.Throw:
					Step();
					var ths = new ThrowStatement() { Location = t.Location, Parent = Parent };

					ths.ThrowExpression = parserParts.expressionsParser.Expression(Scope);
					Expect(DTokens.Semicolon);
					ths.EndLocation = t.EndLocation;

					return ths;
				case DTokens.Scope:
					Step();

					if (laKind == DTokens.OpenParenthesis)
					{
						var s = new ScopeGuardStatement()
						{
							Location = t.Location,
							Parent = Parent
						};

						Step();

						if (Expect(DTokens.Identifier) && t.Value != null) // exit, failure, success
							s.GuardedScope = t.Value.ToLower();
						else if (IsEOF)
							s.GuardedScope = DTokens.IncompleteId;

						Expect(DTokens.CloseParenthesis);

						s.ScopedStatement = Statement(Scope: Scope, Parent: s);

						s.EndLocation = t.EndLocation;
						return s;
					}
					else
						parserParts.declarationParser.PushAttribute(new Modifier(DTokens.Scope), false);
					goto default;
				case DTokens.Asm:
					return asmStatementParser.ParseAsmStatement(Scope, Parent);
				case DTokens.Pragma:
					var ps = new PragmaStatement { Location = la.Location };

					ps.Pragma = parserParts.attributesParser._Pragma();
					ps.Parent = Parent;

					ps.ScopedStatement = Statement(Scope: Scope, Parent: ps);
					ps.EndLocation = t.EndLocation;
					return ps;
				case DTokens.Mixin:
					if (Peek(1).Kind == DTokens.OpenParenthesis)
					{
						OverPeekBrackets(DTokens.OpenParenthesis);
						if (Lexer.CurrentPeekToken.Kind != DTokens.Semicolon)
							return ExpressionStatement(Scope, Parent);
						return parserParts.modulesParser.MixinDeclaration(Scope, Parent);
					}
					else
					{
						var tmx = parserParts.templatesParser.TemplateMixin(Scope, Parent);
						if (tmx.MixinId == null)
							return tmx;
						else
							return new DeclarationStatement { Declarations = new[] { new NamedTemplateMixinNode(tmx) }, Parent = Parent };
					}
				case DTokens.Assert:
					parserParts.declarationParser.CheckForStorageClasses(Scope);
					if (Modifier.ContainsAnyAttributeToken(DeclarationAttributes, DTokens.Static))
					{
						Step();
						return parserParts.modulesParser.ParseStaticAssertStatement(Scope);
					}
					else
						return ExpressionStatement(Scope, Parent);
				case DTokens.Volatile:
					Step();
					var vs = new VolatileStatement() { Location = t.Location, Parent = Parent };

					vs.ScopedStatement = Statement(Scope: Scope, Parent: vs);
					vs.EndLocation = t.EndLocation;

					return vs;
				case DTokens.Import:
					if (laKind == DTokens.Static)
						Step(); // Will be handled in ImportDeclaration

					return parserParts.modulesParser.ImportDeclaration(Scope);
				case DTokens.Enum:
				case DTokens.Alias:
				case DTokens.Typedef:
					var ds = new DeclarationStatement() { Location = la.Location, Parent = Parent, ParentNode = Scope };
					ds.Declarations = parserParts.declarationParser.Declaration(Scope).ToArray();

					if (ds.Declarations != null &&
						ds.Declarations.Length == 1 &&
						!(ds.Declarations[0] is DVariable) &&
						!AllowWeakTypeParsing)
						Scope.Add(ds.Declarations[0]);

					ds.EndLocation = t.EndLocation;
					return ds;
				default:
					if (DTokensSemanticHelpers.IsClassLike(laKind)
						|| (DTokensSemanticHelpers.IsBasicType(laKind) && Lexer.CurrentPeekToken.Kind != DTokens.Dot)
						|| DTokensSemanticHelpers.IsModifier(laKind))
						goto case DTokens.Typedef;
					if (parserParts.expressionsParser.IsAssignExpression())
						return ExpressionStatement(Scope, Parent);
					goto case DTokens.Typedef;

			}
		}

		private IStatement ExpressionStatement(IBlockNode Scope, IStatement Parent)
		{
			var s = new ExpressionStatement() { Location = la.Location, Parent = Parent, ParentNode = Scope };

			// a==b, a=9; is possible -> Expressions can be there, not only single AssignExpressions!
			s.Expression = parserParts.expressionsParser.Expression(Scope);
			s.EndLocation = t.EndLocation;

			Expect(DTokens.Semicolon);
			if (s.Expression != null)
				return s;
			return null;
		}

		ForStatement ForStatement(IBlockNode Scope, IStatement Parent)
		{
			Step();

			var dbs = new ForStatement { Location = t.Location, Parent = Parent };

			if (!Expect(DTokens.OpenParenthesis))
				return dbs;

			// Initialize
			if (laKind == DTokens.Semicolon)
				Step();
			else
				dbs.Initialize = Statement(false, Scope: Scope, Parent: dbs); // Against the spec, blocks aren't allowed here!

			// Test
			if (laKind != DTokens.Semicolon)
				dbs.Test = parserParts.expressionsParser.Expression(Scope);

			if (Expect(DTokens.Semicolon))
			{
				// Increment
				if (laKind != (DTokens.CloseParenthesis))
					dbs.Increment = parserParts.expressionsParser.Expression(Scope);

				Expect(DTokens.CloseParenthesis);
				dbs.ScopedStatement = Statement(Scope: Scope, Parent: dbs);
			}
			dbs.EndLocation = t.EndLocation;

			return dbs;
		}

		public ForeachStatement ForeachStatement(IBlockNode Scope, IStatement Parent, bool isStatic)
		{
			Step();

			ForeachStatement dbs = isStatic ? new StaticForeachStatement() : new ForeachStatement();
			dbs.Location = t.Location;
			dbs.IsReverse = t.Kind == DTokens.Foreach_Reverse;
			dbs.Parent = Parent;

			if (!Expect(DTokens.OpenParenthesis))
				return dbs;

			var tl = new List<DVariable>();

			bool init = true;
			while (init || laKind == DTokens.Comma)
			{
				if (init)
					init = false;
				else
					Step();

				var forEachVar = new DVariable { Parent = Scope };
				forEachVar.Location = la.Location;

				if (isStatic && (laKind == DTokens.Alias || laKind == DTokens.Enum))
					Step();

				parserParts.declarationParser.CheckForStorageClasses(Scope);
				parserParts.declarationParser.ApplyAttributes(forEachVar);

				if (IsEOF)
				{
					SynErr(DTokens.Identifier, "Element variable name or type expected");
					forEachVar.NameHash = DTokens.IncompleteIdHash;
				}
				else if (laKind == (DTokens.Identifier) && (Lexer.CurrentPeekToken.Kind == (DTokens.Semicolon) || Lexer.CurrentPeekToken.Kind == DTokens.Comma))
				{
					Step();
					forEachVar.NameLocation = t.Location;
					forEachVar.Name = t.Value;
				}
				else
				{
					var type = parserParts.declarationParser.BasicType(Scope);

					var tnode = parserParts.declarationParser.Declarator(type, false, Scope);
					if (!(tnode is DVariable))
						break;
					if (forEachVar.Attributes != null)
						if (tnode.Attributes == null)
							tnode.Attributes = new List<DAttribute>(forEachVar.Attributes);
						else
							tnode.Attributes.AddRange(forEachVar.Attributes);
					tnode.Location = forEachVar.Location;
					forEachVar = tnode as DVariable;
				}
				forEachVar.EndLocation = t.EndLocation;

				tl.Add(forEachVar);
			}

			dbs.ForeachTypeList = tl.ToArray();

			if (Expect(DTokens.Semicolon))
				dbs.Aggregate = parserParts.expressionsParser.Expression(Scope);

			// ForeachRangeStatement
			if (laKind == DTokens.DoubleDot)
			{
				Step();
				dbs.UpperAggregate = parserParts.expressionsParser.Expression();
			}

			if (Expect(DTokens.CloseParenthesis))
				dbs.ScopedStatement = Statement(Scope: Scope, Parent: dbs);
			dbs.EndLocation = t.EndLocation;

			return dbs;
		}

		StatementCondition StmtCondition(IStatement Parent, IBlockNode Scope)
		{
			var sl = la.Location;

			var c = parserParts.modulesParser.Condition(Scope);
			c.Location = sl;
			c.EndLocation = t.EndLocation;
			var sc = new StatementCondition
			{
				Condition = c,
				Location = sl,
			};

			sc.ScopedStatement = Statement(true, false, Scope, sc);

			if (laKind == DTokens.Semicolon)
				Step();

			if (laKind == DTokens.Else)
			{
				Step();
				sc.ElseStatement = Statement(true, false, Scope, sc);
			}

			if (IsEOF)
				sc.EndLocation = la.Location;
			else
				sc.EndLocation = t.EndLocation;

			return sc;
		}

		public BlockStatement BlockStatement(INode ParentNode = null, IStatement Parent = null)
		{
			var OldPreviousCommentString = PreviousComment;
			PreviousComment = new StringBuilder();

			var bs = new BlockStatement() { Location = la.Location, ParentNode = ParentNode, Parent = Parent };

			if (Expect(DTokens.OpenCurlyBrace))
			{
				if (ParseStructureOnly && laKind != DTokens.CloseCurlyBrace)
					Lexer.SkipCurrentBlock();
				else
				{
					while (!IsEOF && laKind != (DTokens.CloseCurlyBrace))
					{
						var prevLocation = la.Location;
						var s = Statement(Scope: ParentNode as IBlockNode, Parent: bs);

						// Avoid infinite loops -- hacky?
						if (prevLocation == la.Location)
						{
							Step();
							break;
						}

						if (s != null)
							bs.Add(s);
					}
				}

				if (!Expect(DTokens.CloseCurlyBrace) && IsEOF)
				{
					bs.EndLocation = la.Location;
					return bs;
				}
			}
			if (t != null)
				bs.EndLocation = t.EndLocation;

			PreviousComment = OldPreviousCommentString;
			return bs;
		}
		#endregion
	}
}
