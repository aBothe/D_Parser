using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;

namespace D_Parser.Parser.Implementations
{
	class DExpressionsParser : DParserImplementationPart
	{
		readonly DParserParts parserParts;

		public DExpressionsParser(DParserStateContext stateContext, DParserParts parserParts)
			: base(stateContext) { this.parserParts = parserParts; }

		public IExpression Expression(IBlockNode Scope = null)
		{
			// AssignExpression
			var ass = AssignExpression(Scope);
			if (laKind != (DTokens.Comma))
				return ass;

			/*
			 * The following is a leftover of C syntax and proably cause some errors when parsing arguments etc.
			 */
			// AssignExpression , Expression
			var ae = new Expression();
			ae.Add(ass);
			while (laKind == (DTokens.Comma))
			{
				Step();
				ae.Add(AssignExpression(Scope));
			}
			return ae;
		}

		/// <summary>
		/// This function has a very high importance because here we decide whether it's a declaration or assignExpression!
		/// </summary>
		public bool IsAssignExpression()
		{
			if (parserParts.declarationParser.LookAheadIsStorageClass)
				return false;

			if (!parserParts.declarationParser.IsBasicType())
				return true;

			if (DTokensSemanticHelpers.IsBasicType(laKind))
			{
				if (Lexer.CurrentPeekToken.Kind != DTokens.Dot && Peek().Kind != DTokens.Identifier)
				{
					/*
					 * PrimaryExpression allows
					 * BasicType . Identifier 
					 * --> if BasicType IS int or float etc., and NO dot follows, it must be a type
					 */
					Peek(1);
					return false;
				}
			}

			// uint[]** MyArray;
			else
			{
				// Skip initial dot
				if (laKind == DTokens.Dot)
					Peek();

				if (Lexer.CurrentPeekToken.Kind != DTokens.Identifier)
				{
					if (laKind == DTokens.Identifier || laKind == DTokens.Dot)
					{
						// Skip initial identifier list
						if (Lexer.CurrentPeekToken.Kind == DTokens.Not)
						{
							Peek();
							if (Lexer.CurrentPeekToken.Kind != DTokens.Is && Lexer.CurrentPeekToken.Kind != DTokens.In)
							{
								if (Lexer.CurrentPeekToken.Kind == (DTokens.OpenParenthesis))
									OverPeekBrackets(DTokens.OpenParenthesis);
								else
									Peek();

								if (Lexer.CurrentPeekToken.Kind == DTokens.EOF)
								{
									Peek(1);
									return true;
								}
							}
						}

						while (Lexer.CurrentPeekToken.Kind == DTokens.Dot)
						{
							Peek();

							if (Lexer.CurrentPeekToken.Kind == DTokens.Identifier)
							{
								Peek();

								if (Lexer.CurrentPeekToken.Kind == DTokens.Not)
								{
									Peek();
									if (Lexer.CurrentPeekToken.Kind != DTokens.Is && Lexer.CurrentPeekToken.Kind != DTokens.In)
									{
										if (Lexer.CurrentPeekToken.Kind == (DTokens.OpenParenthesis))
											OverPeekBrackets(DTokens.OpenParenthesis);
										else
											Peek();
									}

									if (Lexer.CurrentPeekToken.Kind == DTokens.EOF)
									{
										Peek(1);
										return true;
									}
								}
							}
							else
							{
								/*
								 * If a non-identifier follows a dot, treat it as expression, not as declaration.
								 */
								Peek(1);
								return true;
							}
						}
					}
					else if (Lexer.CurrentPeekToken.Kind == DTokens.OpenParenthesis)
					{
						if (parserParts.attributesParser.IsFunctionAttribute)
						{
							OverPeekBrackets(DTokens.OpenParenthesis);
							bool isPrimitiveExpr = Lexer.CurrentPeekToken.Kind == DTokens.Dot || Lexer.CurrentPeekToken.Kind == DTokens.OpenParenthesis;
							Peek(1);
							return isPrimitiveExpr;
						}
						else if (laKind == DTokens.Typeof || laKind == DTokens.__vector)
							OverPeekBrackets(DTokens.OpenParenthesis);
					}
				}
			}

			if (Lexer.CurrentPeekToken == null)
				Peek();


			// Skip basictype2's
			bool HadPointerDeclaration = false;
			while (Lexer.CurrentPeekToken.Kind == DTokens.Times || Lexer.CurrentPeekToken.Kind == DTokens.OpenSquareBracket)
			{
				if (Lexer.CurrentPeekToken.Kind == DTokens.Times)
				{
					HadPointerDeclaration = true;
					Peek();
					if (Lexer.CurrentPeekToken.Kind == DTokens.Literal)
					{ // char[a.member*8] abc; // conv.d:3278
						Peek(1);
						return true;
					}
				}

				else // if (Lexer.CurrentPeekToken.Kind == OpenSquareBracket)
				{
					Peek();
					if (DTokensSemanticHelpers.IsBasicType(Lexer.CurrentPeekToken)
						&& !(Lexer.CurrentPeekToken.Kind == DTokens.Identifier || Lexer.CurrentPeekToken.Kind == DTokens.Dot))
					{
						Peek(1);
						return false;
					}
					OverPeekBrackets(DTokens.OpenSquareBracket, true);
					if (Lexer.CurrentPeekToken.Kind == DTokens.EOF) // Due to completion purposes
						return true;
				}
			}

			var pkKind = Lexer.CurrentPeekToken.Kind;
			Peek(1);

			// And now, after having skipped the basictype and possible trailing basictype2's,
			// we check for an identifier or delegate declaration to ensure that there's a declaration and not an expression
			// Addition: If a times token ('*') follows an identifier list, we can assume that we have a declaration and NOT an expression!
			// Example: *a=b is an expression; a*=b is not possible (and a Multiply-Assign-Expression) - instead something like A* a should be taken...
			switch (pkKind)
			{
				case DTokens.Identifier:
				case DTokens.Delegate:
				case DTokens.Function:
				case DTokens.EOF: // Also assume a declaration if no further token follows
				case DTokens.__EOF__:
					return false;
				default:
					return !HadPointerDeclaration;
			}
		}

