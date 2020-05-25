using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;

namespace D_Parser.Parser.Implementations
{
	class DDeclarationParser : DParserImplementationPart
	{
		readonly DParserParts parserParts;

		public DDeclarationParser(DParserStateContext stateContext, DParserParts parserParts)
			: base(stateContext) { this.parserParts = parserParts; }

		#region Declarations
		// http://www.digitalmars.com/d/2.0/declaration.html

		public bool CheckForStorageClasses(IBlockNode scope)
		{
			bool ret = false;
			while (LookAheadIsStorageClass)
			{
				if (parserParts.attributesParser.IsAttributeSpecifier) // extern, align
					parserParts.attributesParser.AttributeSpecifier(scope);
				else
				{
					Step();
					// Always allow more than only one property DAttribute
					if (!Modifier.ContainsAnyAttributeToken(DeclarationAttributes.ToArray(), t.Kind))
						PushAttribute(new Modifier(t.Kind, t.Value) { Location = t.Location, EndLocation = t.EndLocation }, false);
				}
				ret = true;
			}
			return ret;
		}

		public IEnumerable<INode> Declaration(IBlockNode Scope)
		{
			CheckForStorageClasses(Scope);

			switch (laKind)
			{
				case DTokens.Alias:
				case DTokens.Typedef:
					foreach (var e in AliasDeclaration(Scope))
						yield return e;
					break;
				case DTokens.Struct:
				case DTokens.Union:
					yield return parserParts.bodiedSymbolsParser.AggregateDeclaration(Scope);
					break;
				case DTokens.Enum:
					Step();

					switch (laKind)
					{
						case DTokens.Identifier:
							switch (Lexer.CurrentPeekToken.Kind)
							{
								case DTokens.__EOF__:
								case DTokens.EOF:
								case DTokens.Semicolon: // enum E;
								case DTokens.Colon: // enum E : int {...}
								case DTokens.OpenCurlyBrace: // enum E {...}
									yield return parserParts.bodiedSymbolsParser.EnumDeclaration(Scope);
									yield break;
							}
							break;

						case DTokens.__EOF__:
						case DTokens.EOF:

						case DTokens.Semicolon: // enum;
						case DTokens.Colon: // enum : int {...}
						case DTokens.OpenCurlyBrace: // enum {...}
							yield return parserParts.bodiedSymbolsParser.EnumDeclaration(Scope);
							yield break;
					}

					var enumAttr = new Modifier(DTokens.Enum) { Location = t.Location, EndLocation = t.EndLocation };
					PushAttribute(enumAttr, false);
					foreach (var i in Decl(Scope, enumAttr))
						yield return i;
					break;
				case DTokens.Class:
					yield return parserParts.bodiedSymbolsParser.ClassDeclaration(Scope);
					break;
				case DTokens.Template:
					yield return parserParts.templatesParser.TemplateDeclaration(Scope);
					break;
				case DTokens.Mixin:
					if (Peek(1).Kind == DTokens.Template)
						goto case DTokens.Template;
					goto default;
				case DTokens.Interface:
					yield return parserParts.bodiedSymbolsParser.InterfaceDeclaration(Scope);
					break;
				case DTokens.Ref:
					foreach (var i in Decl(Scope))
						yield return i;
					break;
				default:
					if (IsBasicType())
						goto case DTokens.Ref;
					else if (IsEOF)
					{
						if (CheckForStorageClasses(Scope))
							goto case DTokens.Ref;
						foreach (var i in Decl(Scope))
						{
							// If we're at EOF, there should only be exactly 1 node returned
							i.NameHash = 0;
							yield return i;
						}
						break;
					}
					SynErr(laKind, "Declaration expected, not " + DTokens.GetTokenString(laKind));
					Step();
					break;
			}
		}

