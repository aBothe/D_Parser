using System.Collections.Generic;

using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;

namespace D_Parser.Parser.Implementations
{
	class DAsmStatementParser : DParserImplementationPart
	{
		readonly DParserParts parserParts;

		public DAsmStatementParser(DParserStateContext stateContext, DParserParts parserParts)
			: base(stateContext)
		{
			this.parserParts = parserParts;
		}

		public AsmStatement ParseAsmStatement(IBlockNode Scope, IStatement Parent)
		{
			Step();
			AsmAlignStatement als;
			var s = new AsmStatement() { Location = t.Location, Parent = Parent };

			parserParts.declarationParser.CheckForStorageClasses(Scope); // allowed since dmd 2.067
			parserParts.declarationParser.ApplyAttributes(new DVariable());

			Expect(DTokens.OpenCurlyBrace);

			var l = new List<AbstractStatement>();
			while (!IsEOF && laKind != (DTokens.CloseCurlyBrace))
			{
				bool retrying = false;
				Retry:
				bool noStatement = false;
				switch (laKind)
				{
					case DTokens.Align:
						als = new AsmAlignStatement() { Location = la.Location, Parent = s };
						Step();
						als.ValueExpression = parserParts.expressionsParser.Expression(Scope);
						l.Add(als);
						Step();
						break;
					case DTokens.Identifier:
						var opCode = AsmInstructionStatement.OpCode.__UNKNOWN__;
						var dataType = AsmRawDataStatement.DataType.__UNKNOWN__;
						if (Peek(1).Kind == DTokens.Colon)
						{
							l.Add(new LabeledStatement() { Location = la.Location, Parent = s, Identifier = la.Value, EndLocation = Peek(1).EndLocation });
							Step();
							Step();
							if (laKind == DTokens.Semicolon)
								Step();
							continue;
						}

						if (AsmRawDataStatement.TryParseDataType(la.Value, out dataType))
							l.Add(new AsmRawDataStatement() { Location = la.Location, Parent = s, TypeOfData = dataType });
						else if (AsmInstructionStatement.TryParseOpCode(la.Value, out opCode))
							l.Add(new AsmInstructionStatement() { Location = la.Location, Parent = s, Operation = opCode });
						else switch (la.Value.ToLower())
							{
								case "pause":
									SynErr(DTokens.Identifier, "Pause is not supported by dmd's assembler. Use `rep; nop;` instead to achieve the same effect.");
									break;
								case "even":
									als = new AsmAlignStatement() { Location = la.Location, Parent = s };
									als.ValueExpression = new IdentifierExpression(2) { Location = la.Location, EndLocation = la.EndLocation };
									l.Add(als);
									break;
								case "naked":
									noStatement = true;
									break;
								default:
									SynErr(DTokens.Identifier, "Unknown op-code!");
									l.Add(new AsmInstructionStatement() { Location = la.Location, Parent = s, Operation = AsmInstructionStatement.OpCode.__UNKNOWN__ });
									break;
							}
						Step();

						if (noStatement && laKind != DTokens.Semicolon)
							SynErr(DTokens.Semicolon);
						var parentStatement = noStatement ? s : l[l.Count - 1];
						var args = new List<IExpression>();
						if (IsEOF)
							args.Add(new TokenExpression(DTokens.Incomplete));
						else if (laKind != DTokens.Semicolon)
						{
							while (true)
							{
								if (laKind == DTokens.CloseCurlyBrace)
								{
									// This is required as a custom error message because
									// it would complain about finding an identifier instead.
									SynErr(DTokens.Semicolon, "; expected, } found");
									break;
								}
								var e = ParseAsmExpression(Scope, parentStatement);
								if (e != null)
									args.Add(e);
								if (laKind == DTokens.Comma)
								{
									Step();
									continue;
								}
								if (IsEOF)
									args.Add(new TokenExpression(DTokens.Incomplete));
								if (!Expect(DTokens.Semicolon))
								{
									while (laKind != DTokens.Semicolon && laKind != DTokens.CloseCurlyBrace && !IsEOF)
										Step();
									if (laKind == DTokens.Semicolon)
										Step();
								}

								break;
							}
						}
						else
							Step();
						if (parentStatement is AsmInstructionStatement)
							((AsmInstructionStatement)parentStatement).Arguments = args.ToArray();
						else if (parentStatement is AsmRawDataStatement)
							((AsmRawDataStatement)parentStatement).Data = args.ToArray();
						break;
					case DTokens.Semicolon:
						Step();
						break;
					case DTokens.Literal:
						l.Add(new AsmRawDataStatement
						{
							Location = la.Location,
							Data = new[] { ParseAsmPrimaryExpression(Scope, Parent) },
							EndLocation = t.EndLocation,
							Parent = Parent
						});

						Expect(DTokens.Semicolon);
						break;
					default:
						string val;
						if (!retrying && DTokens.TryGetKeywordString(laKind, out val))
						{
							la.LiteralValue = val;
							la.Kind = DTokens.Identifier;
							Lexer.laKind = DTokens.Identifier;
							retrying = true;
							goto Retry;
						}
						else
						{
							noStatement = true;
							SynErr(DTokens.Identifier);
							Step();
						}
						break;
				}

				if (!noStatement)
					l[l.Count - 1].EndLocation = t.Location;
			}

			if (!Expect(DTokens.CloseCurlyBrace) && (t.Kind == DTokens.OpenCurlyBrace || t.Kind == DTokens.Semicolon) && IsEOF)
				l.Add(new AsmInstructionStatement() { Operation = AsmInstructionStatement.OpCode.__UNKNOWN__ });

			s.EndLocation = t.EndLocation;
			s.Instructions = l.ToArray();
			return s;
		}