		public IExpression AssignExpression(IBlockNode Scope = null)
		{
			var left = ConditionalExpression(Scope);
			if (!DTokensSemanticHelpers.IsAssignOperator(laKind))
				return left;

			Step();
			var ate = new AssignExpression(t.Kind);
			ate.LeftOperand = left;
			ate.RightOperand = AssignExpression(Scope);
			return ate;
		}

		public IExpression ConditionalExpression(IBlockNode Scope = null)
		{
			var trigger = OrOrExpression(Scope);
			if (laKind != (DTokens.Question))
				return trigger;

			Expect(DTokens.Question);
			var se = new ConditionalExpression() { OrOrExpression = trigger };
			se.TrueCaseExpression = Expression(Scope);
			if (Expect(DTokens.Colon))
				se.FalseCaseExpression = ConditionalExpression(Scope);
			return se;
		}

		IExpression OrOrExpression(IBlockNode Scope = null)
		{
			var left = AndAndExpression(Scope);
			if (laKind != DTokens.LogicalOr)
				return left;

			Step();
			var ae = new OrOrExpression();
			ae.LeftOperand = left;
			ae.RightOperand = OrOrExpression(Scope);
			return ae;
		}

		IExpression AndAndExpression(IBlockNode Scope = null)
		{
			// Note: Due to making it easier to parse, we ignore the OrExpression-CmpExpression rule
			// -> So we only assume that there's a OrExpression

			var left = OrExpression(Scope);
			if (laKind != DTokens.LogicalAnd)
				return left;

			Step();
			var ae = new AndAndExpression();
			ae.LeftOperand = left;
			ae.RightOperand = AndAndExpression(Scope);
			return ae;
		}

		IExpression OrExpression(IBlockNode Scope = null)
		{
			var left = XorExpression(Scope);
			if (laKind != DTokens.BitwiseOr)
				return left;

			Step();
			var ae = new OrExpression();
			ae.LeftOperand = left;
			ae.RightOperand = OrExpression(Scope);
			return ae;
		}

		IExpression XorExpression(IBlockNode Scope = null)
		{
			var left = AndExpression(Scope);
			if (laKind != DTokens.Xor)
				return left;

			Step();
			var ae = new XorExpression();
			ae.LeftOperand = left;
			ae.RightOperand = XorExpression(Scope);
			return ae;
		}

		IExpression AndExpression(IBlockNode Scope = null)
		{
			// Note: Since we ignored all kinds of CmpExpressions in AndAndExpression(), we have to take CmpExpression instead of ShiftExpression here!
			var left = CmpExpression(Scope);
			if (laKind != DTokens.BitwiseAnd)
				return left;

			Step();
			var ae = new AndExpression();
			ae.LeftOperand = left;
			ae.RightOperand = AndExpression(Scope);
			return ae;
		}

		IExpression CmpExpression(IBlockNode Scope = null)
		{
			// TODO: Make this into a switch.
			var left = ShiftExpression(Scope);

			OperatorBasedExpression ae;

			switch (laKind)
			{
				case DTokens.Equal:
				case DTokens.NotEqual:
					ae = new EqualExpression(laKind == DTokens.NotEqual);
					break;

				case DTokens.LessThan:
				case DTokens.LessEqual:
				case DTokens.GreaterThan:
				case DTokens.GreaterEqual:
				case DTokens.Unordered:
				case DTokens.LessOrGreater:
				case DTokens.LessEqualOrGreater:
				case DTokens.UnorderedOrGreater:
				case DTokens.UnorderedGreaterOrEqual:
				case DTokens.UnorderedOrLess:
				case DTokens.UnorderedLessOrEqual:
				case DTokens.UnorderedOrEqual:
					ae = new RelExpression(laKind);
					break;

				case DTokens.Is:
					ae = new IdentityExpression(false, la.Location);
					break;

				case DTokens.In:
					ae = new InExpression(false, la.Location);
					break;

				case DTokens.Not:
					var peekToken = Peek(1);
					switch (peekToken.Kind)
					{
						case DTokens.Is:
							ae = new IdentityExpression(false, peekToken.Location);
							Step();
							break;
						case DTokens.In:
							ae = new InExpression(true, peekToken.Location);
							Step();
							break;
						default:
							return left;
					}
					break;

				default:
					return left;
			}

			// Skip operator
			Step();

			ae.LeftOperand = left;
			ae.RightOperand = ShiftExpression(Scope);
			return ae;
		}

		IExpression ShiftExpression(IBlockNode Scope = null)
		{
			var left = AddExpression(Scope);
			if (!(laKind == DTokens.ShiftLeft || laKind == DTokens.ShiftRight || laKind == DTokens.ShiftRightUnsigned))
				return left;

			Step();
			var ae = new ShiftExpression(t.Kind);
			ae.LeftOperand = left;
			ae.RightOperand = ShiftExpression(Scope);
			return ae;
		}