		IEnumerable<INode> AliasDeclaration(IBlockNode Scope)
		{
			Step();
			// _t is just a synthetic node which holds possible following attributes
			var _t = new DVariable();
			ApplyAttributes(_t);
			_t.Description = GetComments();

			// AliasThis
			if ((laKind == DTokens.Identifier && Lexer.CurrentPeekToken.Kind == DTokens.This) ||
				(laKind == DTokens.This && Lexer.CurrentPeekToken.Kind == DTokens.Assign))
			{
				yield return AliasThisDeclaration(_t, Scope);
				yield break;
			}

			// AliasInitializerList
			else if (laKind == DTokens.Identifier && (Lexer.CurrentPeekToken.Kind == DTokens.Assign ||
				(Lexer.CurrentPeekToken.Kind == DTokens.OpenParenthesis && OverPeekBrackets(DTokens.OpenParenthesis) && Lexer.CurrentPeekToken.Kind == DTokens.Assign)))
			{
				DVariable dv = null;
				do
				{
					if (laKind == DTokens.Comma)
						Step();
					if (!Expect(DTokens.Identifier))
						break;
					dv = new DVariable
					{
						IsAlias = true,
						Attributes = _t.Attributes,
						Description = _t.Description,
						Name = t.Value,
						NameLocation = t.Location,
						Location = t.Location,
						Parent = Scope
					};

					if (laKind == DTokens.OpenParenthesis)
					{
						var ep = new EponymousTemplate();
						ep.AssignFrom(dv);
						dv = ep;
						parserParts.templatesParser.TemplateParameterList(ep);
					}

					if (Expect(DTokens.Assign))
					{
						// alias fnRtlAllocateHeap = extern(Windows) void* function(void* HeapHandle, uint Flags, size_t Size) nothrow;
						CheckForStorageClasses(Scope);
						ApplyAttributes(dv);

						Lexer.PushLookAheadBackup();
						var wkTypeParsingBackup = AllowWeakTypeParsing;
						AllowWeakTypeParsing = true;
						AssignOrWrapTypeToNode(dv, Type(Scope));
						AllowWeakTypeParsing = wkTypeParsingBackup;
						if (!(laKind == DTokens.Comma || laKind == DTokens.Semicolon))
						{
							Lexer.RestoreLookAheadBackup();
							dv.Initializer = parserParts.expressionsParser.AssignExpression(Scope);
						}
						else
							Lexer.PopLookAheadBackup();
					}
					yield return dv;
				}
				while (laKind == DTokens.Comma);

				Expect(DTokens.Semicolon);
				if (dv != null)
					dv.Description += CheckForPostSemicolonComment();
				yield break;
			}

			// alias BasicType Declarator
			foreach (var n in Decl(Scope, laKind != DTokens.Identifier || Lexer.CurrentPeekToken.Kind != DTokens.OpenParenthesis ? null : new Modifier(DTokens.Alias), true))
			{
				var dv = n as DVariable;
				if (dv != null)
				{
					if (n.NameHash == DTokens.IncompleteIdHash && n.Type == null) // 'alias |' shall trigger completion, 'alias int |' not
						n.NameHash = 0;
					dv.Attributes.AddRange(_t.Attributes);
					dv.IsAlias = true;
				}
				yield return n;
			}
		}

		DVariable AliasThisDeclaration(DVariable initiallyParsedNode, IBlockNode Scope)
		{
			var dv = new DVariable
			{
				Description = initiallyParsedNode.Description,
				Location = t.Location,
				IsAlias = true,
				IsAliasThis = true,
				NameHash = DVariable.AliasThisIdentifierHash,
				Parent = Scope,
				Attributes = initiallyParsedNode.Attributes
			};

			if (!(Scope is DClassLike))
				SemErr(DTokens.This, "alias this declarations are only allowed in structs and classes!");

			// alias this = Identifier
			if (laKind == DTokens.This && Lexer.CurrentPeekToken.Kind == DTokens.Assign)
			{
				Step(); // Step beyond 'this'
				dv.NameLocation = t.Location;
				Step(); // Step beyond '='
				if (Expect(DTokens.Identifier))
				{
					AssignOrWrapTypeToNode(dv, new IdentifierDeclaration(t.Value)
					{
						Location = t.Location,
						EndLocation = t.EndLocation
					});
				}
			}
			else
			{
				Step(); // Step beyond Identifier
				AssignOrWrapTypeToNode(dv, new IdentifierDeclaration(t.Value)
				{
					Location = dv.NameLocation = t.Location,
					EndLocation = t.EndLocation
				});

				Step(); // Step beyond 'this'
				dv.NameLocation = t.Location;
			}

			dv.EndLocation = t.EndLocation;

			Expect(DTokens.Semicolon);
			dv.Description += CheckForPostSemicolonComment();
			return dv;
		}

