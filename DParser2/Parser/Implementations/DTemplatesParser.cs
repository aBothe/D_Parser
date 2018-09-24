using System.Collections.Generic;

using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;

namespace D_Parser.Parser.Implementations
{
	class DTemplatesParser : DParserImplementationPart
	{
		readonly DParserParts parserParts;

		public DTemplatesParser(DParserStateContext stateContext, DParserParts parserParts)
			: base(stateContext) { this.parserParts = parserParts; }

		/*
		 * American beer is like sex on a boat - Fucking close to water;)
		 */

		public INode TemplateDeclaration(INode Parent)
		{
			var startLoc = la.Location;

			// TemplateMixinDeclaration
			Modifier mixinMod;
			if (laKind == DTokens.Mixin)
			{
				Step();
				mixinMod = new Modifier(DTokens.Mixin) { Location = t.Location, EndLocation = t.EndLocation };
			}
			else
				mixinMod = null;

			Expect(DTokens.Template);
			var dc = new DClassLike(DTokens.Template)
			{
				Description = GetComments(),
				Location = startLoc,
				Parent = Parent
			};

			parserParts.declarationParser.ApplyAttributes(dc);

			if (mixinMod != null)
				dc.Attributes.Add(mixinMod);

			if (Expect(DTokens.Identifier))
			{
				dc.Name = t.Value;
				dc.NameLocation = t.Location;
			}
			else if (IsEOF)
				dc.NameHash = DTokens.IncompleteIdHash;

			TemplateParameterList(dc);

			if (laKind == (DTokens.If))
				parserParts.bodiedSymbolsParser.Constraint(dc);

			// [Must not contain a base class list]

			parserParts.bodiedSymbolsParser.ClassBody(dc);

			return dc;
		}

		public TemplateMixin TemplateMixin(INode Scope, IStatement Parent = null)
		{
			// mixin TemplateIdentifier !( TemplateArgumentList ) MixinIdentifier ;
			//							|<--			optional			 -->|
			var r = new TemplateMixin { Attributes = parserParts.declarationParser.GetCurrentAttributeSet_Array() };
			if (Parent == null)
				r.ParentNode = Scope;
			else
				r.Parent = Parent;
			ITypeDeclaration preQualifier = null;

			Expect(DTokens.Mixin);
			r.Location = t.Location;

			bool modScope = false;
			if (laKind == DTokens.Dot)
			{
				modScope = true;
				Step();
			}
			else if (laKind != DTokens.Identifier)
			{// See Dsymbol *Parser::parseMixin()
				if (laKind == DTokens.Typeof)
				{
					preQualifier = parserParts.declarationParser.TypeOf(Scope as IBlockNode);
				}
				else if (laKind == DTokens.__vector)
				{
					//TODO: Parse vectors(?)
				}

				Expect(DTokens.Dot);
			}

			r.Qualifier = parserParts.declarationParser.IdentifierList(Scope as IBlockNode);
			if (r.Qualifier != null)
				r.Qualifier.InnerMost.InnerDeclaration = preQualifier;
			else
				r.Qualifier = preQualifier;

			if (modScope)
			{
				var innerMost = r.Qualifier.InnerMost;
				if (innerMost is IntermediateIdType)
					(innerMost as IntermediateIdType).ModuleScoped = true;
			}

			// MixinIdentifier
			if (laKind == DTokens.Identifier)
			{
				Step();
				r.IdLocation = t.Location;
				r.MixinId = t.Value;
			}
			else if (r.Qualifier != null && IsEOF)
				r.MixinId = DTokens.IncompleteId;

			Expect(DTokens.Semicolon);
			r.EndLocation = t.EndLocation;

			return r;
		}

		/// <summary>
		/// Be a bit lazy here with checking whether there're templates or not
		/// </summary>
		public bool IsTemplateParameterList()
		{
			Lexer.StartPeek();
			var pk = la;
			int r = 0;
			while (r >= 0 && pk.Kind != DTokens.EOF && pk.Kind != DTokens.__EOF__)
			{
				if (pk.Kind == DTokens.OpenParenthesis)
					r++;
				else if (pk.Kind == DTokens.CloseParenthesis)
				{
					r--;
					if (r <= 0)
						return Peek().Kind == DTokens.OpenParenthesis;
				}
				pk = Peek();
			}
			return false;
		}