		/// <summary>
		/// Note: Add, Multiply as well as Cat Expressions are parsed in this method.
		/// </summary>
		IExpression AddExpression(IBlockNode Scope = null)
		{
			var left = UnaryExpression(Scope);

			OperatorBasedExpression ae = null;

			switch (laKind)
			{
				case DTokens.Plus:
				case DTokens.Minus:
					ae = new AddExpression(laKind == DTokens.Minus);
					break;
				case DTokens.Tilde:
					ae = new CatExpression();
					break;
				case DTokens.Times:
				case DTokens.Div:
				case DTokens.Mod:
					ae = new MulExpression(laKind);
					break;
				default:
					return left;
			}

			Step();

			ae.LeftOperand = left;
			ae.RightOperand = AddExpression(Scope);
			return ae;
		}

		IExpression UnaryExpression(IBlockNode Scope = null)
		{
			switch (laKind)
			{
				// Note: PowExpressions are handled in PowExpression()
				case DTokens.BitwiseAnd:
				case DTokens.Increment:
				case DTokens.Decrement:
				case DTokens.Times:
				case DTokens.Minus:
				case DTokens.Plus:
				case DTokens.Not:
				case DTokens.Tilde:
					Step();
					SimpleUnaryExpression sue;
					switch (t.Kind)
					{
						case DTokens.BitwiseAnd:
							sue = new UnaryExpression_And();
							break;
						case DTokens.Increment:
							sue = new UnaryExpression_Increment();
							break;
						case DTokens.Decrement:
							sue = new UnaryExpression_Decrement();
							break;
						case DTokens.Times:
							sue = new UnaryExpression_Mul();
							break;
						case DTokens.Minus:
							sue = new UnaryExpression_Sub();
							break;
						case DTokens.Plus:
							sue = new UnaryExpression_Add();
							break;
						case DTokens.Tilde:
							sue = new UnaryExpression_Cat();
							break;
						case DTokens.Not:
							sue = new UnaryExpression_Not();
							break;
						default:
							SynErr(t.Kind, "Illegal token for unary expressions");
							return null;
					}
					sue.Location = t.Location;
					sue.UnaryExpression = UnaryExpression(Scope);
					return sue;

				// CastExpression
				case DTokens.Cast:
					Step();
					var ce = new CastExpression { Location = t.Location };

					if (Expect(DTokens.OpenParenthesis))
					{
						if (laKind != DTokens.CloseParenthesis) // Yes, it is possible that a cast() can contain an empty type!
							ce.Type = parserParts.declarationParser.Type(Scope);
						Expect(DTokens.CloseParenthesis);
					}
					ce.UnaryExpression = UnaryExpression(Scope);
					ce.EndLocation = t.EndLocation;
					return ce;

				// DeleteExpression
				case DTokens.Delete:
					Step();
					return new DeleteExpression() { UnaryExpression = UnaryExpression(Scope) };

				// PowExpression
				default:
					var left = PostfixExpression(Scope);

					if (laKind != DTokens.Pow)
						return left;

					Step();
					var pe = new PowExpression();
					pe.LeftOperand = left;
					pe.RightOperand = UnaryExpression(Scope);

					return pe;
			}
		}

		IExpression NewExpression(IBlockNode Scope = null)
		{
			Expect(DTokens.New);
			var startLoc = t.Location;

			IExpression[] newArgs = null;
			// NewArguments
			if (laKind == (DTokens.OpenParenthesis))
			{
				Step();
				if (laKind != (DTokens.CloseParenthesis))
					newArgs = ArgumentList(Scope).ToArray();
				Expect(DTokens.CloseParenthesis);
			}

			/*
			 * If there occurs a class keyword here, interpretate it as an anonymous class definition
			 * http://digitalmars.com/d/2.0/expression.html#NewExpression
			 * 
			 * NewArguments ClassArguments BaseClasslist_opt { DeclDefs } 
			 * 
			 * http://digitalmars.com/d/2.0/class.html#anonymous
			 * 
				NewAnonClassExpression:
					new PerenArgumentListopt class PerenArgumentList_opt SuperClass_opt InterfaceClasses_opt ClassBody

				PerenArgumentList:
					(ArgumentList)
			 * 
			 */
			if (laKind == (DTokens.Class))
			{
				Step();
				var ac = new AnonymousClassExpression();
				ac.NewArguments = newArgs;
				ac.Location = startLoc;

				// ClassArguments
				if (laKind == (DTokens.OpenParenthesis))
				{
					Step();
					if (laKind != DTokens.CloseParenthesis)
						ac.ClassArguments = ArgumentList(Scope).ToArray();
					Expect(DTokens.CloseParenthesis);
				}

				var anclass = new DClassLike(DTokens.Class)
				{
					IsAnonymousClass = true,
					Location = startLoc
				};

				// BaseClasslist_opt
				if (laKind != DTokens.OpenCurlyBrace)
				{
					parserParts.bodiedSymbolsParser.BaseClassList(anclass, laKind == DTokens.Colon);
					// SuperClass_opt InterfaceClasses_opt
					if (laKind != DTokens.OpenCurlyBrace)
						parserParts.bodiedSymbolsParser.BaseClassList(anclass, false);
				}

				parserParts.bodiedSymbolsParser.ClassBody(anclass);

				ac.AnonymousClass = anclass;

				ac.EndLocation = t.EndLocation;

				if (Scope != null && !AllowWeakTypeParsing)
					Scope.Add(ac.AnonymousClass);

				return ac;
			}

			// NewArguments Type
			else
			{
				var nt = parserParts.declarationParser.BasicType(Scope);
				parserParts.declarationParser.ParseBasicType2(ref nt, Scope);

				var initExpr = new NewExpression()
				{
					NewArguments = newArgs,
					Type = nt,
					Location = startLoc
				};

				List<IExpression> args;

				var ad = nt as ArrayDecl;

				if ((ad == null || ad.ClampsEmpty) && laKind == DTokens.OpenParenthesis)
				{
					Step();
					if (laKind != DTokens.CloseParenthesis)
						args = ArgumentList(Scope);
					else
						args = new List<IExpression>();

					if (Expect(DTokens.CloseParenthesis))
						initExpr.EndLocation = t.EndLocation;
					else
						initExpr.EndLocation = CodeLocation.Empty;

					if (ad != null)
					{
						if (args.Count == 0)
						{
							SemErr(DTokens.CloseParenthesis, "Size for the rightmost array dimension needed");

							initExpr.EndLocation = t.EndLocation;
							return initExpr;
						}

						while (ad != null)
						{
							if (args.Count == 0)
								break;

							ad.KeyType = null;
							ad.KeyExpression = args[args.Count - 1];

							args.RemoveAt(args.Count - 1);

							ad = ad.InnerDeclaration as ArrayDecl;
						}
					}
				}
				else
				{
					initExpr.EndLocation = t.EndLocation;
					args = new List<IExpression>();
				}

				ad = nt as ArrayDecl;

				if (ad != null && ad.KeyExpression == null)
				{
					if (ad.KeyType == null)
						SemErr(DTokens.CloseSquareBracket, "Size of array expected");
				}

				initExpr.Arguments = args.ToArray();

				return initExpr;
			}
		}