		IEnumerable<INode> Decl(IBlockNode Scope, DAttribute StorageClass = null, bool isAlias = false)
		{
			var startLocation = la.Location;
			var initialComment = GetComments();
			ITypeDeclaration ttd = null;

			CheckForStorageClasses(Scope);

			// Autodeclaration
			if (StorageClass == null)
				StorageClass = DTokensSemanticHelpers.ContainsStorageClass(DeclarationAttributes);

			if (laKind == DTokens.Enum)
			{
				Step();
				PushAttribute(StorageClass = new Modifier(DTokens.Enum) { Location = t.Location, EndLocation = t.EndLocation }, false);
			}

			// If there's no explicit type declaration, leave our node's type empty!
			if ((StorageClass != Modifier.Empty &&
				laKind == DTokens.Identifier && (DeclarationAttributes.Count > 0 || Lexer.CurrentPeekToken.Kind == DTokens.OpenParenthesis)))
			{ // public auto var=0; // const foo(...) {} 
				if (Lexer.CurrentPeekToken.Kind == DTokens.Assign || Lexer.CurrentPeekToken.Kind == DTokens.OpenParenthesis)
				{
				}
				else if (Lexer.CurrentPeekToken.Kind == DTokens.Semicolon)
				{
					SemErr(t.Kind, "Initializer expected for auto type, semicolon found!");
				}
				else
					ttd = BasicType(Scope);
			}
			else if (!IsEOF)
			{
				// standalone this/super only allowed in alias declarations
				if (isAlias && (laKind == DTokens.This || laKind == DTokens.Super) && Lexer.CurrentPeekToken.Kind != DTokens.Dot)
				{
					ttd = new DTokenDeclaration(laKind) { Location = la.Location, EndLocation = la.EndLocation };
					Step();
				}
				else
					ttd = BasicType(Scope);
			}


			if (IsEOF)
			{
				/*
				 * T! -- tix.Arguments == null
				 * T!(int, -- last argument == null
				 * T!(int, bool, -- ditto
				 * T!(int) -- now every argument is complete
				 */
				var tix = ttd as TemplateInstanceExpression;
				if (tix != null)
				{
					if (tix.Arguments == null || tix.Arguments.Length == 0 ||
						(tix.Arguments[tix.Arguments.Length - 1] is TokenExpression &&
						(tix.Arguments[tix.Arguments.Length - 1] as TokenExpression).Token == DTokens.INVALID))
					{
						yield break;
					}
				}
				else if (ttd is MemberFunctionAttributeDecl && (ttd as MemberFunctionAttributeDecl).InnerType == null)
				{
					yield break;
				}
			}

			// Declarators
			var firstNode = Declarator(ttd, false, Scope);
			if (firstNode == null)
				yield break;
			firstNode.Description = initialComment;
			firstNode.Location = startLocation;

			// Check for declaration constraints
			if (laKind == (DTokens.If))
				parserParts.bodiedSymbolsParser.Constraint(firstNode);

			// BasicType Declarators ;
			if (laKind == DTokens.Assign || laKind == DTokens.Comma || laKind == DTokens.Semicolon)
			{
				// DeclaratorInitializer
				if (laKind == DTokens.Assign)
				{
					var init = Initializer(Scope);
					var dv = firstNode as DVariable;
					if (dv != null)
						dv.Initializer = init;
				}
				firstNode.EndLocation = t.EndLocation;
				yield return firstNode;

				// DeclaratorIdentifierList
				var otherNode = firstNode;
				while (laKind == DTokens.Comma)
				{
					Step();
					if (IsEOF || Expect(DTokens.Identifier))
					{
						otherNode = new DVariable();

						// Note: In DDoc, all declarations that are made at once (e.g. int a,b,c;) get the same pre-declaration-description!
						otherNode.Description = initialComment;

						otherNode.AssignFrom(firstNode);
						otherNode.Location = t.Location;
						if (t.Kind == DTokens.Identifier)
							otherNode.Name = t.Value;
						else if (IsEOF)
							otherNode.NameHash = DTokens.IncompleteIdHash;
						otherNode.NameLocation = t.Location;

						if (laKind == DTokens.OpenParenthesis)
							parserParts.templatesParser.TemplateParameterList(otherNode);

						if (laKind == DTokens.Assign)
							(otherNode as DVariable).Initializer = Initializer(Scope);

						otherNode.EndLocation = t.EndLocation;
						yield return otherNode;
					}
					else
						break;
				}

				Expect(DTokens.Semicolon);

				// Note: In DDoc, only the really last declaration will get the post semicolon comment appended
				otherNode.Description += CheckForPostSemicolonComment();

				yield break;
			}

			// BasicType Declarator FunctionBody
			else if (firstNode is DMethod && (parserParts.bodiedSymbolsParser.IsFunctionBody || IsEOF))
			{
				firstNode.Description += CheckForPostSemicolonComment();

				parserParts.bodiedSymbolsParser.FunctionBody((DMethod)firstNode);

				firstNode.Description += CheckForPostSemicolonComment();

				yield return firstNode;
				yield break;
			}
			else
				SynErr(DTokens.OpenCurlyBrace, "; or function body expected after declaration stub.");

			if (IsEOF)
				yield return firstNode;
		}

		public bool IsBasicType()
		{
			return DTokensSemanticHelpers.IsBasicType(la);
		}

		public ITypeDeclaration BasicType(IBlockNode scope)
		{
			bool isModuleScoped = laKind == DTokens.Dot;
			if (isModuleScoped)
				Step();

			ITypeDeclaration td = null;
			if (DTokensSemanticHelpers.IsBasicType(laKind))
			{
				Step();
				return new DTokenDeclaration(t.Kind) { Location = t.Location, EndLocation = t.EndLocation };
			}

			if (DTokensSemanticHelpers.IsMemberFunctionAttribute(laKind))
			{
				Step();
				var md = new MemberFunctionAttributeDecl(t.Kind) { Location = t.Location };
				bool p = false;

				if (laKind == DTokens.OpenParenthesis)
				{
					Step();
					p = true;

					if (IsEOF)
						return md;
				}

				// e.g. cast(const)
				if (laKind != DTokens.CloseParenthesis)
					md.InnerType = p ? Type(scope) : BasicType(scope);

				if (p)
					Expect(DTokens.CloseParenthesis);
				md.EndLocation = t.EndLocation;
				return md;
			}

			//TODO
			if (laKind == DTokens.Ref)
				Step();

			if (laKind == (DTokens.Typeof))
			{
				td = TypeOf(scope);
				if (laKind != DTokens.Dot)
					return td;
				Step();
			}

			else if (laKind == DTokens.__vector)
			{
				td = Vector(scope);
				if (laKind != DTokens.Dot)
					return td;
				Step();
			}

			else if (laKind == DTokens.__traits)
			{
				var te = parserParts.expressionsParser.TraitsExpression(scope);
				td = new TypeOfDeclaration { Location = t.Location, Expression = te };
				if (laKind != DTokens.Dot)
					return td;
				Step();
			}

			if (AllowWeakTypeParsing && laKind != DTokens.Identifier)
				return null;

			if (td == null)
				td = IdentifierList(scope);
			else
			{
				var td_back = td;
				td = IdentifierList(scope);
				td.InnerMost = td_back;
			}

			if (isModuleScoped && td != null)
			{
				var innerMost = td.InnerMost;
				if (innerMost is IntermediateIdType)
					((IntermediateIdType)innerMost).ModuleScoped = true;
			}

			return td;
		}