		public void TemplateParameterList(DNode dn)
		{
			if (!Expect(DTokens.OpenParenthesis))
			{
				SynErr(DTokens.OpenParenthesis, "Template parameter list expected");
				dn.TemplateParameters = new TemplateParameter[0];
				return;
			}

			if (laKind == (DTokens.CloseParenthesis))
			{
				Step();
				return;
			}

			var ret = new List<TemplateParameter>();

			bool init = true;
			while (init || laKind == (DTokens.Comma))
			{
				if (init) init = false;
				else Step();

				if (laKind == DTokens.CloseParenthesis)
					break;

				ret.Add(TemplateParameter(dn));
			}

			Expect(DTokens.CloseParenthesis);

			dn.TemplateParameters = ret.ToArray();
		}

		public TemplateParameter TemplateParameter(DNode parent)
		{
			IBlockNode scope = parent as IBlockNode;
			CodeLocation startLoc;

			// TemplateThisParameter
			if (laKind == (DTokens.This))
			{
				Step();

				startLoc = t.Location;
				var end = t.EndLocation;

				return new TemplateThisParameter(TemplateParameter(parent), parent) { Location = startLoc, EndLocation = end };
			}

			// TemplateTupleParameter
			else if (laKind == (DTokens.Identifier) && Lexer.CurrentPeekToken.Kind == DTokens.TripleDot)
			{
				Step();
				startLoc = t.Location;
				var id = t.Value;
				Step();

				return new TemplateTupleParameter(id, startLoc, parent) { Location = startLoc, EndLocation = t.EndLocation };
			}

			// TemplateAliasParameter
			else if (laKind == (DTokens.Alias))
			{
				Step();

				startLoc = t.Location;
				TemplateAliasParameter al;
				ITypeDeclaration bt;

				if (IsEOF)
					al = new TemplateAliasParameter(DTokens.IncompleteIdHash, CodeLocation.Empty, parent);
				else
				{
					bt = parserParts.declarationParser.BasicType(scope);
					parserParts.declarationParser.ParseBasicType2(ref bt, scope);

					if (laKind == DTokens.Identifier)
					{
						// alias BasicType Declarator TemplateAliasParameterSpecialization_opt TemplateAliasParameterDefault_opt
						var nn = parserParts.declarationParser.Declarator(bt, false, parent);
						al = new TemplateAliasParameter(nn.NameHash, nn.NameLocation, parent);
						al.Type = nn.Type;
						//TODO: Assign other parts of the declarator? Parameters and such?
					}
					else if (bt is IdentifierDeclaration)
						al = new TemplateAliasParameter((bt as IdentifierDeclaration).IdHash, bt.Location, parent);
					else
						al = new TemplateAliasParameter(0, CodeLocation.Empty, parent);
				}
				al.Location = startLoc;

				// TemplateAliasParameterSpecialization
				if (laKind == (DTokens.Colon))
				{
					Step();

					AllowWeakTypeParsing = true;
					al.SpecializationType = parserParts.declarationParser.Type(scope);
					AllowWeakTypeParsing = false;

					if (al.SpecializationType == null)
						al.SpecializationExpression = parserParts.expressionsParser.ConditionalExpression(scope);
				}

				// TemplateAliasParameterDefault
				if (laKind == (DTokens.Assign))
				{
					Step();

					if (parserParts.expressionsParser.IsAssignExpression())
						al.DefaultExpression = parserParts.expressionsParser.ConditionalExpression(scope);
					else
						al.DefaultType = parserParts.declarationParser.Type(scope);
				}
				al.EndLocation = t.EndLocation;
				return al;
			}

			// TemplateTypeParameter
			else if (laKind == (DTokens.Identifier) && (
				Lexer.CurrentPeekToken.Kind == (DTokens.Colon)
				|| Lexer.CurrentPeekToken.Kind == (DTokens.Assign)
				|| Lexer.CurrentPeekToken.Kind == (DTokens.Comma)
				|| Lexer.CurrentPeekToken.Kind == (DTokens.CloseParenthesis)))
			{
				Expect(DTokens.Identifier);
				var tt = new TemplateTypeParameter(t.Value, t.Location, parent) { Location = t.Location };

				if (laKind == DTokens.Colon)
				{
					Step();
					tt.Specialization = parserParts.declarationParser.Type(scope);
				}

				if (laKind == DTokens.Assign)
				{
					Step();
					tt.Default = parserParts.declarationParser.Type(scope);
				}
				tt.EndLocation = t.EndLocation;
				return tt;
			}

			// TemplateValueParameter
			startLoc = la.Location;
			var dv = parserParts.declarationParser.Declarator(parserParts.declarationParser.BasicType(scope), false, null);

			if (dv == null)
			{
				SynErr(t.Kind, "Declarator expected for parsing template parameter");
				return new TemplateTypeParameter(DTokens.IncompleteIdHash, t.Location, parent) { Location = t.Location };
			}

			var tv = new TemplateValueParameter(dv.NameHash, dv.NameLocation, parent)
			{
				Location = startLoc,
				Type = dv.Type
			};

			if (laKind == (DTokens.Colon))
			{
				Step();
				tv.SpecializationExpression = parserParts.expressionsParser.ConditionalExpression(scope);
			}

			if (laKind == (DTokens.Assign))
			{
				Step();
				tv.DefaultExpression = parserParts.expressionsParser.AssignExpression(scope);
			}
			tv.EndLocation = t.EndLocation;
			return tv;
		}