		public List<IExpression> ArgumentList(IBlockNode Scope = null)
		{
			var ret = new List<IExpression>();

			if (laKind == DTokens.CloseParenthesis)
				return ret;

			ret.Add(AssignExpression(Scope));

			while (laKind == (DTokens.Comma))
			{
				Step();
				if (laKind == DTokens.CloseParenthesis)
					break;
				ret.Add(AssignExpression(Scope));
			}

			return ret;
		}

		public IExpression PostfixExpression(IBlockNode Scope = null)
		{
			IExpression leftExpr = null;

			/*
			 * Despite the following syntax is an explicit UnaryExpression (see http://dlang.org/expression.html#UnaryExpression),
			 * stuff like (MyType).init[] is actually allowed - so it's obviously a PostfixExpression! (Nov 13 2013)
			 */

			// ( Type ) . Identifier
			if (laKind == DTokens.OpenParenthesis)
			{
				Lexer.StartPeek();
				OverPeekBrackets(DTokens.OpenParenthesis, false);
				var dotToken = Lexer.CurrentPeekToken;

				if (Lexer.CurrentPeekToken.Kind == DTokens.Dot &&
					(Peek().Kind == DTokens.Identifier || Lexer.CurrentPeekToken.Kind == DTokens.EOF))
				{
					var wkParsing = AllowWeakTypeParsing;
					AllowWeakTypeParsing = true;
					Lexer.PushLookAheadBackup();
					Step();
					var startLoc = t.Location;

					var td = parserParts.declarationParser.Type(Scope);

					AllowWeakTypeParsing = wkParsing;

					/*				
					 * (a. -- expression: (a.myProp + 2) / b;
					 * (int. -- must be expression anyway
					 * (const).asdf -- definitely unary expression ("type")
					 * (const). -- also treat it as type accessor
					 */
					if (td != null &&
						laKind == DTokens.CloseParenthesis && Lexer.CurrentPeekToken == dotToken) // Also take it as a type declaration if there's nothing following (see Expression Resolving)
					{
						Step();  // Skip to )
						if (laKind == DTokens.Dot)
						{
							Step();  // Skip to .
							if ((laKind == DTokens.Identifier && Peek(1).Kind != DTokens.Not && Peek(1).Kind != DTokens.OpenParenthesis) || IsEOF)
							{
								Lexer.PopLookAheadBackup();
								Step();  // Skip to identifier

								leftExpr = new UnaryExpression_Type()
								{
									Type = td,
									AccessIdentifier = t.Value,
									Location = startLoc,
									EndLocation = t.EndLocation
								};
							}
							else
								Lexer.RestoreLookAheadBackup();
						}
						else
							Lexer.RestoreLookAheadBackup();
					}
					else
						Lexer.RestoreLookAheadBackup();
				}
			}

			// PostfixExpression
			if (leftExpr == null)
				leftExpr = PrimaryExpression(Scope);

			while (!IsEOF)
			{
				switch (laKind)
				{
					case DTokens.Dot:
						Step();

						var pea = new PostfixExpression_Access
						{
							PostfixForeExpression = leftExpr
						};

						leftExpr = pea;

						if (laKind == DTokens.New)
							pea.AccessExpression = PostfixExpression(Scope);
						else if (parserParts.declarationParser.IsTemplateInstance)
							pea.AccessExpression = parserParts.templatesParser.TemplateInstance(Scope);
						else if (Expect(DTokens.Identifier))
							pea.AccessExpression = new IdentifierExpression(t.Value)
							{
								Location = t.Location,
								EndLocation = t.EndLocation
							};
						else if (IsEOF)
							pea.AccessExpression = new TokenExpression(DTokens.Incomplete);

						pea.EndLocation = t.EndLocation;
						break;
					case DTokens.Increment:
					case DTokens.Decrement:
						Step();
						var peid = t.Kind == DTokens.Increment ? (PostfixExpression)new PostfixExpression_Increment() : new PostfixExpression_Decrement();
						peid.EndLocation = t.EndLocation;
						peid.PostfixForeExpression = leftExpr;
						leftExpr = peid;
						break;
					// Function call
					case DTokens.OpenParenthesis:
						Step();
						var pemc = new PostfixExpression_MethodCall();
						pemc.PostfixForeExpression = leftExpr;
						leftExpr = pemc;

						if (laKind == DTokens.CloseParenthesis)
							Step();
						else
						{
							pemc.Arguments = ArgumentList(Scope).ToArray();
							Expect(DTokens.CloseParenthesis);
						}

						if (IsEOF)
							pemc.EndLocation = CodeLocation.Empty;
						else
							pemc.EndLocation = t.EndLocation;
						break;
					// IndexExpression | SliceExpression
					case DTokens.OpenSquareBracket:
						Step();
						var loc = t.Location;
						var args = new List<PostfixExpression_ArrayAccess.IndexArgument>();

						if (laKind != DTokens.CloseSquareBracket)
						{
							do
							{
								var firstEx = AssignExpression(Scope);
								// [ AssignExpression .. AssignExpression ] || ArgumentList
								if (laKind == DTokens.DoubleDot)
								{
									Step();
									args.Add(new PostfixExpression_ArrayAccess.SliceArgument(firstEx, AssignExpression(Scope)));
								}
								else
									args.Add(new PostfixExpression_ArrayAccess.IndexArgument(firstEx));
							} while (laKind == DTokens.Comma && Expect(DTokens.Comma) &&
								laKind != DTokens.CloseSquareBracket); // Trailing comma allowed https://github.com/aBothe/D_Parser/issues/170
						}

						Expect(DTokens.CloseSquareBracket);
						leftExpr = new PostfixExpression_ArrayAccess(args.ToArray())
						{
							EndLocation = t.EndLocation,
							PostfixForeExpression = leftExpr
						};
						break;
					default:
						return leftExpr;
				}
			}

			return leftExpr;
		}