		bool IsBasicType2()
		{
			switch (laKind)
			{
				case DTokens.Times:
				case DTokens.OpenSquareBracket:
				case DTokens.Delegate:
				case DTokens.Function:
					return true;
				default:
					return false;
			}
		}

		ITypeDeclaration BasicType2(IBlockNode scope)
		{
			// *
			if (laKind == (DTokens.Times))
			{
				Step();
				return new PointerDecl() { Location = t.Location, EndLocation = t.EndLocation };
			}

			// [ ... ]
			else if (laKind == (DTokens.OpenSquareBracket))
			{
				var startLoc = la.Location;
				Step();
				// [ ]
				if (laKind == (DTokens.CloseSquareBracket))
				{
					Step();
					return new ArrayDecl() { Location = startLoc, EndLocation = t.EndLocation };
				}

				ITypeDeclaration cd = null;

				// [ Type ]
				Lexer.PushLookAheadBackup();
				bool weaktype = AllowWeakTypeParsing;
				AllowWeakTypeParsing = true;

				var keyType = Type(scope);

				AllowWeakTypeParsing = weaktype;

				if (keyType != null && laKind == DTokens.CloseSquareBracket && !(keyType is IdentifierDeclaration))
				{
					//HACK: Both new int[size_t] as well as new int[someConstNumber] are legal. So better treat them as expressions.
					cd = new ArrayDecl() { KeyType = keyType, Location = startLoc };
					Lexer.PopLookAheadBackup();
				}
				else
				{
					Lexer.RestoreLookAheadBackup();

					var fromExpression = parserParts.expressionsParser.AssignExpression(scope);

					// [ AssignExpression .. AssignExpression ]
					if (laKind == DTokens.DoubleDot)
					{
						Step();
						cd = new ArrayDecl()
						{
							Location = startLoc,
							KeyType = null,
							KeyExpression = new PostfixExpression_ArrayAccess(fromExpression, parserParts.expressionsParser.AssignExpression(scope))
						};
					}
					else
						cd = new ArrayDecl() { KeyType = null, KeyExpression = fromExpression, Location = startLoc };
				}

				if ((AllowWeakTypeParsing && laKind != DTokens.CloseSquareBracket))
					return null;

				Expect(DTokens.CloseSquareBracket);
				if (cd != null)
					cd.EndLocation = t.EndLocation;
				return cd;
			}

			// delegate | function
			else if (laKind == (DTokens.Delegate) || laKind == (DTokens.Function))
			{
				Step();
				var dd = new DelegateDeclaration() { Location = t.Location };
				dd.IsFunction = t.Kind == DTokens.Function;

				if (AllowWeakTypeParsing && laKind != DTokens.OpenParenthesis)
					return null;

				var _dm = new DMethod();
				Parameters(_dm);
				dd.Parameters = _dm.Parameters;

				var attributes = new List<DAttribute>();
				parserParts.attributesParser.FunctionAttributes(ref attributes);
				dd.Modifiers = attributes.Count > 0 ? attributes.ToArray() : null;

				dd.EndLocation = t.EndLocation;
				return dd;
			}
			else
				SynErr(DTokens.Identifier);
			return null;
		}

		public void ParseBasicType2(ref ITypeDeclaration td, IBlockNode scope)
		{
			if (td == null)
			{
				if (!IsBasicType2())
					return;

				td = BasicType2(scope);
				if (td == null)
					return;
			}

			while (IsBasicType2())
			{
				var ttd = BasicType2(scope);
				if (ttd != null)
					ttd.InnerDeclaration = td;
				else if (AllowWeakTypeParsing)
				{
					td = null;
					return;
				}
				td = ttd;
			}
		}