		public TemplateInstanceExpression TemplateInstance(IBlockNode Scope)
		{
			var loc = la.Location;

			var mod = DTokens.INVALID;

			if (DTokensSemanticHelpers.IsStorageClass(laKind))
			{
				mod = laKind;
				Step();
			}

			if (!Expect(DTokens.Identifier))
				return null;

			ITypeDeclaration td = new IdentifierDeclaration(t.Value)
			{
				Location = t.Location,
				EndLocation = t.EndLocation
			};

			td = new TemplateInstanceExpression(mod != DTokens.INVALID ? new MemberFunctionAttributeDecl(mod) { InnerType = td } : td)
			{
				Location = loc
			};

			var args = new List<IExpression>();

			if (!Expect(DTokens.Not))
				return td as TemplateInstanceExpression;

			if (laKind == (DTokens.OpenParenthesis))
			{
				Step();

				if (laKind != DTokens.CloseParenthesis)
				{
					bool init = true;
					while (laKind == DTokens.Comma || init)
					{
						if (!init) Step();
						init = false;

						if (laKind == DTokens.CloseParenthesis)
							break;

						Lexer.PushLookAheadBackup();

						bool wp = AllowWeakTypeParsing;
						AllowWeakTypeParsing = true;

						var typeArg = parserParts.declarationParser.Type(Scope);

						AllowWeakTypeParsing = wp;

						if (typeArg != null && (laKind == DTokens.CloseParenthesis || laKind == DTokens.Comma))
						{
							Lexer.PopLookAheadBackup();
							args.Add(TypeDeclarationExpression.TryWrap(typeArg));
						}
						else
						{
							Lexer.RestoreLookAheadBackup();
							var ex = parserParts.expressionsParser.AssignExpression(Scope);
							if (ex != null)
								args.Add(ex);
						}
					}
				}
				Expect(DTokens.CloseParenthesis);
			}
			else
			{
				/*
				 * TemplateSingleArgument: 
				 *		Identifier 
				 *		BasicTypeX 
				 *		CharacterLiteral 
				 *		StringLiteral 
				 *		IntegerLiteral 
				 *		FloatLiteral 
				 *		true 
				 *		false 
				 *		null 
				 *		this
				*		__FILE__
				*		__MODULE__
				*		__LINE__
				*		__FUNCTION__
				*		__PRETTY_FUNCTION__
				 */

				switch (laKind)
				{
					case DTokens.Literal:
					case DTokens.True:
					case DTokens.False:
					case DTokens.Null:
					case DTokens.This:
					case DTokens.__FILE__:
					case DTokens.__MODULE__:
					case DTokens.__LINE__:
					case DTokens.__FUNCTION__:
					case DTokens.__PRETTY_FUNCTION__:
						args.Add(parserParts.expressionsParser.PrimaryExpression(Scope));
						break;
					case DTokens.Identifier:
						Step();
						args.Add(new IdentifierExpression(t.Value)
						{
							Location = t.Location,
							EndLocation = t.EndLocation
						});
						break;
					default:
						if (DTokensSemanticHelpers.IsBasicType(laKind))
						{
							Step();
							args.Add(TypeDeclarationExpression.TryWrap(new DTokenDeclaration(t.Kind)
							{
								Location = t.Location,
								EndLocation = t.EndLocation
							}));
							break;
						}
						else if (IsEOF)
							goto case DTokens.Literal;
						SynErr(laKind, "Illegal token found on template instance expression argument");
						Step();
						break;
				}

				if (laKind == DTokens.Not && Peek(1).Kind != DTokens.Is && Peek(1).Kind != DTokens.In)
				{
					SynErr(laKind, "multiple ! arguments are not allowed");
					Step();
				}
			}
			(td as TemplateInstanceExpression).Arguments = args.ToArray();
			td.EndLocation = t.EndLocation;
			return td as TemplateInstanceExpression;
		}
	}
}