		public IExpression PrimaryExpression(IBlockNode Scope = null)
		{
			bool isModuleScoped = laKind == DTokens.Dot;
			if (isModuleScoped)
			{
				Step();
				if (IsEOF)
				{
					var dot = new TokenExpression(DTokens.Dot) { Location = t.Location, EndLocation = t.EndLocation };
					return new PostfixExpression_Access { PostfixForeExpression = dot, AccessExpression = new TokenExpression(DTokens.Incomplete) };
				}
			}

			// TemplateInstance
			if (parserParts.declarationParser.IsTemplateInstance)
			{
				var tix = parserParts.templatesParser.TemplateInstance(Scope);
				if (tix != null)
					tix.ModuleScoped = isModuleScoped;
				return tix;
			}

			if (IsLambaExpression())
				return LambaExpression(Scope);

			CodeLocation startLoc;
			switch (laKind)
			{
				// ArrayLiteral | AssocArrayLiteral
				case DTokens.OpenSquareBracket:
					return ArrayLiteral(Scope);
				case DTokens.New:
					return NewExpression(Scope);
				case DTokens.Typeof:
					return TypeDeclarationExpression.TryWrap(parserParts.declarationParser.TypeOf(Scope));
				case DTokens.__traits:
					return TraitsExpression(Scope);
				// Dollar (== Array length expression)
				case DTokens.Dollar:
					Step();
					return new TokenExpression(t.Kind)
					{
						Location = t.Location,
						EndLocation = t.EndLocation
					};
				case DTokens.Identifier:
					Step();
					return new IdentifierExpression(t.Value)
					{
						Location = t.Location,
						EndLocation = t.EndLocation,
						ModuleScoped = isModuleScoped
					};
				// SpecialTokens (this,super,null,true,false,$) // $ has been handled before
				case DTokens.This:
				case DTokens.Super:
				case DTokens.Null:
				case DTokens.True:
				case DTokens.False:
					Step();
					return new TokenExpression(t.Kind)
					{
						Location = t.Location,
						EndLocation = t.EndLocation
					};
				case DTokens.OpenParenthesis:
					if (IsFunctionLiteral())
						goto case DTokens.Function;
					// ( Expression )
					Step();
					var ret = new SurroundingParenthesesExpression() { Location = t.Location };

					ret.Expression = Expression();

					Expect(DTokens.CloseParenthesis);
					ret.EndLocation = t.EndLocation;
					return ret;
				case DTokens.Literal:
					Step();
					startLoc = t.Location;

					// Concatenate multiple string literals here
					if (t.LiteralFormat == LiteralFormat.StringLiteral || t.LiteralFormat == LiteralFormat.VerbatimStringLiteral)
					{
						var sb = new StringBuilder(t.RawCodeRepresentation ?? t.Value);
						while (la.LiteralFormat == LiteralFormat.StringLiteral || la.LiteralFormat == LiteralFormat.VerbatimStringLiteral)
						{
							Step();
							sb.Append(t.RawCodeRepresentation ?? t.Value);
						}
						return new StringLiteralExpression(sb.ToString(), t.LiteralFormat == LiteralFormat.VerbatimStringLiteral, t.Subformat) { Location = startLoc, EndLocation = t.EndLocation };
					}
					//else if (t.LiteralFormat == LiteralFormat.CharLiteral)return new IdentifierExpression(t.LiteralValue) { LiteralFormat=t.LiteralFormat,Location = startLoc, EndLocation = t.EndLocation };
					return new ScalarConstantExpression(t.LiteralValue, t.LiteralFormat, t.Subformat, t.RawCodeRepresentation) { Location = startLoc, EndLocation = t.EndLocation };
				// FunctionLiteral
				case DTokens.Delegate:
				case DTokens.Function:
				case DTokens.OpenCurlyBrace:
					var fl = new FunctionLiteral() { Location = la.Location };
					fl.AnonymousMethod.Location = la.Location;

					if (laKind == DTokens.Delegate || laKind == DTokens.Function)
					{
						Step();
						fl.LiteralToken = t.Kind;
					}

					// file.d:1248
					/*
						listdir (".", delegate bool (DirEntry * de)
						{
							auto s = std.string.format("%s : c %s, w %s, a %s", de.name,
									toUTCString (de.creationTime),
									toUTCString (de.lastWriteTime),
									toUTCString (de.lastAccessTime));
							return true;
						}
						);
					*/
					if (laKind != DTokens.OpenCurlyBrace) // foo( 1, {bar();} ); -> is a legal delegate
					{
						if (!parserParts.attributesParser.IsFunctionAttribute && Lexer.CurrentPeekToken.Kind == DTokens.OpenParenthesis)
							fl.AnonymousMethod.Type = parserParts.declarationParser.BasicType(Scope);
						else if (laKind != DTokens.OpenParenthesis && laKind != DTokens.OpenCurlyBrace)
							fl.AnonymousMethod.Type = parserParts.declarationParser.Type(Scope);

						if (laKind == DTokens.OpenParenthesis)
							parserParts.declarationParser.Parameters(fl.AnonymousMethod);

						parserParts.attributesParser.FunctionAttributes(fl.AnonymousMethod);
					}

					parserParts.bodiedSymbolsParser.FunctionBody(fl.AnonymousMethod);

					if (IsEOF)
						fl.AnonymousMethod.EndLocation = CodeLocation.Empty;

					fl.EndLocation = fl.AnonymousMethod.EndLocation;

					if (Scope != null && !AllowWeakTypeParsing) // HACK -- not only on AllowWeakTypeParsing! But apparently, this stuff may be parsed twice, so force-skip results of the first attempt although this is a rather stupid solution
						Scope.Add(fl.AnonymousMethod);

					return fl;
				// AssertExpression
				case DTokens.Assert:
					Step();
					startLoc = t.Location;
					Expect(DTokens.OpenParenthesis);
					var ce = new AssertExpression() { Location = startLoc };

					var exprs = new List<IExpression>();
					var assertedExpr = AssignExpression(Scope);
					if (assertedExpr != null)
						exprs.Add(assertedExpr);

					if (laKind == (DTokens.Comma))
					{
						Step();
						assertedExpr = AssignExpression(Scope);
						if (assertedExpr != null)
							exprs.Add(assertedExpr);
					}
					ce.AssignExpressions = exprs;
					Expect(DTokens.CloseParenthesis);
					ce.EndLocation = t.EndLocation;
					return ce;
				// MixinExpression
				case DTokens.Mixin:
					Step();
					var me = new MixinExpression() { Location = t.Location };
					if (Expect(DTokens.OpenParenthesis))
					{
						me.AssignExpression = AssignExpression(Scope);
						Expect(DTokens.CloseParenthesis);
					}
					me.EndLocation = t.EndLocation;
					return me;
				// ImportExpression
				case DTokens.Import:
					Step();
					var ie = new ImportExpression() { Location = t.Location };
					Expect(DTokens.OpenParenthesis);

					ie.AssignExpression = AssignExpression(Scope);

					Expect(DTokens.CloseParenthesis);
					ie.EndLocation = t.EndLocation;
					return ie;
				// TypeidExpression
				case DTokens.Typeid:
					Step();
					var tide = new TypeidExpression() { Location = t.Location };
					Expect(DTokens.OpenParenthesis);

					if (IsAssignExpression())
						tide.Expression = AssignExpression(Scope);
					else
					{
						Lexer.PushLookAheadBackup();
						AllowWeakTypeParsing = true;
						tide.Type = parserParts.declarationParser.Type(Scope);
						AllowWeakTypeParsing = false;

						if (tide.Type == null || laKind != DTokens.CloseParenthesis)
						{
							Lexer.RestoreLookAheadBackup();
							tide.Expression = AssignExpression(Scope);
						}
						else
							Lexer.PopLookAheadBackup();
					}

					Expect(DTokens.CloseParenthesis);

					tide.EndLocation = t.EndLocation;
					return tide;
				// IsExpression
				case DTokens.Is:
					Step();
					var ise = new IsExpression() { Location = t.Location };
					Expect(DTokens.OpenParenthesis);

					if (laKind == DTokens.This && Lexer.CurrentPeekToken.Kind != DTokens.Dot)
					{
						Step();
						ise.TestedType = new DTokenDeclaration(DTokens.This) { Location = t.Location, EndLocation = t.EndLocation };
					}
					else
						ise.TestedType = parserParts.declarationParser.Type(Scope);

					if (ise.TestedType == null)
						SynErr(laKind, "In an IsExpression, either a type or an expression is required!");

					if (ise.TestedType != null)
					{
						if (laKind == DTokens.Identifier && (Lexer.CurrentPeekToken.Kind == DTokens.CloseParenthesis
							|| Lexer.CurrentPeekToken.Kind == DTokens.Equal
							|| Lexer.CurrentPeekToken.Kind == DTokens.Colon))
						{
							Step();
							Strings.Add(strVal);
							ise.TypeAliasIdentifierHash = strVal.GetHashCode();
							ise.TypeAliasIdLocation = t.Location;
						}
						else if (IsEOF)
							ise.TypeAliasIdentifierHash = DTokens.IncompleteIdHash;
					}

					if (laKind == DTokens.Colon || laKind == DTokens.Equal)
					{
						Step();
						ise.EqualityTest = t.Kind == DTokens.Equal;
					}
					else if (laKind == DTokens.CloseParenthesis)
					{
						Step();
						ise.EndLocation = t.EndLocation;
						return ise;
					}

					/*
					TypeSpecialization:
						Type
							struct
							union
							class
							interface
							enum
							function
							delegate
							super
						const
						immutable
						inout
						shared
							return
					*/

					bool specialTest = false;
					if (ise.EqualityTest)
					{
						switch (laKind)
						{
							case DTokens.Typedef: // typedef is possible although it's not yet documented in the syntax docs
							case DTokens.Enum:
							case DTokens.Delegate:
							case DTokens.Function:
							case DTokens.Super:
							case DTokens.Return:
								specialTest = true;
								break;
							case DTokens.Const:
							case DTokens.Immutable:
							case DTokens.InOut:
							case DTokens.Shared:
								specialTest = Peek(1).Kind == DTokens.CloseParenthesis || Lexer.CurrentPeekToken.Kind == DTokens.Comma;
								break;
							default:
								specialTest = DTokensSemanticHelpers.IsClassLike(laKind);
								break;
						}
					}
					if (specialTest)
					{
						Step();
						ise.TypeSpecializationToken = t.Kind;
					}
					else if (IsEOF)
						ise.TypeSpecializationToken = DTokens.Incomplete;
					else
						ise.TypeSpecialization = parserParts.declarationParser.Type(Scope);

					// TemplateParameterList
					if (laKind == DTokens.Comma)
					{
						var tempParam = new List<TemplateParameter>();
						do
						{
							Step();
							tempParam.Add(parserParts.templatesParser.TemplateParameter(Scope as DNode));
						}
						while (laKind == DTokens.Comma);
						ise.TemplateParameterList = tempParam.ToArray();
					}

					Expect(DTokens.CloseParenthesis);
					ise.EndLocation = t.EndLocation;
					return ise;
				default:
					if (DTokensSemanticHelpers.IsMetaIdentifier(laKind))
						goto case DTokens.Dollar;
					else if (parserParts.declarationParser.IsBasicType())
					{
						startLoc = la.Location;

						var bt = parserParts.declarationParser.BasicType(Scope);

						switch (laKind)
						{
							case DTokens.Dot: // BasicType . Identifier
								Step();
								// Things like incomplete 'float.' expressions shall be parseable, too
								if (Expect(DTokens.Identifier) || IsEOF)
									return new PostfixExpression_Access()
									{
										PostfixForeExpression = TypeDeclarationExpression.TryWrap(bt),
										AccessExpression = IsEOF ? new TokenExpression(DTokens.Incomplete) as IExpression
										: new IdentifierExpression(t.Value) { Location = t.Location, EndLocation = t.EndLocation },
										EndLocation = t.EndLocation
									};
								break;
							case DTokens.OpenParenthesis:
								Step();

								var callExp = new PostfixExpression_MethodCall { PostfixForeExpression = TypeDeclarationExpression.TryWrap(bt) };
								callExp.Arguments = ArgumentList(Scope).ToArray();

								Expect(DTokens.CloseParenthesis);
								return callExp;
							default:
								if (bt is TypeOfDeclaration || bt is MemberFunctionAttributeDecl)
									return TypeDeclarationExpression.TryWrap(bt);
								break;
						}

						return null;
					}

					SynErr(DTokens.Identifier);
					if (laKind != DTokens.CloseCurlyBrace)
						Step();

					if (IsEOF)
						return new TokenExpression(DTokens.Incomplete) { Location = t.Location, EndLocation = t.Location };

					// Don't know why, in rare situations, t tends to be null..
					if (t == null)
						return null;
					return new TokenExpression() { Location = t.Location, EndLocation = t.EndLocation };
			}
		}