		IExpression ParseAsmExpression(IBlockNode Scope, IStatement Parent)
		{
			var left = ParseAsmLogOrExpression(Scope, Parent);
			while (laKind == DTokens.Question)
			{
				Step();
				var e = new ConditionalExpression();
				e.TrueCaseExpression = ParseAsmExpression(Scope, Parent);
				Expect(DTokens.Colon);
				e.FalseCaseExpression = ParseAsmExpression(Scope, Parent);
				left = e;
			}
			return left;
		}

		IExpression ParseAsmLogOrExpression(IBlockNode Scope, IStatement Parent)
		{
			var left = ParseAsmLogAndExpression(Scope, Parent);
			while (laKind == DTokens.LogicalOr)
			{
				Step();
				var e = new OrOrExpression();
				e.LeftOperand = left;
				e.RightOperand = ParseAsmLogAndExpression(Scope, Parent);
				left = e;
			}
			return left;
		}

		IExpression ParseAsmLogAndExpression(IBlockNode Scope, IStatement Parent)
		{
			var left = ParseAsmOrExpression(Scope, Parent);
			while (laKind == DTokens.LogicalAnd)
			{
				Step();
				var e = new AndAndExpression();
				e.LeftOperand = left;
				e.RightOperand = ParseAsmOrExpression(Scope, Parent);
				left = e;
			}
			return left;
		}

		IExpression ParseAsmOrExpression(IBlockNode Scope, IStatement Parent)
		{
			var left = ParseAsmXorExpression(Scope, Parent);
			while (laKind == DTokens.BitwiseOr)
			{
				Step();
				var e = new OrExpression();
				e.LeftOperand = left;
				e.RightOperand = ParseAsmXorExpression(Scope, Parent);
				left = e;
			}
			return left;
		}

		IExpression ParseAsmXorExpression(IBlockNode Scope, IStatement Parent)
		{
			var left = ParseAsmAndExpression(Scope, Parent);
			while (laKind == DTokens.Xor)
			{
				Step();
				var e = new XorExpression();
				e.LeftOperand = left;
				e.RightOperand = ParseAsmAndExpression(Scope, Parent);
				left = e;
			}
			return left;
		}

		IExpression ParseAsmAndExpression(IBlockNode Scope, IStatement Parent)
		{
			var left = ParseAsmEqualExpression(Scope, Parent);
			while (laKind == DTokens.BitwiseAnd)
			{
				Step();
				var e = new AndExpression();
				e.LeftOperand = left;
				e.RightOperand = ParseAsmEqualExpression(Scope, Parent);
				left = e;
			}
			return left;
		}

		IExpression ParseAsmEqualExpression(IBlockNode Scope, IStatement Parent)
		{
			var left = ParseAsmRelExpression(Scope, Parent);
			while (laKind == DTokens.Equal || laKind == DTokens.NotEqual)
			{
				Step();
				var e = new EqualExpression(t.Kind == DTokens.NotEqual);
				e.LeftOperand = left;
				e.RightOperand = ParseAsmRelExpression(Scope, Parent);
				left = e;
			}
			return left;
		}