		/// <summary>
		/// Parses a type declarator
		/// </summary>
		/// <returns>A dummy node that contains the return type, the variable name and possible parameters of a function declaration</returns>
		public DNode Declarator(ITypeDeclaration basicType, bool IsParam, INode parent)
		{
			DNode ret = new DVariable() { Location = la.Location, Parent = parent };
			ApplyAttributes(ret);

			ParseBasicType2(ref basicType, parent as IBlockNode);
			AssignOrWrapTypeToNode(ret, basicType);

			if (laKind != (DTokens.OpenParenthesis))
			{
				// On external function declarations, no parameter names are required.
				// extern void Cfoo(HANDLE,char**);
				if (IsParam && laKind != (DTokens.Identifier))
				{
					if (IsEOF)
					{
						var tokDecl = ret.Type as DTokenDeclaration;
						var ad = ret.Type as ArrayDecl;
						if ((tokDecl == null || tokDecl.Token != DTokens.Incomplete) && // 'T!|' or similar
							(ad == null || !(ad.KeyExpression is TokenExpression) || (ad.KeyExpression as TokenExpression).Token != DTokens.Incomplete)) // 'string[|'
							ret.NameHash = DTokens.IncompleteIdHash;
					}
					return ret;
				}

				if (Expect(DTokens.Identifier))
				{
					ret.Name = t.Value;
					ret.NameLocation = t.Location;

					// enum asdf(...) = ...;
					if (laKind == DTokens.OpenParenthesis && OverPeekBrackets(DTokens.OpenParenthesis, true) &&
						Lexer.CurrentPeekToken.Kind == DTokens.Assign)
					{
						var eponymousTemplateDecl = new EponymousTemplate();
						eponymousTemplateDecl.AssignFrom(ret);
						ret = eponymousTemplateDecl;

						parserParts.templatesParser.TemplateParameterList(eponymousTemplateDecl);

						return ret;
					}
				}
				else
				{
					if (IsEOF)
					{
						ret.NameHash = DTokens.IncompleteIdHash;
						return ret;
					}

					return null;
					/*
					// Code error! - to prevent infinite declaration loops, step one token forward anyway!
					if(laKind != CloseCurlyBrace && laKind != CloseParenthesis)
						Step();
					return null;
                     */
				}
			}
			else
				OldCStyleFunctionPointer(ret, IsParam);

			if (IsDeclaratorSuffix || parserParts.attributesParser.IsFunctionAttribute)
				DeclaratorSuffixes(ref ret);

			return ret;
		}

		/// <summary>
		/// Add some syntax possibilities here
		/// int (x);
		/// int(*foo);
		/// This way of declaring function pointers is deprecated
		/// </summary>
		void OldCStyleFunctionPointer(DNode ret, bool IsParam)
		{
			Step();
			//SynErr(OpenParenthesis,"C-style function pointers are deprecated. Use the function() syntax instead."); // Only deprecated in D2
			var cd = new DelegateDeclaration() as ITypeDeclaration;
			AssignOrWrapTypeToNode(ret, cd);
			var deleg = cd as DelegateDeclaration;

			/*			 
			 * Parse all basictype2's that are following the initial '('
			 */
			ITypeDeclaration retType = null;
			ParseBasicType2(ref retType, ret.Parent as IBlockNode);
			deleg.ReturnType = retType;

			/*			
			 * Here can be an identifier with some optional DeclaratorSuffixes
			 */
			if (laKind != (DTokens.CloseParenthesis))
			{
				if (IsParam && laKind != (DTokens.Identifier))
				{
					/* If this Declarator is a parameter of a function, don't expect anything here
					 * except a '*' that means that here's an anonymous function pointer
					 */
					if (t.Kind != (DTokens.Times))
						SynErr(DTokens.Times);
				}
				else
				{
					if (Expect(DTokens.Identifier))
						ret.Name = t.Value;

					/*					
					 * Just here suffixes can follow!
					 */
					if (laKind != (DTokens.CloseParenthesis))
					{
						DeclaratorSuffixes(ref ret);
					}
				}
			}
			ret.Type = cd;
			Expect(DTokens.CloseParenthesis);
		}

		bool IsDeclaratorSuffix
		{
			get { return laKind == (DTokens.OpenSquareBracket) || laKind == (DTokens.OpenParenthesis); }
		}

		/// <summary>
		/// Note:
		/// http://www.digitalmars.com/d/2.0/declaration.html#DeclaratorSuffix
		/// The definition of a sequence of declarator suffixes is buggy here! Theoretically template parameters can be declared without a surrounding ( and )!
		/// Also, more than one parameter sequences are possible!
		/// 
		/// TemplateParameterList[opt] Parameters MemberFunctionAttributes[opt]
		/// </summary>
		void DeclaratorSuffixes(ref DNode dn)
		{
			parserParts.attributesParser.FunctionAttributes(dn);

			while (laKind == (DTokens.OpenSquareBracket))
			{
				Step();
				var ad = new ArrayDecl() { Location = t.Location, InnerDeclaration = dn.Type };

				if (laKind != (DTokens.CloseSquareBracket))
				{
					ITypeDeclaration keyType = null;
					Lexer.PushLookAheadBackup();
					if (!parserParts.expressionsParser.IsAssignExpression())
					{
						var weakType = AllowWeakTypeParsing;
						AllowWeakTypeParsing = true;

						keyType = ad.KeyType = Type(dn.Parent as IBlockNode);

						AllowWeakTypeParsing = weakType;
					}
					if (keyType == null || laKind != DTokens.CloseSquareBracket)
					{
						Lexer.RestoreLookAheadBackup();
						keyType = ad.KeyType = null;
						ad.KeyExpression = parserParts.expressionsParser.AssignExpression(dn.Parent as IBlockNode);
					}
					else
						Lexer.PopLookAheadBackup();
				}
				Expect(DTokens.CloseSquareBracket);
				ad.EndLocation = t.EndLocation;
				dn.Type = ad;
			}

			if (laKind == (DTokens.OpenParenthesis))
			{
				if (parserParts.templatesParser.IsTemplateParameterList())
					parserParts.templatesParser.TemplateParameterList(dn);

				var dm = dn as DMethod;
				if (dm == null)
				{
					dm = new DMethod();
					dm.AssignFrom(dn);
					dn = dm;
				}

				Parameters(dm);
			}

			parserParts.attributesParser.FunctionAttributes(ref dn.Attributes);
		}