		public IExpression ArrayLiteral(IBlockNode scope, bool nonInitializer = true)
		{
			Expect(DTokens.OpenSquareBracket);
			var startLoc = t.Location;

			// Empty array literal
			if (laKind == DTokens.CloseSquareBracket)
			{
				Step();
				return new ArrayLiteralExpression(Enumerable.Empty<IExpression>()) { Location = startLoc, EndLocation = t.EndLocation };
			}

			var firstExpression = ParseExpressionOrNonVoidInitializer(scope, nonInitializer);

			// Associtative array
			if (laKind == DTokens.Colon)
			{
				Step();

				var ae = nonInitializer ? new AssocArrayExpression { Location = startLoc } : new ArrayInitializer { Location = startLoc };

				var firstValueExpression = ParseExpressionOrNonVoidInitializer(scope, nonInitializer);

				ae.Elements.Add(new KeyValuePair<IExpression, IExpression>(firstExpression, firstValueExpression));

				while (laKind == DTokens.Comma)
				{
					Step();

					if (laKind == DTokens.CloseSquareBracket)
						break;

					var keyExpr = ParseExpressionOrNonVoidInitializer(scope, nonInitializer);
					IExpression valExpr;
					if (laKind == DTokens.Colon)
					{ // http://dlang.org/expression.html#AssocArrayLiteral Spec failure
						Step();
						valExpr = ParseExpressionOrNonVoidInitializer(scope, nonInitializer);
					}
					else
					{
						valExpr = keyExpr;
						keyExpr = null; // Key will be deduced by incrementing the first key value ever given in the literal
					}

					ae.Elements.Add(new KeyValuePair<IExpression, IExpression>(keyExpr, valExpr));
				}

				Expect(DTokens.CloseSquareBracket);
				ae.EndLocation = t.EndLocation;
				return ae;
			}
			else // Normal array literal
			{
				var ae = new List<IExpression>();
				if (firstExpression != null)
					ae.Add(firstExpression);

				while (laKind == DTokens.Comma)
				{
					Step();
					if (laKind == DTokens.CloseSquareBracket) // And again, empty expressions are allowed
						break;
					ae.Add(ParseExpressionOrNonVoidInitializer(scope, nonInitializer));
				}

				Expect(DTokens.CloseSquareBracket);
				return new ArrayLiteralExpression(ae) { Location = startLoc, EndLocation = t.EndLocation };
			}
		}