		IExpression ParseAsmRelExpression(IBlockNode Scope, IStatement Parent)
		{
			var left = ParseAsmShiftExpression(Scope, Parent);
			while (true)
			{
				switch (laKind)
				{
					case DTokens.LessThan:
					case DTokens.LessEqual:
					case DTokens.GreaterThan:
					case DTokens.GreaterEqual:
						Step();
						var e = new RelExpression(t.Kind);
						e.LeftOperand = left;
						e.RightOperand = ParseAsmShiftExpression(Scope, Parent);
						left = e;
						continue;
					default:
						return left;
				}
			}
		}

		IExpression ParseAsmShiftExpression(IBlockNode Scope, IStatement Parent)
		{
			var left = ParseAsmAddExpression(Scope, Parent);
			while (laKind == DTokens.ShiftRight || laKind == DTokens.ShiftRightUnsigned || laKind == DTokens.ShiftLeft)
			{
				Step();
				var e = new ShiftExpression(t.Kind);
				e.LeftOperand = left;
				e.RightOperand = ParseAsmAddExpression(Scope, Parent);
				left = e;
			}
			return left;
		}

		IExpression ParseAsmAddExpression(IBlockNode Scope, IStatement Parent)
		{
			var left = ParseAsmMulExpression(Scope, Parent);
			while (laKind == DTokens.Plus || laKind == DTokens.Minus)
			{
				Step();
				var e = new AddExpression(t.Kind == DTokens.Minus);
				e.LeftOperand = left;
				e.RightOperand = ParseAsmMulExpression(Scope, Parent);
				left = e;
			}
			return left;
		}

		IExpression ParseAsmMulExpression(IBlockNode Scope, IStatement Parent)
		{
			IExpression left = ParseAsmBracketExpression(Scope, Parent);
			while (laKind == DTokens.Times || laKind == DTokens.Div || laKind == DTokens.Mod)
			{
				Step();
				var e = new MulExpression(t.Kind);
				e.LeftOperand = left;
				e.RightOperand = ParseAsmBracketExpression(Scope, Parent);
				left = e;
			}
			return left;
		}

		IExpression ParseAsmBracketExpression(IBlockNode Scope, IStatement Parent)
		{
			var left = ParseAsmUnaryExpression(Scope, Parent);
			while (laKind == DTokens.OpenSquareBracket)
			{
				Step();
				left = new PostfixExpression_ArrayAccess(ParseAsmExpression(Scope, Parent)) { PostfixForeExpression = left };
				Expect(DTokens.CloseSquareBracket);
				(left as PostfixExpression_ArrayAccess).EndLocation = t.EndLocation;
			}
			return left;
		}

		IExpression ParseAsmUnaryExpression(IBlockNode Scope, IStatement Parent)
		{
			switch (laKind)
			{
				case DTokens.Byte:
					la.LiteralValue = "byte";
					goto case DTokens.Identifier;
				case DTokens.Short:
					la.LiteralValue = "short";
					goto case DTokens.Identifier;
				case DTokens.Int:
					la.LiteralValue = "int";
					goto case DTokens.Identifier;
				case DTokens.Float:
					la.LiteralValue = "float";
					goto case DTokens.Identifier;
				case DTokens.Double:
					la.LiteralValue = "double";
					goto case DTokens.Identifier;
				case DTokens.Real:
					la.LiteralValue = "real";
					goto case DTokens.Identifier;

				case DTokens.Identifier:
					switch (la.Value)
					{
						case "seg":
							Step();
							return new PostfixExpression_Access() { PostfixForeExpression = ParseAsmExpression(Scope, Parent), AccessExpression = new IdentifierExpression("seg") };
						case "offsetof":
							Step();
							return new PostfixExpression_Access() { PostfixForeExpression = ParseAsmExpression(Scope, Parent), AccessExpression = new IdentifierExpression("offsetof") };
						case "near":
						case "far":
						case "byte":
						case "short":
						case "int":
						case "word":
						case "dword":
						case "qword":
						case "float":
						case "double":
						case "real":
							// TODO: Put this information in the AST
							Step();
							if (laKind == DTokens.Identifier && la.Value == "ptr")
								Step();
							else if (t.Value != "short")
								SynErr(DTokens.Identifier, "Expected ptr!");
							else if (!(Parent is AsmInstructionStatement) || !((AsmInstructionStatement)Parent).IsJmpFamily)
								SynErr(DTokens.Identifier, "A short reference is only valid for the jmp family of instructions!");
							return ParseAsmExpression(Scope, Parent);

						default:
							return ParseAsmPrimaryExpression(Scope, Parent);
					}
				case DTokens.Plus:
					Step();
					return new UnaryExpression_Add() { UnaryExpression = ParseAsmUnaryExpression(Scope, Parent) };
				case DTokens.Minus:
					Step();
					return new UnaryExpression_Sub() { UnaryExpression = ParseAsmUnaryExpression(Scope, Parent) };
				case DTokens.Not:
					Step();
					return new UnaryExpression_Not() { UnaryExpression = ParseAsmUnaryExpression(Scope, Parent) };
				case DTokens.Tilde:
					Step();
					return new UnaryExpression_Cat() { UnaryExpression = ParseAsmUnaryExpression(Scope, Parent) };
				default:
					return ParseAsmPrimaryExpression(Scope, Parent);
			}
		}