		public ITypeDeclaration IdentifierList(IBlockNode scope = null)
		{
			ITypeDeclaration td = null;

			switch (laKind)
			{
				case DTokens.This:
				case DTokens.Super:
					Step();
					td = new DTokenDeclaration(t.Kind) { Location = t.Location, EndLocation = t.EndLocation };

					if (!Expect(DTokens.Dot))
						return td;
					break;
			}

			bool notInit = false;
			do
			{
				if (notInit)
					Step();
				else
					notInit = true;

				ITypeDeclaration ttd;

				if (IsTemplateInstance)
					ttd = parserParts.templatesParser.TemplateInstance(scope);
				else if (Expect(DTokens.Identifier))
					ttd = new IdentifierDeclaration(t.Value) { Location = t.Location, EndLocation = t.EndLocation };
				else if (IsEOF)
					return new DTokenDeclaration(DTokens.Incomplete, td);
				else
					ttd = null;
				if (ttd != null)
					ttd.InnerDeclaration = td;
				td = ttd;
			}
			while (laKind == DTokens.Dot);

			return td;
		}

		public bool IsTemplateInstance
		{
			get
			{
				Lexer.StartPeek();
				if (laKind != DTokens.Identifier && (!DTokensSemanticHelpers.IsStorageClass(laKind) || Peek().Kind != DTokens.Identifier))
					return false;

				var r = Peek().Kind == DTokens.Not && !(Peek().Kind == DTokens.Is || Lexer.CurrentPeekToken.Kind == DTokens.In);
				Peek(1);
				return r;
			}
		}

		public bool LookAheadIsStorageClass
		{
			get
			{
				switch (laKind)
				{
					case DTokens.Abstract:
					case DTokens.Auto:
					case DTokens.Deprecated:
					case DTokens.Extern:
					case DTokens.Final:
					case DTokens.Override:
					case DTokens.Scope:
					case DTokens.Synchronized:
					case DTokens.__gshared:
					case DTokens.Ref:
					case DTokens.At:
						return true;
					default:
						return parserParts.attributesParser.IsAttributeSpecifier;
				}
			}
		}

		public ITypeDeclaration Type(IBlockNode scope)
		{
			var td = BasicType(scope);

			if (td != null && IsDeclarator2())
			{
				var ttd = Declarator2(scope);
				if (ttd != null)
					ttd.InnerMost.InnerDeclaration = td;
				td = ttd;
			}

			return td;
		}

		bool IsDeclarator2()
		{
			return IsBasicType2() || laKind == (DTokens.OpenParenthesis);
		}

		/// <summary>
		/// http://www.digitalmars.com/d/2.0/declaration.html#Declarator2
		/// The next bug: Following the definition strictly, this function would end up in an endless loop of requesting another Declarator2
		/// 
		/// So here I think that a Declarator2 only consists of a couple of BasicType2's and some DeclaratorSuffixes
		/// </summary>
		/// <returns></returns>
		ITypeDeclaration Declarator2(IBlockNode scope = null)
		{
			ITypeDeclaration td = null;
			if (laKind == (DTokens.OpenParenthesis))
			{
				Step();
				td = Declarator2(scope);

				if (AllowWeakTypeParsing && (td == null
					|| (t.Kind == DTokens.OpenParenthesis && laKind == DTokens.CloseParenthesis) /* -- means if an argumentless function call has been made, return null because this would be an expression */
					|| laKind != DTokens.CloseParenthesis))
					return null;

				Expect(DTokens.CloseParenthesis);

				// DeclaratorSuffixes
				if (laKind == (DTokens.OpenSquareBracket))
				{
					DNode dn = new DVariable();
					AssignOrWrapTypeToNode(dn, td);
					DeclaratorSuffixes(ref dn);
					td = dn.Type;

					if (dn.Attributes != null && dn.Attributes.Count != 0)
						foreach (var attr in dn.Attributes)
							DeclarationAttributes.Push(attr);
				}
				return td;
			}

			ParseBasicType2(ref td, scope);

			return td;
		}

