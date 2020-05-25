using System;
using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Parser.Implementations
{
	class DModulesParser : DParserImplementationPart
	{
		readonly DParserParts parserParts;

		public DModulesParser(DParserStateContext stateContext, DParserParts parserParts)
			: base(stateContext) { this.parserParts = parserParts; }

		// http://www.digitalmars.com/d/2.0/module.html

		/// <summary>
		/// Module entry point
		/// </summary>
		public DModule Root()
		{
			Step();

			var module = new DModule();
			module.Location = new CodeLocation(1, 1);
			module.BlockStartLocation = new CodeLocation(1, 1);

			// Now only declarations or other statements are allowed!
			while (!IsEOF)
			{
				DeclDef(module);
			}

			// Also track comments at a module's end e.g. for multi-line comment folding
			GetComments();

			module.EndLocation = la.Location;
			return module;
		}

		public void DeclDef(DBlockNode module)
		{
			if (parserParts.attributesParser.IsAttributeSpecifier)
			{
				do
					parserParts.attributesParser.AttributeSpecifier(module);
				while (parserParts.attributesParser.IsAttributeSpecifier);

				var tkind = t.Kind;
				if (tkind == DTokens.Semicolon || tkind == DTokens.CloseCurlyBrace || tkind == DTokens.Colon)
					return;
			}

			if (laKind == DTokens.Semicolon)
			{
				Step();
				return;
			}

			switch (laKind)
			{
				case DTokens.Module:
					var mod = module as DModule;

					var ddoc = GetComments();
					var ms = ModuleDeclaration();
					ms.ParentNode = module;
					ddoc += CheckForPostSemicolonComment();

					if (mod != null)
					{
						if (mod.StaticStatements.Count != 0 ||
							mod.Children.Count != 0)
							SynErr(DTokens.Module, "Module declaration must stand at a module's beginning.");

						mod.OptionalModuleStatement = ms;
						mod.Description = ddoc;

						if (ms.ModuleName != null)
							mod.ModuleName = ms.ModuleName.ToString();
					}
					else
						SynErr(DTokens.Module, "Module statements only allowed in module scope.");

					module.Add(ms);
					break;
				case DTokens.Import:
					module.Add(ImportDeclaration(module));
					break;
				case DTokens.This:
					module.Add(parserParts.bodiedSymbolsParser.Constructor(module, module is DClassLike && ((DClassLike)module).ClassType == DTokens.Struct));
					break;
				case DTokens.Tilde:
					if (Lexer.CurrentPeekToken.Kind != DTokens.This)
						goto default;
					module.Add(parserParts.bodiedSymbolsParser.Destructor());
					break;
				case DTokens.Invariant:
					module.Add(parserParts.attributesParser._Invariant());
					break;
				case DTokens.Unittest:
					Step();
					var dbs = new DMethod(DMethod.MethodType.Unittest);
					parserParts.declarationParser.ApplyAttributes(dbs);
					dbs.Location = t.Location;
					parserParts.bodiedSymbolsParser.FunctionBody(dbs);
					module.Add(dbs);
					break;
				/*
				 * VersionSpecification: 
				 *		version = Identifier ; 
				 *		version = IntegerLiteral ;
				 * 
				 * DebugSpecification: 
				 *		debug = Identifier ; 
				 *		debug = IntegerLiteral ;
				 */
				case DTokens.Version:
				case DTokens.Debug:
					if (Peek(1).Kind == DTokens.Assign)
					{
						DebugSpecification ds = null;
						VersionSpecification vs = null;

						if (laKind == DTokens.Version)
							vs = new VersionSpecification
							{
								Location = la.Location,
								Attributes = parserParts.declarationParser.GetCurrentAttributeSet_Array()
							};
						else
							ds = new DebugSpecification
							{
								Location = la.Location,
								Attributes = parserParts.declarationParser.GetCurrentAttributeSet_Array()
							};

						Step();
						Step();

						if (laKind == DTokens.Literal)
						{
							Step();
							if (t.LiteralFormat != LiteralFormat.Scalar)
								SynErr(t.Kind, "Integer literal expected!");
							try
							{
								if (vs != null)
									vs.SpecifiedNumber = Convert.ToUInt64(t.LiteralValue);
								else
									ds.SpecifiedDebugLevel = Convert.ToUInt64(t.LiteralValue);
							}
							catch
							{
							}
						}
						else if (laKind == DTokens.Identifier)
						{
							Step();
							if (vs != null)
								vs.SpecifiedId = t.Value;
							else
								ds.SpecifiedId = t.Value;
						}
						else if (IsEOF)
						{
							if (vs != null)
								vs.SpecifiedId = DTokens.IncompleteId;
							else
								ds.SpecifiedId = DTokens.IncompleteId;
						}
						else if (ds == null)
							Expect(DTokens.Identifier);

						Expect(DTokens.Semicolon);

						((AbstractStatement)ds ?? vs).EndLocation = t.EndLocation;

						module.Add(vs as StaticStatement ?? ds);
					}
					else
						DeclarationCondition(module);
					break;
				case DTokens.Static:
					if (Lexer.CurrentPeekToken.Kind == DTokens.If)
						goto case DTokens.Version;
					else if (Lexer.CurrentPeekToken.Kind == DTokens.Foreach || Lexer.CurrentPeekToken.Kind == DTokens.Foreach_Reverse)
					{
						Step();
						module.Add(parserParts.statementParser.ForeachStatement(module, null, true) as StaticForeachStatement);
						break;
					}
					goto default;
				case DTokens.Assert:
					Step();
					parserParts.declarationParser.CheckForStorageClasses(module);
					if (!Modifier.ContainsAnyAttributeToken(DeclarationAttributes, DTokens.Static))
						SynErr(DTokens.Static, "Static assert statements must be explicitly marked as static");

					module.Add(ParseStaticAssertStatement(module));
					Expect(DTokens.Semicolon);
					break;
				case DTokens.Mixin:
					switch (Peek(1).Kind)
					{
						case DTokens.Template:
							module.Add(parserParts.templatesParser.TemplateDeclaration(module));
							break;

						case DTokens.__vector:
						case DTokens.Typeof:
						case DTokens.Dot:
						case DTokens.Identifier://TemplateMixin
							var tmx = parserParts.templatesParser.TemplateMixin(module);
							if (tmx.MixinId == null)
								module.Add(tmx);
							else
								module.Add(new NamedTemplateMixinNode(tmx));
							break;

						case DTokens.OpenParenthesis:
							module.Add(MixinDeclaration(module, null));
							break;
						default:
							Step();
							SynErr(DTokens.Identifier);
							break;
					}
					break;
				case DTokens.OpenCurlyBrace:
					AttributeBlock(module);
					break;
				// Class Allocators
				// Note: Although occuring in global scope, parse it anyway but declare it as semantic nonsense;)
				case DTokens.New:
					ParseAllocator(module, DMethod.MethodType.Allocator);
					break;
				case DTokens.Delete:
					ParseAllocator(module, DMethod.MethodType.Deallocator);
					break;
				default:
					var decls = parserParts.declarationParser.Declaration(module);
					if (module != null && decls != null)
						module.AddRange(decls);
					break;
			}
		}

		void ParseAllocator(IBlockNode scope, DMethod.MethodType methodType)
		{
			Step();

			var dm = new DMethod(methodType) { Location = t.Location };
			if(methodType == DMethod.MethodType.Deallocator)
			{
				dm.Name = "delete";
			}
			parserParts.declarationParser.ApplyAttributes(dm);

			parserParts.declarationParser.Parameters(dm);
			parserParts.bodiedSymbolsParser.FunctionBody(dm);
			scope.Add(dm);
		}

		public StaticAssertStatement ParseStaticAssertStatement(IBlockNode scope)
		{
			DeclarationAttributes.Clear();

			var ass = new StaticAssertStatement
			{
				Attributes = parserParts.declarationParser.GetCurrentAttributeSet_Array(),
				Location = t.Location
			};

			if (Expect(DTokens.OpenParenthesis))
			{
				ass.AssertedExpression = parserParts.expressionsParser.AssignExpression();
				if (laKind == (DTokens.Comma))
				{
					Step();
					ass.Message = parserParts.expressionsParser.AssignExpression();
				}

				Expect(DTokens.CloseParenthesis);
			}

			ass.EndLocation = t.EndLocation;
			return ass;
		}

		public IMetaDeclarationBlock AttributeBlock(DBlockNode module)
		{
			/*
			 * If there are attributes given, put their references into the meta block.
			 * Also, pop them from the declarationAttributes stack on to the block attributes so they will be assigned to all child items later on.
			 */

			IMetaDeclarationBlock metaDeclBlock;

			if (DeclarationAttributes.Count != 0)
				metaDeclBlock = new AttributeMetaDeclarationBlock(DeclarationAttributes.ToArray()) { BlockStartLocation = la.Location };
			else
				metaDeclBlock = new MetaDeclarationBlock { BlockStartLocation = la.Location };

			var stk_backup = BlockAttributes;
			BlockAttributes = new Stack<DAttribute>();
			foreach (var attr in stk_backup)
			{
				if (attr is Modifier)
				{
					switch ((attr as Modifier).Token)
					{
						case DTokens.Final:
							continue;
					}
				}
				else if (attr is BuiltInAtAttribute)
				{
					switch ((attr as BuiltInAtAttribute).Kind)
					{
						case BuiltInAtAttribute.BuiltInAttributes.Safe:
						case BuiltInAtAttribute.BuiltInAttributes.System:
						case BuiltInAtAttribute.BuiltInAttributes.Trusted:
							continue;
					}
				}

				BlockAttributes.Push(attr);
			}


			while (DeclarationAttributes.Count > 0)
				BlockAttributes.Push(DeclarationAttributes.Pop());

			parserParts.bodiedSymbolsParser.ClassBody(module, true, false);

			BlockAttributes = stk_backup;

			// Store the meta block
			metaDeclBlock.EndLocation = t.EndLocation;
			if (module != null)
				module.Add(metaDeclBlock);
			return metaDeclBlock;
		}

		public DeclarationCondition Condition(IBlockNode parent)
		{
			DeclarationCondition c = null;

			switch (laKind)
			{
				case DTokens.Version:
					/*				 
					 * http://www.dlang.org/version.html#VersionSpecification
					 * VersionCondition: 
					 *		version ( IntegerLiteral ) 
					 *		version ( Identifier ) 
					 *		version ( unittest )
					 *		version ( assert )
					 */
					Step();
					if (Expect(DTokens.OpenParenthesis))
					{
						switch (laKind)
						{
							case DTokens.Unittest:
								Step();
								c = new VersionCondition("unittest") { IdLocation = t.Location };
								break;
							case DTokens.Assert:
								Step();
								c = new VersionCondition("assert") { IdLocation = t.Location };
								break;
							case DTokens.Literal:
								Step();
								if (t.LiteralFormat != LiteralFormat.Scalar)
									SynErr(t.Kind, "Version number must be an integer");
								else
								{
									ulong v;
									try
									{
										v = Convert.ToUInt64(t.LiteralValue);
									}
									catch
									{
										SemErr(DTokens.Version, "Can't handle " + t.LiteralValue.ToString() + " as version constraint; taking ulong.max instead");
										v = ulong.MaxValue;
									}
									c = new VersionCondition(v) { IdLocation = t.Location };
								}
								break;
							default:
								if (Expect(DTokens.Identifier))
									c = new VersionCondition(t.Value) { IdLocation = t.Location };
								else if (IsEOF)
								{
									c = new VersionCondition(DTokens.IncompleteId);
									parent.Add(new DVariable { Attributes = new List<DAttribute> { c } });
								}
								break;
						}

						Expect(DTokens.CloseParenthesis);
					}

					if (c == null)
						c = new VersionCondition(0);
					break;

				case DTokens.Debug:
					/*				
					 * DebugCondition:
					 *		debug 
					 *		debug ( IntegerLiteral )
					 *		debug ( Identifier )
					 */
					Step();
					if (laKind == DTokens.OpenParenthesis)
					{
						Step();

						if (laKind == DTokens.Literal)
						{
							Step();
							if (t.LiteralFormat != LiteralFormat.Scalar)
								SynErr(t.Kind, "Debug level must be an integer");
							else
							{
								ulong v;
								try
								{
									v = Convert.ToUInt64(t.LiteralValue);
								}
								catch
								{
									SemErr(DTokens.Debug, "Can't handle " + t.LiteralValue.ToString() + " as debug constraint; taking ulong.max instead");
									v = ulong.MaxValue;
								}

								c = new DebugCondition(v) { IdLocation = t.Location };
							}
						}
						else if (Expect(DTokens.Identifier))
							c = new DebugCondition((string)t.LiteralValue) { IdLocation = t.Location };
						else if (IsEOF)
						{
							c = new DebugCondition(DTokens.IncompleteId);
							parent.Add(new DVariable { Attributes = new List<DAttribute> { c } });
						}

						Expect(DTokens.CloseParenthesis);
					}

					if (c == null)
						c = new DebugCondition();
					break;

				case DTokens.Static:
					/*				
					 * StaticIfCondition: 
					 *		static if ( AssignExpression )
					 */
					Step();
					if (Expect(DTokens.If) && Expect(DTokens.OpenParenthesis))
					{
						var x = parserParts.expressionsParser.AssignExpression(parent);
						c = new StaticIfCondition(x);

						if (!Expect(DTokens.CloseParenthesis) && IsEOF)
							parent.Add(new DVariable { Attributes = new List<DAttribute> { c } });
					}
					else
						c = new StaticIfCondition(null);
					break;

				default:
					throw new Exception("Condition() should only be called if Version/Debug/If is the next token!");
			}

			return c;
		}

		void DeclarationCondition(DBlockNode module)
		{
			var sl = la.Location;

			var c = Condition(module);

			c.Location = sl;
			c.EndLocation = t.EndLocation;

			bool allowElse = laKind != DTokens.Colon;

			var metaBlock = parserParts.attributesParser.AttributeSpecifier(module, c, true) as AttributeMetaDeclaration;

			if (allowElse && metaBlock == null)
			{
				SynErr(t.Kind, "Wrong meta block type. (see DeclarationCondition();)");
				return;
			}
			else if (allowElse && laKind == DTokens.Else)
			{
				Step();

				c = new NegatedDeclarationCondition(c);

				BlockAttributes.Push(c);
				if (laKind == DTokens.OpenCurlyBrace)
				{
					metaBlock.OptionalElseBlock = new ElseMetaDeclarationBlock
					{
						Location = t.Location,
						BlockStartLocation = la.Location
					};
					parserParts.bodiedSymbolsParser.ClassBody(module, true, false);
				}
				else if (laKind == DTokens.Colon)
				{
					metaBlock.OptionalElseBlock = new ElseMetaDeclarationSection
					{
						Location = t.Location,
						EndLocation = la.EndLocation
					};
					Step();
					return;
				}
				else
				{
					metaBlock.OptionalElseBlock = new ElseMetaDeclaration { Location = t.Location };
					DeclDef(module);
				}
				BlockAttributes.Pop();

				metaBlock.OptionalElseBlock.EndLocation = t.EndLocation;
			}
		}

		public ModuleStatement ModuleDeclaration()
		{
			Expect(DTokens.Module);
			var ret = new ModuleStatement { Location = t.Location };
			ret.ModuleName = ModuleFullyQualifiedName();
			Expect(DTokens.Semicolon);
			ret.EndLocation = t.EndLocation;
			return ret;
		}

		ITypeDeclaration ModuleFullyQualifiedName()
		{
			if (!Expect(DTokens.Identifier))
				return IsEOF ? new DTokenDeclaration(DTokens.Incomplete) : null;

			var td = new IdentifierDeclaration(t.Value) { Location = t.Location, EndLocation = t.EndLocation };

			while (laKind == DTokens.Dot)
			{
				Step();
				if (Expect(DTokens.Identifier))
					td = new IdentifierDeclaration(t.Value) { Location = t.Location, EndLocation = t.EndLocation, InnerDeclaration = td };
				else if (IsEOF)
					td = new IdentifierDeclaration(DTokens.IncompleteIdHash) { InnerDeclaration = td, Location = t.Location, EndLocation = t.EndLocation };
			}

			return td;
		}

		public ImportStatement ImportDeclaration(IBlockNode scope)
		{
			// In DMD 2.060, the static keyword must be written exactly before the import token
			bool isStatic = t != null && t.Kind == DTokens.Static;
			bool isPublic = Modifier.ContainsAnyAttributeToken(DeclarationAttributes, DTokens.Public) ||
							Modifier.ContainsAnyAttributeToken(BlockAttributes, DTokens.Public);

			Expect(DTokens.Import);

			var importStatement = new ImportStatement {
				Attributes = parserParts.declarationParser.GetCurrentAttributeSet_Array(),
				Location = t.Location,
				IsStatic = isStatic,
				IsPublic = isPublic
			};

			DeclarationAttributes.Clear();

			var imp = _Import();

			while (laKind == DTokens.Comma)
			{
				importStatement.Imports.Add(imp);

				Step();

				imp = _Import();
			}

			if (laKind == DTokens.Colon)
			{
				Step();
				importStatement.ImportBindList = ImportBindings(imp);
			}
			else
				importStatement.Imports.Add(imp); // Don't forget to add the last import

			Expect(DTokens.Semicolon);

			CheckForPostSemicolonComment();

			importStatement.EndLocation = t.EndLocation;

			// Prepare for resolving external items
			importStatement.CreatePseudoAliases(scope);

			return importStatement;
		}

		ImportStatement.Import _Import()
		{
			var import = new ImportStatement.Import();

			// ModuleAliasIdentifier
			if (Lexer.CurrentPeekToken.Kind == DTokens.Assign)
			{
				if (Expect(DTokens.Identifier))
					import.ModuleAlias = new IdentifierDeclaration(t.Value) { Location = t.Location, EndLocation = t.EndLocation };
				Step();
			}

			import.ModuleIdentifier = ModuleFullyQualifiedName();

			return import;
		}

		ImportStatement.ImportBindings ImportBindings(ImportStatement.Import imp)
		{
			var importBindings = new ImportStatement.ImportBindings { Module = imp };

			bool init = true;
			while (laKind == DTokens.Comma || init)
			{
				if (init)
					init = false;
				else
					Step();

				var symbolAlias = Expect(DTokens.Identifier) ?
					new IdentifierDeclaration(t.Value) { Location = t.Location, EndLocation = t.EndLocation } :
					(IsEOF ? new IdentifierDeclaration(DTokens.IncompleteIdHash) : null);

				if (laKind == DTokens.Assign)
				{
					Step();
					if (Expect(DTokens.Identifier))
						importBindings.SelectedSymbols.Add(new ImportStatement.ImportBinding(new IdentifierDeclaration(t.Value)
						{
							Location = t.Location,
							EndLocation = t.EndLocation
						}, symbolAlias));
					else if (IsEOF)
						importBindings.SelectedSymbols.Add(new ImportStatement.ImportBinding(new IdentifierDeclaration(DTokens.IncompleteIdHash), symbolAlias));
				}
				else if (symbolAlias != null)
					importBindings.SelectedSymbols.Add(new ImportStatement.ImportBinding(symbolAlias));
			}

			return importBindings;
		}

		public MixinStatement MixinDeclaration(IBlockNode Scope, IStatement StmtScope)
		{
			var mx = new MixinStatement
			{
				Attributes = parserParts.declarationParser.GetCurrentAttributeSet_Array(),
				Location = la.Location,
				Parent = StmtScope,
				ParentNode = Scope
			};
			Expect(DTokens.Mixin);
			if (Expect(DTokens.OpenParenthesis))
			{
				mx.MixinExpression = parserParts.expressionsParser.AssignExpression();
				if (Expect(DTokens.CloseParenthesis))
					Expect(DTokens.Semicolon);
			}

			mx.EndLocation = t.EndLocation;

			return mx;
		}
	}
}