		IExpression ParseExpressionOrNonVoidInitializer(IBlockNode scope, bool nonInitializer)
		{
			return nonInitializer ? AssignExpression(scope) : parserParts.declarationParser.NonVoidInitializer(scope);
		}

		bool IsLambaExpression()
		{
			Lexer.StartPeek();

			if (laKind == DTokens.Function || laKind == DTokens.Delegate)
				Lexer.Peek();

			if (Lexer.CurrentPeekToken.Kind != DTokens.OpenParenthesis)
			{
				if (Lexer.CurrentPeekToken.Kind == DTokens.Identifier && Peek().Kind == DTokens.GoesTo)
					return true;

				return false;
			}

			OverPeekBrackets(DTokens.OpenParenthesis, false);

			var k = Lexer.CurrentPeekToken.Kind;
			// (string |
			// (const |
			// (string a, |
			// (char[] |
			// (char a |
			// NOT (char* |
			if (k == DTokens.__EOF__ || k == DTokens.EOF)
			{
				var pk = Lexer.CurrentPeekToken;
				var next = la;
				while (pk != next.next)
					next = next.next;

				k = next.Kind;
				return k == DTokens.Comma || k == DTokens.Identifier || k == DTokens.CloseSquareBracket || DTokensSemanticHelpers.IsBasicType(k) || DTokensSemanticHelpers.IsStorageClass(k);
			}

			// (...) => | 
			// (...) pure @nothrow => |
			return k == DTokens.GoesTo || DTokensSemanticHelpers.IsFunctionAttribute(k);
		}