		IExpression ParseAsmPrimaryExpression(IBlockNode Scope, IStatement Parent)
		{
			switch (laKind)
			{
				case DTokens.OpenSquareBracket:
					Step();
					var e = new PostfixExpression_ArrayAccess(ParseAsmExpression(Scope, Parent));
					Expect(DTokens.CloseSquareBracket);
					e.EndLocation = t.EndLocation;
					return e;
				case DTokens.Dollar:
					var ins = Parent as AsmInstructionStatement;
					if (ins == null || (!ins.IsJmpFamily && ins.Operation != AsmInstructionStatement.OpCode.call))
						SynErr(DTokens.Dollar, "The $ operator is only valid on jmp and call instructions!");
					Step();
					return new TokenExpression(t.Kind) { Location = t.Location, EndLocation = t.EndLocation };
				case DTokens.Literal:
					Step();
					return new IdentifierExpression(t.LiteralValue, t.LiteralFormat, t.Subformat) { Location = t.Location, EndLocation = t.EndLocation };
				case DTokens.This:
					Step();
					return new TokenExpression(DTokens.This) { Location = t.Location, EndLocation = t.EndLocation };

				// AsmTypePrefix
				case DTokens.Byte:
				case DTokens.Short:
				case DTokens.Int:
				case DTokens.Float:
				case DTokens.Double:
				case DTokens.Real:

				case DTokens.__LOCAL_SIZE:
					Step();
					return new TokenExpression(t.Kind) { Location = t.Location, EndLocation = t.EndLocation };
				case DTokens.Identifier:
					Step();
					if (AsmRegisterExpression.IsRegister(t.Value))
					{
						string reg = t.Value;
						if (reg == "ST" && laKind == DTokens.OpenParenthesis)
						{
							reg += "(";
							Step();
							if (Expect(DTokens.Literal))
							{
								reg += t.LiteralValue.ToString();
								if (laKind != DTokens.CloseParenthesis)
									SynErr(DTokens.CloseParenthesis);
								else
									Step();
								reg += ")";
							}
						}
						switch (reg)
						{
							case "ES":
							case "CS":
							case "SS":
							case "DS":
							case "GS":
							case "FS":
								if (laKind == DTokens.Colon)
								{
									var ex = new AsmRegisterExpression() { Location = t.Location, EndLocation = t.EndLocation, Register = string.Intern(reg) };
									Step();
									// NOTE: DMD actually allows you to not have an expression after a
									//       segment specifier, however I consider this a bug, and, as
									//       such, am making an expression in that form fail to parse.
									return new UnaryExpression_SegmentBase() { RegisterExpression = ex, UnaryExpression = ParseAsmExpression(Scope, Parent) };
								}
								goto default;
							default:
								// This check is required because of how ST registers are handled.
								if (AsmRegisterExpression.IsRegister(reg))
									return new AsmRegisterExpression() { Location = t.Location, EndLocation = t.EndLocation, Register = string.Intern(reg) };
								SynErr(DTokens.Identifier, "Unknown register!");
								return IsEOF ? new TokenExpression(DTokens.Incomplete) : null;
						}
					}
					else
					{
						IExpression outer = new IdentifierExpression(t.Value) { Location = t.Location, EndLocation = t.EndLocation };
						while (laKind == DTokens.Dot)
						{
							Step();
							if (Expect(DTokens.Identifier))
								outer = new PostfixExpression_Access() { AccessExpression = new IdentifierExpression(t.Value), PostfixForeExpression = outer };
							else
								outer = new TokenExpression(DTokens.Incomplete);
							Step();
						}
						return outer;
					}
				default:
					SynErr(DTokens.Identifier, "Expected a $, literal or an identifier!");
					Step();
					if (IsEOF)
						return new TokenExpression(DTokens.Incomplete);
					return null;
			}
		}
	}
}