		/// <summary>
		/// Parse parameters
		/// </summary>
		public void Parameters(DMethod Parent)
		{
			var ret = Parent.Parameters;
			Expect(DTokens.OpenParenthesis);

			// Empty parameter list
			if (laKind == (DTokens.CloseParenthesis))
			{
				Step();
				return;
			}

			var stk_backup = BlockAttributes;
			BlockAttributes = new Stack<DAttribute>();

			DNode p;

			if (laKind != DTokens.TripleDot && (p = Parameter(Parent)) != null)
			{
				p.Parent = Parent;
				ret.Add(p);
			}

			while (laKind == (DTokens.Comma))
			{
				Step();
				if (laKind == DTokens.TripleDot || laKind == DTokens.CloseParenthesis || (p = Parameter(Parent)) == null)
					break;
				p.Parent = Parent;
				ret.Add(p);
			}

			// It's not specified in the official D syntax spec, but we treat id-only typed anonymous parameters as non-typed id-full parameters
			if (Parent != null && Parent.SpecialType == DMethod.MethodType.AnonymousDelegate)
			{
				foreach (var r in ret)
					if (r.NameHash == 0 && r.Type is IdentifierDeclaration && r.Type.InnerDeclaration == null)
					{
						r.NameHash = (r.Type as IdentifierDeclaration).IdHash;
						r.Type = null;
					}
			}

			/*
			 * There can be only one '...' in every parameter list
			 */
			if (laKind == DTokens.TripleDot)
			{
				// If it doesn't have a comma, add a VarArgDecl to the last parameter
				bool HadComma = t.Kind == (DTokens.Comma);

				Step();

				if (!HadComma && ret.Count > 0)
				{
					var lastParameter = ret[ret.Count - 1];
					lastParameter.Type = new VarArgDecl(lastParameter.Type);
				}
				else
				{
					var dv = new DVariable();
					dv.Type = new VarArgDecl();
					dv.Parent = Parent;
					ret.Add(dv);
				}
			}

			Expect(DTokens.CloseParenthesis);
			BlockAttributes = stk_backup;
		}

		private DNode Parameter(IBlockNode Scope = null)
		{
			var attr = new List<DAttribute>();
			var startLocation = la.Location;

			CheckForStorageClasses(Scope);

			while ((DTokensSemanticHelpers.IsParamModifier(laKind) && laKind != DTokens.InOut) || (DTokensSemanticHelpers.IsMemberFunctionAttribute(laKind) && Lexer.CurrentPeekToken.Kind != DTokens.OpenParenthesis))
			{
				Step();
				attr.Add(new Modifier(t.Kind));
			}

			if (laKind == DTokens.Auto && Lexer.CurrentPeekToken.Kind == DTokens.Ref) // functional.d:595 // auto ref F fp
			{
				Step();
				Step();
				attr.Add(new Modifier(DTokens.Auto));
				attr.Add(new Modifier(DTokens.Ref));
			}

			var td = BasicType(Scope);

			var ret = Declarator(td, true, Scope);
			if (ret == null)
				return null;
			ret.Location = startLocation;

			if (attr.Count > 0)
			{
				if (ret.Attributes == null)
					ret.Attributes = new List<DAttribute>(attr);
				else
					ret.Attributes.AddRange(attr);
			}

			// DefaultInitializerExpression
			if (laKind == (DTokens.Assign))
			{
				Step();

				var defInit = parserParts.expressionsParser.AssignExpression(Scope);

				var dv = ret as DVariable;
				if (dv != null)
					dv.Initializer = defInit;
			}

			ret.EndLocation = IsEOF ? la.EndLocation : t.EndLocation;

			return ret;
		}

		private IExpression Initializer(IBlockNode Scope = null)
		{
			Expect(DTokens.Assign);

			// VoidInitializer
			if (laKind == DTokens.Void && Lexer.CurrentPeekToken.Kind != DTokens.Dot)
			{
				Step();
				return new VoidInitializer() { Location = t.Location, EndLocation = t.EndLocation };
			}

			return NonVoidInitializer(Scope);
		}

		public IExpression NonVoidInitializer(IBlockNode Scope = null)
		{
			// ArrayInitializers are handled in PrimaryExpression(), whereas setting IsParsingInitializer to true is required!

			#region StructInitializer
			if (laKind == DTokens.OpenCurlyBrace && IsStructInitializer)
			{
				// StructMemberInitializations
				var ae = new StructInitializer() { Location = la.Location };
				var inits = new List<StructMemberInitializer>();

				bool IsInit = true;
				while (IsInit || laKind == (DTokens.Comma))
				{
					Step();
					IsInit = false;

					// Allow empty post-comma expression IF the following token finishes the initializer expression
					// int[] a={1,2,3,4,};
					if (laKind == DTokens.CloseCurlyBrace)
						break;

					// Identifier : NonVoidInitializer
					var sinit = new StructMemberInitializer { Location = la.Location };

					if (laKind == DTokens.Identifier && Lexer.CurrentPeekToken.Kind == DTokens.Colon)
					{
						Step();
						sinit.MemberName = t.Value;
						Step();
					}
					else if (IsEOF)
					{
						sinit.MemberNameHash = DTokens.IncompleteIdHash;
					}

					sinit.Value = NonVoidInitializer(Scope);

					sinit.EndLocation = t.EndLocation;

					inits.Add(sinit);
				}

				Expect(DTokens.CloseCurlyBrace);

				ae.MemberInitializers = inits.ToArray();
				ae.EndLocation = t.EndLocation;

				return ae;
			}
			#endregion

			#region ArrayLiteral | AssocArrayLiteral
			if (laKind == DTokens.OpenSquareBracket && IsArrayInitializer)
				return parserParts.expressionsParser.ArrayLiteral(Scope, false);
			#endregion

			return parserParts.expressionsParser.AssignExpression(Scope);
		}