		bool IsFunctionLiteral()
		{
			if (laKind != DTokens.OpenParenthesis)
				return false;

			Lexer.StartPeek();

			OverPeekBrackets(DTokens.OpenParenthesis, false);

			bool at = false;
			while (DTokensSemanticHelpers.IsStorageClass(Lexer.CurrentPeekToken.Kind) || (at = Lexer.CurrentPeekToken.Kind == DTokens.At))
			{
				Lexer.Peek();
				if (at)
					Lexer.Peek();
				if (Lexer.CurrentPeekToken.Kind == DTokens.OpenParenthesis)
					OverPeekBrackets(DTokens.OpenParenthesis, false);
			}

			return Lexer.CurrentPeekToken.Kind == DTokens.OpenCurlyBrace;
		}

		FunctionLiteral LambaExpression(IBlockNode Scope = null)
		{
			var fl = new FunctionLiteral(true);

			fl.Location = fl.AnonymousMethod.Location = la.Location;

			if (laKind == DTokens.Function || laKind == DTokens.Delegate)
			{
				fl.LiteralToken = laKind;
				Step();
			}

			if (laKind == DTokens.Identifier)
			{
				Step();

				var p = new DVariable
				{
					Name = t.Value,
					Location = t.Location,
					EndLocation = t.EndLocation,
					Attributes = new List<DAttribute> { new Modifier(DTokens.Auto) }
				};

				fl.AnonymousMethod.Parameters.Add(p);
			}
			else if (laKind == DTokens.OpenParenthesis)
				parserParts.declarationParser.Parameters(fl.AnonymousMethod);

			LambdaBody(fl.AnonymousMethod);
			fl.EndLocation = fl.AnonymousMethod.EndLocation;

			if (Scope != null && !AllowWeakTypeParsing)
				Scope.Add(fl.AnonymousMethod);

			return fl;
		}

		public void LambdaBody(DMethod anonymousMethod)
		{
			parserParts.attributesParser.FunctionAttributes(anonymousMethod);

			if (laKind == DTokens.OpenCurlyBrace)
			{
				anonymousMethod.Body = parserParts.statementParser.BlockStatement(anonymousMethod);
				anonymousMethod.EndLocation = anonymousMethod.Body.EndLocation;
			}
			else if (Expect(DTokens.GoesTo))
			{
				anonymousMethod.Body = new BlockStatement { Location = t.EndLocation, ParentNode = anonymousMethod };

				var ae = AssignExpression(anonymousMethod);

				var endLocation = IsEOF ? CodeLocation.Empty : t.EndLocation;

				anonymousMethod.Body.Add(new ReturnStatement
				{
					Location = ae.Location,
					EndLocation = endLocation,
					ReturnExpression = ae
				});

				anonymousMethod.Body.EndLocation = endLocation;
				anonymousMethod.EndLocation = anonymousMethod.Body.EndLocation;
			}
			else // (string | -- see IsLambdaExpression()
				anonymousMethod.EndLocation = la.Location;
		}

		IExpression TraitsExpression(IBlockNode scope)
		{
			Expect(DTokens.__traits);
			var ce = new TraitsExpression() { Location = t.Location };
			if (Expect(DTokens.OpenParenthesis))
			{
				if (Expect(DTokens.Identifier))
					ce.Keyword = t.Value;
				else if (IsEOF)
					ce.Keyword = DTokens.IncompleteId;

				var al = new List<TraitsArgument>();

				var weakTypeParsingBackup = AllowWeakTypeParsing;

				while (laKind == DTokens.Comma)
				{
					Step();

					Lexer.PushLookAheadBackup();

					AllowWeakTypeParsing = true;
					var td = parserParts.declarationParser.Type(scope);
					AllowWeakTypeParsing = false;

					if (td != null && (laKind == DTokens.Comma || laKind == DTokens.CloseParenthesis || IsEOF))
					{
						Lexer.PopLookAheadBackup();
						al.Add(new TraitsArgument(td));
						continue;
					}

					Lexer.RestoreLookAheadBackup();

					al.Add(new TraitsArgument(AssignExpression(scope)));
				}

				AllowWeakTypeParsing = weakTypeParsingBackup;

				Expect(DTokens.CloseParenthesis);

				if (al.Count != 0)
					ce.Arguments = al.ToArray();
			}
			ce.EndLocation = t.EndLocation;
			return ce;
		}
	}
}