		/// <summary>
		/// Scan ahead to see if it is an array initializer or an expression.
		/// If it ends with a ';' ',' or '}', it is an array initializer.
		/// </summary>
		bool IsArrayInitializer
		{
			get
			{
				OverPeekBrackets(DTokens.OpenSquareBracket, laKind == DTokens.OpenSquareBracket);
				var k = Lexer.CurrentPeekToken.Kind;
				return k == DTokens.Comma || k == DTokens.Semicolon || k == DTokens.CloseCurlyBrace;
			}
		}

		/// <summary>
		/// If there's a semicolon or a return somewhere inside the braces, it automatically is a delegate, and not a struct initializer
		/// </summary>
		bool IsStructInitializer
		{
			get
			{
				int r = 1;
				var pk = Peek(1);
				while (r > 0 && pk.Kind != DTokens.EOF && pk.Kind != DTokens.__EOF__)
				{
					switch (pk.Kind)
					{
						case DTokens.Return:
						case DTokens.Semicolon:
							return false;
						case DTokens.OpenCurlyBrace:
							r++;
							break;
						case DTokens.CloseCurlyBrace:
							r--;
							break;
					}
					pk = Peek();
				}
				return true;
			}
		}

		public TypeOfDeclaration TypeOf(IBlockNode scope)
		{
			Expect(DTokens.Typeof);
			var md = new TypeOfDeclaration { Location = t.Location };

			if (Expect(DTokens.OpenParenthesis))
			{
				if (laKind == DTokens.Return)
				{
					Step();
					md.Expression = new TokenExpression(DTokens.Return, t.Location, t.EndLocation);
				}
				else
					md.Expression = parserParts.expressionsParser.Expression(scope);
				Expect(DTokens.CloseParenthesis);
			}
			md.EndLocation = t.EndLocation;
			return md;
		}

		VectorDeclaration Vector(IBlockNode scope)
		{
			var startLoc = t == null ? new CodeLocation() : t.Location;
			Expect(DTokens.__vector);
			var md = new VectorDeclaration { Location = startLoc };

			if (Expect(DTokens.OpenParenthesis))
			{
				if (parserParts.expressionsParser.IsAssignExpression())
					md.Id = parserParts.expressionsParser.Expression(scope);
				else
					md.IdDeclaration = Type(scope);
				Expect(DTokens.CloseParenthesis);
			}

			md.EndLocation = t.EndLocation;
			return md;
		}

		#endregion

		static void AssignOrWrapTypeToNode(INode node, ITypeDeclaration td)
		{
			if (node.Type != null)
			{
				var memberFunctionAttrDecl = node.Type as MemberFunctionAttributeDecl;
				while (memberFunctionAttrDecl != null && memberFunctionAttrDecl.InnerType is MemberFunctionAttributeDecl)
					memberFunctionAttrDecl = memberFunctionAttrDecl.InnerType as MemberFunctionAttributeDecl;

				if (memberFunctionAttrDecl != null)
					memberFunctionAttrDecl.InnerType = td;
				else
					node.Type.InnerMost = td;
			}
			else
				node.Type = td;
		}

		public void PushAttribute(DAttribute attr, bool BlockAttributes)
		{
			var stk = BlockAttributes ? this.BlockAttributes : this.DeclarationAttributes;

			var m = attr as Modifier;
			if (m != null)
				// If attr would change the accessability of an item, remove all previously found (so the most near attribute that's next to the item is significant)
				if (DTokensSemanticHelpers.IsVisibilityModifier(m.Token))
					Modifier.CleanupAccessorAttributes(stk, m.Token);
				else
					Modifier.RemoveFromStack(stk, m.Token);

			stk.Push(attr);
		}

		public void ApplyAttributes(DNode n)
		{
			var unfilteredAttributesToAssign = GetCurrentAttributeSet();
			var attributesToAssign = new List<DAttribute>(unfilteredAttributesToAssign);

			foreach (var attribute in unfilteredAttributesToAssign)
			{
				byte? mod = (attribute as Modifier)?.Token;
				if (mod.HasValue)
				{
					switch (mod.Value)
					{
						case DTokens.Immutable:
						case DTokens.Const:
						case DTokens.InOut:
						case DTokens.Shared:
							attributesToAssign.Remove(attribute);
							AssignOrWrapTypeToNode(n, new MemberFunctionAttributeDecl(mod.Value));
							break;
					}
				}
			}

			n.Attributes = attributesToAssign;
		}

		public DAttribute[] GetCurrentAttributeSet_Array()
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

			foreach (var a in BlockAttributes)
			{
				// ISSUE: Theoretically, when having two identically written but semantically different UDA attributes, the first one will become overridden.
				key = a.Accept(vis);
				if ((i = keys.IndexOf(key)) > -1)
					attrs[i] = a;
				else
				{
					keys.Add(key);
					attrs.Insert(0, a);
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
					attrs.Insert(0, a);
				}
			}
			DeclarationAttributes.Clear();

			for (i = attrs.Count - 1; i >= 0; i--)
			{
				var m = attrs[i] as Modifier;
				if (m != null)
				{
					// If accessor already in attribute array, remove it
					if (DTokensSemanticHelpers.IsVisibilityModifier(m.Token))
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
	}
}
