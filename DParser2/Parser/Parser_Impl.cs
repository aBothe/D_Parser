using System;
using System.Collections.Generic;
using System.Text;

using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;

namespace D_Parser.Parser
{
	/// <summary>
	/// Parser for D Code
	/// </summary>
	public partial class DParser
	{
		#region Modules
		// http://www.digitalmars.com/d/2.0/module.html

		/// <summary>
		/// Module entry point
		/// </summary>
		public DModule Root()
		{
			Step();

			var module = new DModule();
			LastParsedObject = module;
			module.Location = la.Location;
			doc = module;

			// Only one module declaration possible!
			if (laKind == (Module))
			{
				module.Description = GetComments();
				module.OptionalModuleStatement= ModuleDeclaration();
				module.Add(module.OptionalModuleStatement);
				module.Description += CheckForPostSemicolonComment();

				if (module.OptionalModuleStatement.ModuleName!=null)
					module.ModuleName = module.OptionalModuleStatement.ModuleName.ToString();
				module.OptionalModuleStatement.ParentNode = doc;
			}

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

		#region Comments
		StringBuilder PreviousComment = new StringBuilder();

		string GetComments()
		{
			var sb = new StringBuilder ();

			foreach (var c in Lexer.Comments)
			{
				if (c.CommentType.HasFlag(Comment.Type.Documentation))
					sb.AppendLine(c.CommentText);
			}

			TrackerVariables.Comments.AddRange(Lexer.Comments);
			Lexer.Comments.Clear();

			sb.Trim ();

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
		string CheckForPostSemicolonComment()
		{
			if (t == null)
				return string.Empty;

			int ExpectedLine = t.Line;

			var ret = new StringBuilder ();

			int i=0;
			foreach (var c in Lexer.Comments)
			{
				if (c.CommentType.HasFlag(Comment.Type.Documentation))
				{
					// Ignore ddoc comments made e.g. in int a /** ignored comment */, b,c; 
					// , whereas this method is called as t is the final semicolon
					if (c.EndPosition <= t.Location)
					{
						i++;
						TrackerVariables.Comments.Add(c);
						continue;
					}
					else if (c.StartPosition.Line > ExpectedLine)
						break;

					ret.AppendLine(c.CommentText);
				}
				
				i++;
				TrackerVariables.Comments.Add(c);
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

			ret.Insert (0, Environment.NewLine);

			PreviousComment.Append(ret.ToString());
			return ret.ToString();
		}

		#endregion

		void DeclDef(DBlockNode module)
		{
			//AttributeSpecifier
			while (IsAttributeSpecifier())
			{
				AttributeSpecifier(module);

				if (t.Kind == Colon || laKind == CloseCurlyBrace || IsEOF)
					return;
			}

			if (laKind == Semicolon)
			{
				LastParsedObject = null;
				Step();
				return;
			}

			//ImportDeclaration
			if (laKind == Import)
				module.Add(ImportDeclaration(module));

			//Constructor
			else if (laKind == (This))
				module.Add(Constructor(module, module is DClassLike ? ((DClassLike)module).ClassType == DTokens.Struct : false));

			//Destructor
			else if (laKind == (Tilde) && Lexer.CurrentPeekToken.Kind == (This))
				module.Add(Destructor());

			//Invariant
			else if (laKind == (Invariant))
				module.Add(_Invariant());

			//UnitTest
			else if (laKind == (Unittest))
			{
				Step();
				var dbs = new DMethod(DMethod.MethodType.Unittest);
				ApplyAttributes(dbs);
				LastParsedObject = dbs;
				dbs.Location = t.Location;
				FunctionBody(dbs);
				dbs.EndLocation = t.EndLocation;
				module.Add(dbs);
			}

			/*
			 * VersionSpecification: 
			 *		version = Identifier ; 
			 *		version = IntegerLiteral ;
			 * 
			 * DebugSpecification: 
			 *		debug = Identifier ; 
			 *		debug = IntegerLiteral ;
			 */
			else if ((laKind == Version || laKind == Debug) && Peek(1).Kind == Assign)
			{
				DebugSpecification ds = null;
				VersionSpecification vs = null;

				if (laKind == Version)
					LastParsedObject = vs = new VersionSpecification { Location = la.Location, Attributes = GetCurrentAttributeSet_Array() };
				else
					LastParsedObject = ds = new DebugSpecification { Location = la.Location, Attributes = GetCurrentAttributeSet_Array() };
				
				Step();
				Step();

				if (laKind == Literal)
				{
					Step();
					if (t.LiteralFormat != LiteralFormat.Scalar)
						SynErr(t.Kind, "Integer literal expected!");
					try
					{
						if (vs != null)
							vs.SpecifiedNumber = Convert.ToInt32(t.LiteralValue);
						else
							ds.SpecifiedDebugLevel = Convert.ToInt32(t.LiteralValue);
					}
					catch { }
				}
				else if (laKind == Identifier)
				{
					Step();
					if (vs != null)
						vs.SpecifiedId = t.Value;
					else
						ds.SpecifiedId = t.Value;
				}
				else if (ds == null)
					Expect(Identifier);

				Expect(Semicolon);

				if (vs == null)
					ds.EndLocation = t.EndLocation;
				else
					vs.EndLocation = t.EndLocation;

				module.Add(vs as StaticStatement ?? ds);
			}

			else if (laKind == Version || laKind == Debug || (laKind == Static && Lexer.CurrentPeekToken.Kind == If))
				DeclarationCondition(module);

			//StaticAssert
			else if (laKind == (Assert))
			{
				Step();

				if (!Modifier.ContainsAttribute(DeclarationAttributes, Static))
					SynErr(Static, "Static assert statements must be explicitly marked as static");

				var ass = new StaticAssertStatement { Attributes = GetCurrentAttributeSet_Array(), Location = t.Location };

				if (Expect(OpenParenthesis))
				{
					ass.AssertedExpression = AssignExpression();
					if (laKind == (Comma))
					{
						Step();
						ass.Message = AssignExpression();
					}
					if(Expect(CloseParenthesis))
						Expect(Semicolon);
				}

				ass.EndLocation = t.EndLocation;

				module.Add(ass);
			}

			//TemplateMixinDeclaration
			else if (laKind == Mixin)
			{
				if (Peek(1).Kind == Template)
					module.Add(TemplateDeclaration(module));

				//TemplateMixin
				else if (Lexer.CurrentPeekToken.Kind == Identifier)
				{
					var tmx = TemplateMixin(module);
					if(tmx.MixinId==null)
						module.Add(tmx);
					else
						module.Add(new NamedTemplateMixinNode(tmx));
				}

				//MixinDeclaration
				else if (Lexer.CurrentPeekToken.Kind == OpenParenthesis)
					module.Add(MixinDeclaration(module,null));
				else
				{
					Step();
					SynErr(Identifier);
				}
			}

			// {
			else if (laKind == (OpenCurlyBrace))
				AttributeBlock(module);

			// Class Allocators
			// Note: Although occuring in global scope, parse it anyway but declare it as semantic nonsense;)
			else if (laKind == (New))
			{
				Step();

				var dm = new DMethod(DMethod.MethodType.Allocator) { Location = t.Location };
				ApplyAttributes(dm);

				dm.Parameters = Parameters(dm);
				FunctionBody(dm);
				dm.EndLocation = t.EndLocation;
				module.Add(dm);
			}

			// Class Deallocators
			else if (laKind == Delete)
			{
				Step();

				var dm = new DMethod(DMethod.MethodType.Deallocator) { Location = t.Location };
				dm.Name = "delete";
				ApplyAttributes(dm);

				dm.Parameters = Parameters(dm);
				FunctionBody(dm);
				dm.EndLocation = t.EndLocation;
				module.Add(dm);
			}

			// else:
			else
			{
				var decls = Declaration(module);
				if(module != null && decls!=null)
					module.AddRange(decls);
			}
		}

		IMetaDeclarationBlock AttributeBlock(DBlockNode module)
		{
			int popCount = DeclarationAttributes.Count;

			/*
			 * If there are attributes given, put their references into the meta block.
			 * Also, pop them from the declarationAttributes stack on to the block attributes so they will be assigned to all child items later on.
			 */

			IMetaDeclarationBlock metaDeclBlock;

			if (popCount != 0)
				metaDeclBlock = new AttributeMetaDeclarationBlock(DeclarationAttributes.ToArray()) { BlockStartLocation = la.Location };
			else
				metaDeclBlock = new MetaDeclarationBlock { BlockStartLocation = la.Location };

			while (DeclarationAttributes.Count > 0)
				BlockAttributes.Push(DeclarationAttributes.Pop());

			ClassBody(module, true, false);

			// Pop the previously pushed attributes back off the stack
			for (int i = popCount; i > 0; i--)
				BlockAttributes.Pop();

			// Store the meta block
			metaDeclBlock.EndLocation = t.EndLocation;
			if(module!=null)
				module.Add(metaDeclBlock);
			return metaDeclBlock;
		}

		DeclarationCondition Condition(IBlockNode parent)
		{
			DeclarationCondition c = null;

			/* 
			 * http://www.dlang.org/version.html#VersionSpecification
			 * VersionCondition: 
			 *		version ( IntegerLiteral ) 
			 *		version ( Identifier ) 
			 *		version ( unittest )
			 *		version ( assert )
			 */
			if (laKind == Version)
			{
				Step();
				if (Expect(OpenParenthesis))
				{
					TrackerVariables.ExpectingIdentifier = true;
					if (laKind == Unittest)
					{
						Step();
						c = new VersionCondition("unittest") { IdLocation = t.Location };
					}
					else if (laKind == Assert)
					{
						Step();
						c = new VersionCondition("assert") { IdLocation = t.Location };
					}
					else if (laKind == Literal)
					{
						Step();
						if (t.LiteralFormat != LiteralFormat.Scalar)
							SynErr(t.Kind, "Version number must be an integer");
						else
							c = new VersionCondition(Convert.ToInt32(t.LiteralValue)) { IdLocation = t.Location };
					}
					else if (Expect(Identifier))
						c = new VersionCondition(t.Value) { IdLocation = t.Location };

					if (Expect(CloseParenthesis))
						TrackerVariables.ExpectingIdentifier = false;
				}
				
				if(c==null)
					c = new VersionCondition("");
			}

			/*
			 * DebugCondition:
			 *		debug 
			 *		debug ( IntegerLiteral )
			 *		debug ( Identifier )
			 */
			else if (laKind == Debug)
			{
				Step();
				if (laKind == OpenParenthesis)
				{
					Step();
					TrackerVariables.ExpectingIdentifier = true;

					if (laKind == Literal)
					{
						Step();
						if (t.LiteralFormat != LiteralFormat.Scalar)
							SynErr(t.Kind, "Debug level must be an integer");
						else
							c = new DebugCondition(Convert.ToInt32(t.LiteralValue)) { IdLocation = t.Location };
					}
					else if (Expect(Identifier))
						c = new DebugCondition((string)t.LiteralValue) { IdLocation = t.Location };
					else
						c = new DebugCondition();

					if (Expect(CloseParenthesis))
						TrackerVariables.ExpectingIdentifier = false;
				}
				else
					c = new DebugCondition();
			}

			/*
			 * StaticIfCondition: 
			 *		static if ( AssignExpression )
			 */
			else if (laKind == Static)
			{
				Step ();
				if (Expect (If) && Expect(OpenParenthesis))
				{
					var x = AssignExpression(parent);
					c = new StaticIfCondition(x);

					Expect(CloseParenthesis);
				}
				else
					c = new StaticIfCondition(null);
			}
			else
				throw new Exception("Condition() should only be called if Version/Debug/If is the next token!");

			LastParsedObject = c;

			return c;
		}

		void DeclarationCondition(DBlockNode module)
		{
			var sl = la.Location;

			var c = Condition(module);

			c.Location = sl;
			c.EndLocation = t.EndLocation;

			bool allowElse = laKind != Colon;

			var metaBlock = AttributeTrail(module, c, true) as AttributeMetaDeclaration;

			if (allowElse && metaBlock == null)
			{
				SynErr(t.Kind, "Wrong meta block type. (see DeclarationCondition();)");
				return;
			}
			else if (allowElse && laKind == Else)
			{
				Step();

				c = new NegatedDeclarationCondition(c);

				BlockAttributes.Push(c);
				if (laKind == OpenCurlyBrace)
				{
					metaBlock.OptionalElseBlock = new ElseMetaDeclarationBlock { Location = t.Location, BlockStartLocation = la.Location };
					ClassBody(module, true, false);
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

		ModuleStatement ModuleDeclaration()
		{
			Expect(Module);
			var ret = new ModuleStatement { Location=t.Location };
			LastParsedObject = ret;
			ret.ModuleName = ModuleFullyQualifiedName();
			if (Expect(Semicolon))
				LastParsedObject = null;
			ret.EndLocation = t.EndLocation;
			return ret;
		}

		ITypeDeclaration ModuleFullyQualifiedName()
		{
			if (!Expect(Identifier))
				return null;

			var td = new IdentifierDeclaration(t.Value) { Location=t.Location,EndLocation=t.EndLocation };

			while (laKind == Dot)
			{
				Step();
				Expect(Identifier);
				td = new IdentifierDeclaration(t.Value) { Location=t.Location, EndLocation=t.EndLocation, InnerDeclaration = td };
			}

			return td;
		}

		ImportStatement ImportDeclaration(IBlockNode scope)
		{
			// In DMD 2.060, the static keyword must be written exactly before the import token
			bool isStatic = t!= null && t.Kind == Static; 
			bool isPublic = Modifier.ContainsAttribute(DeclarationAttributes, Public) ||
							Modifier.ContainsAttribute(BlockAttributes,Public);

			Expect(Import);

			var importStatement = new ImportStatement { Attributes = GetCurrentAttributeSet_Array(), Location=t.Location, IsStatic = isStatic, IsPublic = isPublic };
			
			DeclarationAttributes.Clear();
			
			var imp = _Import();

			while (laKind == Comma)
			{
				importStatement.Imports.Add(imp);

				Step();

				imp = _Import();
			}

			if (laKind == Colon)
			{
				Step();
				importStatement.ImportBindList = ImportBindings(imp);
			}
			else
				importStatement.Imports.Add(imp); // Don't forget to add the last import

			if (Expect(Semicolon))
				LastParsedObject = null;

			CheckForPostSemicolonComment();

			importStatement.EndLocation = t.EndLocation;

			// Prepare for resolving external items
			importStatement.CreatePseudoAliases(scope);

			return importStatement;
		}

		ImportStatement.Import _Import()
		{
			var import = new ImportStatement.Import();
			LastParsedObject = import;

			// ModuleAliasIdentifier
			if (Lexer.CurrentPeekToken.Kind == Assign)
			{
				if(Expect(Identifier))
					import.ModuleAlias = new IdentifierDeclaration(t.Value) { Location = t.Location, EndLocation = t.EndLocation };
				Step();
			}

			import.ModuleIdentifier = ModuleFullyQualifiedName();

			if (!IsEOF)
				LastParsedObject = null;

			return import;
		}

		ImportStatement.ImportBindings ImportBindings(ImportStatement.Import imp)
		{
			var importBindings = new ImportStatement.ImportBindings { Module=imp };
			LastParsedObject = importBindings;

			bool init = true;
			while (laKind == Comma || init)
			{
				if (init)
					init = false;
				else
					Step();

				if (Expect(Identifier))
				{
					var symbolAlias = new IdentifierDeclaration(t.Value){ Location = t.Location, EndLocation = t.EndLocation };
						
					if (laKind == Assign)
					{
						Step();
						if (Expect (Identifier))
							importBindings.SelectedSymbols.Add (new ImportStatement.ImportBinding (new IdentifierDeclaration (t.Value) {
								Location = t.Location,
								EndLocation = t.EndLocation
							}, symbolAlias));
					}
					else
						importBindings.SelectedSymbols.Add(new ImportStatement.ImportBinding(symbolAlias));
				}
			}

			if (!IsEOF)
				LastParsedObject = null;

			return importBindings;
		}

		MixinStatement MixinDeclaration(IBlockNode Scope, IStatement StmtScope)
		{
			var mx = new MixinStatement{
				Attributes = GetCurrentAttributeSet_Array(),
				Location = la.Location,
				Parent = StmtScope,
				ParentNode = Scope
			};
			Expect(Mixin);
			if(Expect(OpenParenthesis))
			{
            	mx.MixinExpression = AssignExpression();
            	if(Expect(CloseParenthesis))
					Expect(Semicolon);
			}
			
			mx.EndLocation = t.EndLocation;
			
			return mx;
		}
		#endregion

		#region Declarations
		// http://www.digitalmars.com/d/2.0/declaration.html

		bool CheckForStorageClasses(DBlockNode scope)
		{
			bool ret = false;
			while (IsStorageClass )
			{
				if (IsAttributeSpecifier()) // extern, align
					AttributeSpecifier(scope);
				else
				{
					Step();
					// Always allow more than only one property DAttribute
					if (!Modifier.ContainsAttribute(DeclarationAttributes.ToArray(), t.Kind))
						PushAttribute(new Modifier(t.Kind, t.Value) { Location = t.Location, EndLocation = t.EndLocation }, false);
				}
				ret = true;
			}
			return ret;
		}

		public INode[] Declaration(IBlockNode Scope)
		{
			// Enum possible storage class attributes
			bool HasStorageClassModifiers = CheckForStorageClasses(Scope as DBlockNode);

			// Aliases
			if (laKind == (Alias) || laKind == Typedef)
				return AliasDeclaration (Scope, HasStorageClassModifiers);
			else if (laKind == (Struct) || laKind == (Union))
				return new[]{ AggregateDeclaration (Scope) };
			else if (laKind == Enum) {
				// As another meta-programming feature, it is possible to create static functions 
				// that return enums, i.e. a constant value or something
				// Additionally, eponymous template declarations are there since 2.064 - and both syntaxes are quite similar
				if (Lexer.CurrentPeekToken.Kind == Identifier && Peek ().Kind == OpenParenthesis) {
					Peek (1);
					var l = Decl (HasStorageClassModifiers, Scope);
					return l != null ? l.ToArray () : null;
				}

				return EnumDeclaration (Scope);
			} else if (laKind == (Class))
				return new[]{ ClassDeclaration (Scope) };
			else if (laKind == (Template) || (laKind == Mixin && Peek (1).Kind == Template))
				return new[]{ TemplateDeclaration (Scope) };
			else if (laKind == (Interface))
				return new[]{ InterfaceDeclaration (Scope) };
			else if (IsBasicType () || laKind == Ref) {
				var l = Decl(HasStorageClassModifiers, Scope);
				return l != null ? l.ToArray () : null;
			}
				
			SynErr(laKind,"Declaration expected, not "+GetTokenString(laKind));
			Step();
			return null;
		}

		INode[] AliasDeclaration(IBlockNode Scope, bool HasStorageClassModifiers)
		{
			Step();
			// _t is just a synthetic node which holds possible following attributes
			var _t = new DVariable();
			ApplyAttributes(_t);
			_t.Description = GetComments();
			List<INode> decls;

			// AliasThis
			if ((laKind == Identifier && Lexer.CurrentPeekToken.Kind == This) ||
				(laKind == This && Lexer.CurrentPeekToken.Kind == Assign))
				return new[]{AliasThisDeclaration(_t, Scope)};

			// AliasInitializerList
			else if(laKind == Identifier && Lexer.CurrentPeekToken.Kind == Assign)
			{
				decls = new List<INode>();
				do{
					if(laKind == Comma)
						Step();
					if(!Expect(Identifier))
						break;
					var dv = new DVariable{
						IsAlias = true,
						Attributes = _t.Attributes,
						Description = _t.Description,
						Name = t.Value,
						NameLocation = t.Location,
						Location = t.Location,
						Parent = Scope
					};
					if(Expect(Assign))
					{
						Lexer.PushLookAheadBackup();
						dv.Type = Type();
						if(!(laKind == Comma || laKind == Semicolon))
						{
							Lexer.RestoreLookAheadBackup();
							dv.Initializer = AssignExpression(Scope);
						}
						else
							Lexer.PopLookAheadBackup();
					}
					decls.Add(dv);
				}
				while(laKind == Comma);

				Expect(Semicolon);
				decls[decls.Count-1].Description += CheckForPostSemicolonComment();
				return decls.ToArray();
			}

			// alias BasicType Declarator
			decls=Decl(HasStorageClassModifiers,Scope, laKind != Identifier || Lexer.CurrentPeekToken.Kind != OpenParenthesis ? null : new Modifier(DTokens.Alias));

			if(decls!=null){
				foreach (var n in decls)
					if (n is DVariable){
						((DNode)n).Attributes.AddRange(_t.Attributes);
						((DVariable)n).IsAlias = true;
					}

				decls[decls.Count-1].Description += CheckForPostSemicolonComment();
				return decls.ToArray ();
			}
			return null;
		}

		DVariable AliasThisDeclaration(DVariable initiallyParsedNode, IBlockNode Scope)
		{
			var dv = new DVariable { 
				Description = initiallyParsedNode.Description,
				Location=t.Location, 
				IsAlias=true,
				IsAliasThis = true,
				NameHash = DVariable.AliasThisIdentifierHash,
				Parent = Scope,
				Attributes = initiallyParsedNode.Attributes
			};
			LastParsedObject = dv;

			if(!(Scope is DClassLike))
				SemErr(DTokens.This, "alias this declarations are only allowed in structs and classes!");

			// alias this = Identifier
			if(laKind == This && Lexer.CurrentPeekToken.Kind == Assign)
			{
				Step(); // Step beyond 'this'
				dv.NameLocation=t.Location;
				Step(); // Step beyond '='
				if(Expect(Identifier))
				{
					dv.Type= new IdentifierDeclaration(t.Value)
					{
						Location = t.Location,
						EndLocation = t.EndLocation
					};
				}
			}
			else
			{
				Step(); // Step beyond Identifier
				dv.Type = new IdentifierDeclaration(t.Value)
				{
					Location=dv.NameLocation =t.Location, 
					EndLocation=t.EndLocation 
				};

				Step(); // Step beyond 'this'
				dv.NameLocation=t.Location;
			}

			dv.EndLocation = t.EndLocation;

			Expect(Semicolon);
			dv.Description += CheckForPostSemicolonComment();
			return dv;
		}

		List<INode> Decl(bool HasStorageClassModifiers,IBlockNode Scope, DAttribute StorageClass = null)
		{
			var startLocation = la.Location;
			var initialComment = GetComments();
			ITypeDeclaration ttd = null;

			CheckForStorageClasses(Scope as DBlockNode);

			// Autodeclaration
			if(StorageClass == null)
				StorageClass = DTokens.ContainsStorageClass(DeclarationAttributes);

			if (laKind == Enum)
			{
				Step();
				PushAttribute(StorageClass = new Modifier(Enum) { Location = t.Location, EndLocation = t.EndLocation },false);
			}
			
			// If there's no explicit type declaration, leave our node's type empty!
			if ((StorageClass != Modifier.Empty && 
				laKind == (Identifier) && (DeclarationAttributes.Count > 0 || Lexer.CurrentPeekToken.Kind == OpenParenthesis))) // public auto var=0; // const foo(...) {} 
			{
				if (Lexer.CurrentPeekToken.Kind == Assign || Lexer.CurrentPeekToken.Kind ==OpenParenthesis) 
				{ }
				else if (Lexer.CurrentPeekToken.Kind == Semicolon)
				{
					SemErr(t.Kind, "Initializer expected for auto type, semicolon found!");
				}
				else
					ttd = BasicType();
			}
			else
				ttd = BasicType();


			if (IsEOF)
			{
				/*
				 * T! -- tix.Arguments == null
				 * T!(int, -- last argument == null
				 * T!(int, bool, -- ditto
				 * T!(int) -- now every argument is complete
				 */
				var tix=ttd as TemplateInstanceExpression;
				if (tix != null) {
					if (tix.Arguments == null || tix.Arguments.Length == 0 ||
					    (tix.Arguments [tix.Arguments.Length - 1] is TokenExpression &&
					    (tix.Arguments [tix.Arguments.Length - 1] as TokenExpression).Token == DTokens.INVALID)) {
						LastParsedObject = ttd;
						return null;
					}
				} else if (ttd is MemberFunctionAttributeDecl && (ttd as MemberFunctionAttributeDecl).InnerType == null) {
					LastParsedObject = ttd;
					return null;
				}
			}

			// Declarators
			var firstNode = Declarator(ttd,false, Scope);
			if (firstNode == null)
				return null;
			firstNode.Description = initialComment;
			firstNode.Location = startLocation;

			// Check for declaration constraints
			if (laKind == (If))
				Constraint(firstNode);

			// BasicType Declarators ;
			if (laKind==Assign || laKind==Comma || laKind==Semicolon)
			{
				// DeclaratorInitializer
				if (laKind == (Assign))
				{
					TrackerVariables.InitializedNode = firstNode;
					var dv = firstNode as DVariable;
					if(dv!=null)
						dv.Initializer = Initializer(Scope);
				}
				firstNode.EndLocation = t.EndLocation;
				var ret = new List<INode>();
				ret.Add(firstNode);

				// DeclaratorIdentifierList
				while (laKind == Comma)
				{
					Step();
					if (Expect(Identifier))
					{
						var otherNode = new DVariable();
						LastParsedObject = otherNode;

						/// Note: In DDoc, all declarations that are made at once (e.g. int a,b,c;) get the same pre-declaration-description!
						otherNode.Description = initialComment;

						otherNode.AssignFrom(firstNode);
						otherNode.Location = t.Location;
						otherNode.Name = t.Value;
						otherNode.NameLocation = t.Location;

						if (laKind == (Assign))
						{
							TrackerVariables.InitializedNode = otherNode;
							otherNode.Initializer = Initializer(Scope);
						}

						otherNode.EndLocation = t.EndLocation;
						ret.Add(otherNode);
					}
					else
						break;
				}

				if (Expect(Semicolon))
					LastParsedObject = null;

				// Note: In DDoc, only the really last declaration will get the post semicolon comment appended
				if (ret.Count > 0)
					ret[ret.Count - 1].Description += CheckForPostSemicolonComment();

				return ret;
			}

			// BasicType Declarator FunctionBody
			else if (firstNode is DMethod && (IsFunctionBody || IsEOF))
			{
				firstNode.Description += CheckForPostSemicolonComment();

				FunctionBody((DMethod)firstNode);

				firstNode.Description += CheckForPostSemicolonComment();

				var ret = new List<INode> ();
				ret.Add (firstNode);
				return ret;
			}
			else
				SynErr(OpenCurlyBrace, "; or function body expected after declaration stub.");

			return null;
		}

		bool IsBasicType()
		{
			return 
				BasicTypes[laKind] || 
				laKind == (Typeof) || 
				IsFunctionAttribute ||
				(laKind == (Dot) && Lexer.CurrentPeekToken.Kind == (Identifier)) || 
				//BasicTypes[Lexer.CurrentPeekToken.Kind] || 
				laKind == (Identifier);
		}

		/// <summary>
		/// Used if the parser is unsure if there's a type or an expression - then, instead of throwing exceptions, the Type()-Methods will simply return null;
		/// </summary>
		public bool AllowWeakTypeParsing = false;

		ITypeDeclaration BasicType()
		{
			bool isModuleScoped = laKind == Dot;
			if (isModuleScoped)
				Step();

			ITypeDeclaration td = null;
			if (BasicTypes[laKind])
			{
				Step();
				return new DTokenDeclaration(t.Kind) { Location=t.Location, EndLocation=t.EndLocation };
			}

			if (MemberFunctionAttribute[laKind])
			{
				Step();
				var md = new MemberFunctionAttributeDecl(t.Kind) { Location=t.Location };
				bool p = false;

				if (laKind == OpenParenthesis)
				{
					Step();
					p = true;

					if (IsEOF)
						return md;
				}

				// e.g. cast(const)
				if (laKind != CloseParenthesis)
					md.InnerType = p ? Type() : BasicType();

				if (p)
					Expect(CloseParenthesis);
				md.EndLocation = t.EndLocation;
				return md;
			}

			//TODO
			if (laKind == Ref)
				Step();

			if (laKind == (Typeof))
			{
				td = TypeOf();
				if (laKind != Dot)
					return td;
			}

			else if (laKind == __vector)
			{
				td = Vector();
				if (laKind != Dot)
					return td;
			}

			if (AllowWeakTypeParsing && laKind != Identifier)
				return null;

			if (td == null)
				td = IdentifierList();
			else
				td.InnerMost = IdentifierList();

			if (td == null)
				TrackerVariables.ExpectingIdentifier = false;
			else if(isModuleScoped)
			{
				var innerMost = td.InnerMost;
				if (innerMost is IdentifierDeclaration)
					((IdentifierDeclaration)innerMost).ModuleScoped = true;
				else if (innerMost is TemplateInstanceExpression)
					((TemplateInstanceExpression)innerMost).ModuleScopedIdentifier = true;
			}

			return td;
		}

		bool IsBasicType2()
		{
			return laKind == (Times) || laKind == (OpenSquareBracket) || laKind == (Delegate) || laKind == (Function);
		}

		ITypeDeclaration BasicType2()
		{
			// *
			if (laKind == (Times))
			{
				Step();
				return new PointerDecl() { Location=t.Location, EndLocation=t.EndLocation };
			}

			// [ ... ]
			else if (laKind == (OpenSquareBracket))
			{
				var startLoc = la.Location;
				Step();
				// [ ]
				if (laKind == (CloseSquareBracket)) 
				{ 
					Step();
					return new ArrayDecl() { Location=startLoc, EndLocation=t.EndLocation }; 
				}

				ITypeDeclaration cd = null;

				// [ Type ]
				Lexer.PushLookAheadBackup();
				bool weaktype = AllowWeakTypeParsing;
				AllowWeakTypeParsing = true;

				var keyType = Type();

				AllowWeakTypeParsing = weaktype;

				if (keyType != null && laKind == CloseSquareBracket && !(keyType is IdentifierDeclaration))
				{
					//HACK: Both new int[size_t] as well as new int[someConstNumber] are legal. So better treat them as expressions.
					cd = new ArrayDecl() { KeyType = keyType, ClampsEmpty = false, Location = startLoc };
					Lexer.PopLookAheadBackup();
				}
				else
				{
					Lexer.RestoreLookAheadBackup();

					var fromExpression = AssignExpression();

					// [ AssignExpression .. AssignExpression ]
					if (laKind == DoubleDot)
					{
						Step();
						cd = new ArrayDecl() {
							Location=startLoc,
							ClampsEmpty=false,
							KeyType=null,
							KeyExpression= new PostfixExpression_Slice() { 
								FromExpression=fromExpression,
								ToExpression=AssignExpression()}};
					}
					else
						cd = new ArrayDecl() { KeyType=null, KeyExpression=fromExpression,ClampsEmpty=false,Location=startLoc };
				}

				if ((AllowWeakTypeParsing && laKind != CloseSquareBracket) || IsEOF)
					return null;

				Expect(CloseSquareBracket);
				if(cd!=null)
					cd.EndLocation = t.EndLocation;
				return cd;
			}

			// delegate | function
			else if (laKind == (Delegate) || laKind == (Function))
			{
				Step();
				var dd = new DelegateDeclaration() { Location=t.Location};
				dd.IsFunction = t.Kind == Function;

				var lpo = LastParsedObject;

				if (AllowWeakTypeParsing && laKind != OpenParenthesis)
					return null;

				dd.Parameters = Parameters(null);

				if (!IsEOF)
					LastParsedObject = lpo;

				var attributes = new List<DAttribute>();
				FunctionAttributes(ref attributes);
				dd.Modifiers= attributes.Count > 0 ? attributes.ToArray() : null;

				dd.EndLocation = t.EndLocation;
				return dd;
			}
			else
				SynErr(Identifier);
			return null;
		}

		/// <summary>
		/// Parses a type declarator
		/// </summary>
		/// <returns>A dummy node that contains the return type, the variable name and possible parameters of a function declaration</returns>
		DNode Declarator(ITypeDeclaration basicType,bool IsParam, INode parent)
		{
			DNode ret = new DVariable() { Type=basicType, Location = la.Location, Parent = parent };
			ApplyAttributes (ret);
			LastParsedObject = ret;

			while (IsBasicType2())
			{
				if (ret.Type == null) 
					ret.Type = BasicType2();
				else { 
					var ttd = BasicType2(); 
					if(ttd!=null)
						ttd.InnerDeclaration = ret.Type; 
					ret.Type = ttd; 
				}
			}

			// Only return no variable if the BasicType2 wasn't parsed properly to ensure good code completion
			if (IsEOF && ret != LastParsedObject)
				return null;

			if (laKind != (OpenParenthesis))
			{
				// On external function declarations, no parameter names are required.
				// extern void Cfoo(HANDLE,char**);
				if (IsParam && laKind != (Identifier))
				{
					if(ret.Type!=null && IsEOF)
						ExpectingNodeName = true;
					return ret;
				}

				if (Expect(Identifier))
				{
					ret.Name = t.Value;
					ret.NameLocation = t.Location;

					if (laKind == OpenParenthesis && ret.Type == null) {
						OverPeekBrackets (DTokens.OpenParenthesis, true);

						var k = Lexer.LastToken.Kind;
						if (k == DTokens.Alias || k == DTokens.Enum) {
							if (ret.Attributes == null)
								ret.Attributes = new List<DAttribute> ();
							ret.Attributes.Add (new Modifier (k));
						}

						// enum asdf(...) = ...;
						if (Lexer.CurrentPeekToken.Kind == Assign) {
							var eponymousTemplateDecl = new EponymousTemplate ();
							eponymousTemplateDecl.AssignFrom (ret);
							ret = eponymousTemplateDecl;

							TemplateParameterList (eponymousTemplateDecl);

							return ret;
						}
					}
				}
				else
				{
					if (IsEOF || IsParam)
					{
						ExpectingNodeName = true;
						return ret;
					}

					return null;
					// Code error! - to prevent infinite declaration loops, step one token forward anyway!
					if(laKind != CloseCurlyBrace && laKind != CloseParenthesis)
						Step();
					return null;
				}
			}
			else
				OldCStyleFunctionPointer(ret, IsParam);

			if (IsDeclaratorSuffix || IsFunctionAttribute)
			{
				var dm = new DMethod { Parent = parent, Parameters = null };
				dm.AssignFrom(ret);
				LastParsedObject = dm;

				DeclaratorSuffixes(dm);

				if (dm.Parameters != null)
					ret = dm;
				else
					LastParsedObject = ret;
			}

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
			LastParsedObject = cd;
			ret.Type = cd;
			var deleg = cd as DelegateDeclaration;

			/*			 
			 * Parse all basictype2's that are following the initial '('
			 */
			while (IsBasicType2())
			{
				var ttd = BasicType2();

				if (deleg.ReturnType == null) 
					deleg.ReturnType = ttd;
				else
				{
					if(ttd!=null)
						ttd.InnerDeclaration = deleg.ReturnType;
					deleg.ReturnType = ttd;
				}
			}

			/*			
			 * Here can be an identifier with some optional DeclaratorSuffixes
			 */
			if (laKind != (CloseParenthesis))
			{
				if (IsParam && laKind != (Identifier))
				{
					/* If this Declarator is a parameter of a function, don't expect anything here
					 * except a '*' that means that here's an anonymous function pointer
					 */
					if (t.Kind != (Times))
						SynErr(Times);
				}
				else
				{
					if(Expect(Identifier))
						ret.Name = t.Value;

					/*					
					 * Just here suffixes can follow!
					 */
					if (laKind != (CloseParenthesis))
					{
						DeclaratorSuffixes(ret);
					}
				}
			}
			ret.Type = cd;
			Expect(CloseParenthesis);
		}

		bool IsDeclaratorSuffix
		{
			get { return laKind == (OpenSquareBracket) || laKind == (OpenParenthesis); }
		}

		/// <summary>
		/// Note:
		/// http://www.digitalmars.com/d/2.0/declaration.html#DeclaratorSuffix
		/// The definition of a sequence of declarator suffixes is buggy here! Theoretically template parameters can be declared without a surrounding ( and )!
		/// Also, more than one parameter sequences are possible!
		/// 
		/// TemplateParameterList[opt] Parameters MemberFunctionAttributes[opt]
		/// </summary>
		void DeclaratorSuffixes(DNode dn)
		{
			FunctionAttributes(ref dn.Attributes);

			while (laKind == (OpenSquareBracket))
			{
				Step();
				var ad = new ArrayDecl() { Location=t.Location };
				LastParsedObject = ad;
				ad.InnerDeclaration = dn.Type;
				if (laKind != (CloseSquareBracket))
				{
					ad.ClampsEmpty = false;
					ITypeDeclaration keyType=null;
					Lexer.PushLookAheadBackup();
					if (!IsAssignExpression())
					{
						var weakType = AllowWeakTypeParsing;
						AllowWeakTypeParsing = true;
						
						keyType= ad.KeyType = Type();

						AllowWeakTypeParsing = weakType;
					}
					if (keyType == null || laKind != CloseSquareBracket)
					{
						Lexer.RestoreLookAheadBackup();
						keyType = ad.KeyType = null;
						ad.KeyExpression = AssignExpression();
					}
					else
						Lexer.PopLookAheadBackup();
				}
				Expect(CloseSquareBracket);
				ad.EndLocation = t.EndLocation;
				dn.Type = ad;
			}

			if (laKind == (OpenParenthesis))
			{
				if (IsTemplateParameterList())
				{
					TemplateParameterList(dn);
				}
				var dm = dn as DMethod;
				if(dm != null)
					dm.Parameters = Parameters(dm);
			}

			FunctionAttributes(ref dn.Attributes);
		}

		public ITypeDeclaration IdentifierList()
		{
			ITypeDeclaration td = null;

			bool notInit = false;
			do
			{
				if (notInit)
					Step();
				else
					notInit = true;

				ITypeDeclaration ttd;

				if (IsTemplateInstance)
					ttd = TemplateInstance(null);
				else if (Expect(Identifier))
					ttd = new IdentifierDeclaration(t.Value) { Location = t.Location, EndLocation = t.EndLocation };
				else if (IsEOF)
				{
					TrackerVariables.ExpectingIdentifier = true;
					return td == null ? null : new DTokenDeclaration(DTokens.INVALID, td);
				}
				else 
					ttd = null;
				if (ttd != null)
					ttd.InnerDeclaration = td;
				td = ttd;
			}
			while (laKind == Dot);

			return td;
		}

		bool IsStorageClass
		{
			get
			{
				return laKind == (Abstract) ||
			laKind == (Auto) ||
			((MemberFunctionAttribute[laKind]) && Lexer.CurrentPeekToken.Kind != (OpenParenthesis)) ||
			laKind == (Deprecated) ||
			laKind == (Extern) ||
			laKind == (Final) ||
			laKind == (Override) ||
			laKind == (Scope) ||
			laKind == (Static) ||
			laKind == (Synchronized) ||
			laKind == __gshared ||
					laKind == Ref ||
			IsAtAttribute;
			}
		}

		public ITypeDeclaration Type()
		{
			var td = BasicType();

			if (td != null && IsDeclarator2())
			{
				var ttd = Declarator2();
				if (ttd != null)
					ttd.InnerMost.InnerDeclaration = td;
				td = ttd;
			}

			return td;
		}

		bool IsDeclarator2()
		{
			return IsBasicType2() || laKind == (OpenParenthesis);
		}

		/// <summary>
		/// http://www.digitalmars.com/d/2.0/declaration.html#Declarator2
		/// The next bug: Following the definition strictly, this function would end up in an endless loop of requesting another Declarator2
		/// 
		/// So here I think that a Declarator2 only consists of a couple of BasicType2's and some DeclaratorSuffixes
		/// </summary>
		/// <returns></returns>
		ITypeDeclaration Declarator2()
		{
			ITypeDeclaration td = null;
			if (laKind == (OpenParenthesis))
			{
				Step();
				td = Declarator2();
				
				if (AllowWeakTypeParsing && (td == null||(t.Kind==OpenParenthesis && laKind==CloseParenthesis) /* -- means if an argumentless function call has been made, return null because this would be an expression */|| laKind!=CloseParenthesis))
					return null;

				Expect(CloseParenthesis);

				// DeclaratorSuffixes
				if (laKind == (OpenSquareBracket))
				{
					var dn = new DVariable();
					dn.Type = td;
					DeclaratorSuffixes(dn);
					td = dn.Type;

					if(dn.Attributes!= null && dn.Attributes.Count != 0)
						foreach(var attr in dn.Attributes)
							DeclarationAttributes.Push(attr);
				}
				return td;
			}

			while (IsBasicType2())
			{
				var ttd = BasicType2();
				if (AllowWeakTypeParsing && ttd == null)
					return null;

				if(ttd!=null)
					ttd.InnerDeclaration = td;
				td = ttd;
			}

			return td;
		}

		/// <summary>
		/// Parse parameters
		/// </summary>
		List<INode> Parameters(DMethod Parent)
		{
			var ret = new List<INode>();
			Expect(OpenParenthesis);

			// Empty parameter list
			if (laKind == (CloseParenthesis))
			{
				Step();
				return ret;
			}

			var stk_backup = BlockAttributes;
			BlockAttributes = new Stack<DAttribute>();

			DNode p;

			if (laKind != TripleDot && (p = Parameter(Parent)) != null)
			{
				p.Parent = Parent;
				ret.Add(p);
			}

			while (laKind == (Comma))
			{
				Step();
				if (laKind == TripleDot || laKind==CloseParenthesis || (p = Parameter(Parent)) == null)
					break;
				p.Parent = Parent;
				ret.Add(p);
			}

			// It's not specified in the official D syntax spec, but we treat id-only typed anonymous parameters as non-typed id-full parameters
			if(Parent != null && Parent.SpecialType == DMethod.MethodType.AnonymousDelegate)
			{
				foreach(var r in ret)
					if (r.NameHash == 0 && r.Type is IdentifierDeclaration && r.Type.InnerDeclaration == null)
					{
						r.NameHash = (r.Type as IdentifierDeclaration).IdHash;
						r.Type = null;
					}
			}

			/*
			 * There can be only one '...' in every parameter list
			 */
			if (laKind == TripleDot)
			{
				// If it doesn't have a comma, add a VarArgDecl to the last parameter
				bool HadComma = t.Kind == (Comma);

				Step();

				if (!HadComma && ret.Count > 0)
				{
					// Put a VarArgDecl around the type of the last parameter
					ret[ret.Count - 1].Type = new VarArgDecl(ret[ret.Count - 1].Type);
				}
				else
				{
					var dv = new DVariable();
					LastParsedObject = dv;
					dv.Type = new VarArgDecl();
					dv.Parent = Parent;
					ret.Add(dv);
				}
			}

			Expect(CloseParenthesis);
			BlockAttributes = stk_backup;
			return ret;
		}

		private DNode Parameter(IBlockNode Scope = null)
		{
			var attr = new List<DAttribute>();
			var startLocation = la.Location;

			CheckForStorageClasses (Scope as DBlockNode);

			while ((ParamModifiers[laKind] && laKind != InOut) || (MemberFunctionAttribute[laKind] && Lexer.CurrentPeekToken.Kind != OpenParenthesis))
			{
				Step();
				attr.Add(new Modifier(t.Kind));
			}

			if (laKind == Auto && Lexer.CurrentPeekToken.Kind == Ref) // functional.d:595 // auto ref F fp
			{
				Step();
				Step();
				attr.Add(new Modifier(Auto));
				attr.Add(new Modifier(Ref));
			}

			var td = BasicType();

			var ret = Declarator(td,true, Scope);
			if (ret == null)
				return null;
			ret.Location = startLocation;

			if (attr.Count > 0) {
				if(ret.Attributes == null)
					ret.Attributes = new List<DAttribute>(attr);
				else
					ret.Attributes.AddRange(attr);
			}
			
			// DefaultInitializerExpression
			if (laKind == (Assign))
			{
				Step();

				TrackerVariables.InitializedNode = ret;
				TrackerVariables.IsParsingInitializer = true;

				var defInit = AssignExpression(Scope);

				var dv = ret as DVariable;
				if (dv!=null)
					dv.Initializer = defInit;

				if (!IsEOF)
					TrackerVariables.IsParsingInitializer = false;
			}
			ret.EndLocation = t.EndLocation;

			return ret;
		}

		private IExpression Initializer(IBlockNode Scope = null)
		{
			Expect(Assign);

			// VoidInitializer
			if (laKind == Void)
			{
				Step();
				var ret= new VoidInitializer() { Location=t.Location,EndLocation=t.EndLocation};

				LastParsedObject = ret;
				return ret;
			}

			return NonVoidInitializer(Scope);
		}

		IExpression NonVoidInitializer(IBlockNode Scope = null)
		{
			var isParsingInitializer_backup = TrackerVariables.IsParsingInitializer;
			TrackerVariables.IsParsingInitializer = true;

			// ArrayInitializers are handled in PrimaryExpression(), whereas setting IsParsingInitializer to true is required!

			#region StructInitializer
			if (laKind == OpenCurlyBrace && IsStructInitializer)
			{
				// StructMemberInitializations
				var ae = new StructInitializer() { Location = la.Location };
				LastParsedObject = ae;
				var inits = new List<StructMemberInitializer>();

				bool IsInit = true;
				while (IsInit || laKind == (Comma))
				{
					Step();
					IsInit = false;

					// Allow empty post-comma expression IF the following token finishes the initializer expression
					// int[] a={1,2,3,4,};
					if (laKind == CloseCurlyBrace)
						break;

					// Identifier : NonVoidInitializer
					var sinit = new StructMemberInitializer { Location = la.Location };
					LastParsedObject = sinit;
					if (laKind == Identifier && Lexer.CurrentPeekToken.Kind == Colon)
					{
						Step();
						sinit.MemberName = t.Value;
						Step();
					}

					sinit.Value = NonVoidInitializer(Scope);

					sinit.EndLocation = t.EndLocation;

					inits.Add(sinit);
				}

				Expect(DTokens.CloseCurlyBrace);

				ae.MemberInitializers = inits.ToArray();
				ae.EndLocation = t.EndLocation;

				if (!IsEOF)
					TrackerVariables.IsParsingInitializer = isParsingInitializer_backup;

				return ae;
			}
			#endregion

			var expr= AssignExpression(Scope);

			if (!IsEOF)
				TrackerVariables.IsParsingInitializer = isParsingInitializer_backup;

			return expr;
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

		TypeOfDeclaration TypeOf()
		{
			Expect(Typeof);
			var md = new TypeOfDeclaration { Location = t.Location };
			LastParsedObject = md;

			if (Expect(OpenParenthesis))
			{
				if (laKind == Return)
				{
					Step();
					md.Expression = new TokenExpression(Return) { Location = t.Location, EndLocation = t.EndLocation };
				}
				else
					md.Expression = Expression();
				Expect(CloseParenthesis);
			}
			md.EndLocation = t.EndLocation;
			return md;
		}

		VectorDeclaration Vector()
		{
			var startLoc = t == null ? new CodeLocation() : t.Location;
			Expect(__vector);
			var md = new VectorDeclaration { Location = startLoc };

			if (Expect(OpenParenthesis))
			{
				LastParsedObject = md;

				if (!IsEOF)
					md.Id = Expression();

				if (Expect(CloseParenthesis))
					TrackerVariables.ExpectingIdentifier = false;
			}

			md.EndLocation = t.EndLocation;
			return md;
		}

		#endregion

		#region Attributes

		DMethod _Invariant()
		{
            var inv = new DMethod { SpecialType= DMethod.MethodType.ClassInvariant };
			LastParsedObject = inv;

			Expect(Invariant);
			inv.Location = t.Location;
			if (laKind == OpenParenthesis)
			{
				Step();
				Expect(CloseParenthesis);
			}
            if(!IsEOF)
			    inv.Body=BlockStatement();
			inv.EndLocation = t.EndLocation;
			return inv;
		}

		PragmaAttribute _Pragma()
		{
			Expect(Pragma);
			var s = new PragmaAttribute { Location = t.Location };
			LastParsedObject = s;
			if (Expect(OpenParenthesis))
			{
				if (Expect(Identifier))
					s.Identifier = t.Value;

				var l = new List<IExpression>();
				while (laKind == Comma)
				{
					Step();
					l.Add(AssignExpression());
				}
				if (l.Count > 0)
					s.Arguments = l.ToArray();

				if (Expect(CloseParenthesis))
					TrackerVariables.ExpectingIdentifier = false;
			}
			s.EndLocation = t.EndLocation;
			return s;
		}

		bool IsAttributeSpecifier()
		{
			return IsAttributeSpecifier(laKind, Lexer.CurrentPeekToken.Kind);
		}

		public static bool IsAttributeSpecifier(int laKind, int peekTokenKind=0)
		{
			return (laKind == (Extern) || laKind == (Export) || laKind == (Align) || laKind == Pragma || laKind == (Deprecated) || IsProtectionAttribute(laKind)
				|| (laKind == (Static) && peekTokenKind != If) || laKind == (Final) || laKind == (Override) || laKind == (Abstract) || laKind == (Scope) || laKind == (__gshared) || laKind==Synchronized
				|| ((laKind == (Auto) || MemberFunctionAttribute[laKind]) && (peekTokenKind != OpenParenthesis && peekTokenKind != Identifier))
				|| laKind == At);
		}
		
		/// <summary>
		/// True on e.g. @property or @"Hey ho my attribute"
		/// </summary>
		bool IsAtAttribute
		{
			get{
				return laKind == At;
			}
		}

		bool IsProtectionAttribute()
		{
			return IsProtectionAttribute(laKind);
		}

		public static bool IsProtectionAttribute(int laKind)
		{
			return laKind == (Public) || laKind == (Private) || laKind == (Protected) || laKind == (Extern) || laKind == (Package);
		}

		private void AttributeSpecifier(DBlockNode scope)
		{
			DAttribute attr;
			if(IsAtAttribute)
				attr = AtAttribute(scope);
			else if (laKind == Pragma)
				 attr=_Pragma();
			else if(laKind == Deprecated)
			{
				Step();
				var loc = t.Location;
				IExpression lc = null;
				if(laKind == OpenParenthesis)
				{
					Step();
					lc = AssignExpression(scope);
					Expect(CloseParenthesis);
				}
				attr = new DeprecatedAttribute(loc, t.EndLocation, lc);
			}
			else
			{
				var m = new Modifier(laKind, la.Value) { Location = la.Location };
				attr = m;
				LastParsedObject = attr;
				if (laKind == Extern && Lexer.CurrentPeekToken.Kind == OpenParenthesis)
				{
					Step(); // Skip extern
					Step(); // Skip (
	
					TrackerVariables.ExpectingIdentifier = true;
					var sb = new StringBuilder ();
					while (!IsEOF && laKind != CloseParenthesis)
					{
						Step();
						sb.Append(t.ToString());

						TrackerVariables.ExpectingIdentifier = false;
	
						if (t.Kind == Identifier && laKind == Identifier)
							sb.Append(' ');
					}
	
					m.LiteralContent = sb.ToString();
	
					if (!Expect(CloseParenthesis))
						return;
				}
				else if (laKind == Align && Lexer.CurrentPeekToken.Kind == OpenParenthesis)
				{
					Step();
					Step();
					if (Expect(Literal))
						m.LiteralContent = new IdentifierExpression(t.LiteralValue, t.LiteralFormat);
	
					if (!Expect(CloseParenthesis))
						return;
				}
				else
					Step();
	
				m.EndLocation = t.EndLocation;
			}

			if (laKind == Colon)
				LastParsedObject = null;

			//TODO: What about these semicolons after e.g. a pragma? Enlist these attributes anyway in the meta decl list?
			if (laKind != Semicolon)
				AttributeTrail(scope, attr);
		}
		
		/// <summary>
		/// Parses an attribute that starts with an @. Might be user-defined or a built-in attribute.
		/// Due to the fact that
		/// </summary>
		AtAttribute AtAttribute(IBlockNode scope)
		{
			var sl = la.Location;
			Expect(At);
			
			if(laKind == Identifier)
			{
				BuiltInAtAttribute.BuiltInAttributes att = 0;
				switch(la.Value)
				{
					case "safe":
						att = BuiltInAtAttribute.BuiltInAttributes.Safe;
					break;
					case "system":
						att = BuiltInAtAttribute.BuiltInAttributes.System;
					break;
					case "trusted":
						att = BuiltInAtAttribute.BuiltInAttributes.Trusted;
					break;
					case "property":
						att = BuiltInAtAttribute.BuiltInAttributes.Property;
					break;
					case "disable":
						att = BuiltInAtAttribute.BuiltInAttributes.Disable;
					break;
				}
				
				if(att != 0)
				{
					Step();
					return new BuiltInAtAttribute(att){Location = sl, EndLocation = t.EndLocation};
				}
			}
			else if(laKind == OpenParenthesis)
			{
				Step();
				var args = ArgumentList(scope);
				Expect(CloseParenthesis);
				return new UserDeclarationAttribute(args.ToArray()){Location = sl, EndLocation = t.EndLocation};
			}
			
			return new UserDeclarationAttribute(new[]{PostfixExpression(scope)}) { Location = sl, EndLocation = t.EndLocation };
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="module"></param>
		/// <param name="previouslyParsedAttribute"></param>
		/// <param name="RequireDeclDef">If no colon and no open curly brace is given as lookahead, a DeclDef may be parsed otherwise, if parameter is true.</param>
		/// <returns></returns>
		IMetaDeclaration AttributeTrail(DBlockNode module, DAttribute previouslyParsedAttribute, bool RequireDeclDef = false)
		{
			if (laKind == Colon)
			{
				Step();
				PushAttribute(previouslyParsedAttribute, true);

				AttributeMetaDeclarationSection metaDecl = null;
				//TODO: Put all remaining block/decl(?) attributes into the section definition..
				if(module!=null)
					module.Add(metaDecl = new AttributeMetaDeclarationSection(previouslyParsedAttribute) { EndLocation = t.EndLocation });
				return metaDecl;
			}
			else 
				PushAttribute(previouslyParsedAttribute, false);

			if (laKind == OpenCurlyBrace)
				return AttributeBlock(module);
			else
			{
				if (RequireDeclDef)
					DeclDef(module);
				return new AttributeMetaDeclaration(previouslyParsedAttribute) { EndLocation = previouslyParsedAttribute.EndLocation };
			}
		}
		
		bool IsFunctionAttribute
		{
			get{return MemberFunctionAttribute[laKind] || IsAtAttribute;}
		}

		void FunctionAttributes(DNode n)
		{
			FunctionAttributes(ref n.Attributes);
		}

		void FunctionAttributes(ref List<DAttribute> attributes)
		{
			DAttribute attr=null;
			if (attributes == null)
				attributes = new List<DAttribute>();
			while (IsFunctionAttribute)
			{
                if(IsAtAttribute)
                	attr = AtAttribute(null);
                else
                {
					attributes.Add(attr = new Modifier(laKind, la.Value) { Location = la.Location, EndLocation = la.EndLocation });
					Step();
                }
			}
			if(attr != null)
				LastParsedObject = attr;
		}
		#endregion

		#region Expressions
		public IExpression Expression(IBlockNode Scope = null)
		{
			// AssignExpression
			var ass = AssignExpression(Scope);
			if (laKind != (Comma))
				return ass;

			/*
			 * The following is a leftover of C syntax and proably cause some errors when parsing arguments etc.
			 */
			// AssignExpression , Expression
			var ae = new Expression();
			LastParsedObject = ae;
			ae.Add(ass);
			while (laKind == (Comma))
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
			if (IsBasicType())
			{
				bool HadPointerDeclaration = false;

				// uint[]** MyArray;
				if (!BasicTypes[laKind])
				{
					// Skip initial dot
					if (Peek(laKind == Dot ? 2 :1).Kind != Identifier)
					{
						if (laKind == Identifier)
						{
							// Skip initial identifier list
							if (Lexer.CurrentPeekToken.Kind == Not)
							{
								Peek();
								if (Lexer.CurrentPeekToken.Kind != Is && Lexer.CurrentPeekToken.Kind != In)
								{
									if (Lexer.CurrentPeekToken.Kind == (OpenParenthesis))
										OverPeekBrackets(OpenParenthesis);
									else
										Peek();

									if (Lexer.CurrentPeekToken.Kind == EOF) {
										Peek (1);
										return true;
									}
								}
							}

							while (Lexer.CurrentPeekToken.Kind == Dot)
							{
								Peek();

								if (Lexer.CurrentPeekToken.Kind == Identifier)
								{
									Peek();

									if (Lexer.CurrentPeekToken.Kind == Not)
									{
										Peek();
										if (Lexer.CurrentPeekToken.Kind != Is && Lexer.CurrentPeekToken.Kind != In)
										{
											if (Lexer.CurrentPeekToken.Kind == (OpenParenthesis))
												OverPeekBrackets(OpenParenthesis);
											else 
												Peek();
										}

										if (Lexer.CurrentPeekToken.Kind == EOF) {
											Peek (1);
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
						else if(Lexer.CurrentPeekToken.Kind == OpenParenthesis)
						{
							if(IsFunctionAttribute)
							{
								OverPeekBrackets(OpenParenthesis);
								bool isPrimitiveExpr = Lexer.CurrentPeekToken.Kind == Dot;
								Peek(1);
								return isPrimitiveExpr;
							}
							else if (laKind == Typeof)
								OverPeekBrackets(OpenParenthesis);
						}
					}
				}
				else if(Peek(1).Kind != Dot && Peek().Kind!=Identifier)
				{
					/*
					 * PrimaryExpression allows
					 * BasicType . Identifier 
					 * --> if BasicType IS int or float etc., and NO dot follows, it must be a type
					 */
					Peek(1);
					return false;
				}

				if (Lexer.CurrentPeekToken == null)
					Peek();

				// Skip basictype2's
				while (Lexer.CurrentPeekToken.Kind == Times || Lexer.CurrentPeekToken.Kind == OpenSquareBracket)
				{
					if (Lexer.CurrentPeekToken.Kind == Times)
						HadPointerDeclaration = true;

					if (Lexer.CurrentPeekToken.Kind == OpenSquareBracket)
						OverPeekBrackets(OpenSquareBracket);
					else Peek();

					if (HadPointerDeclaration && Lexer.CurrentPeekToken.Kind == Literal) // char[a.member*8] abc; // conv.d:3278
					{
						Peek(1);
						return true;
					}
				}

				// And now, after having skipped the basictype and possible trailing basictype2's,
				// we check for an identifier or delegate declaration to ensure that there's a declaration and not an expression
				// Addition: If a times token ('*') follows an identifier list, we can assume that we have a declaration and NOT an expression!
				// Example: *a=b is an expression; a*=b is not possible (and a Multiply-Assign-Expression) - instead something like A* a should be taken...
				if (HadPointerDeclaration || 
					Lexer.CurrentPeekToken.Kind == Identifier || 
					Lexer.CurrentPeekToken.Kind == Delegate || 
					Lexer.CurrentPeekToken.Kind == Function ||

					// Also assume a declaration if no further token follows
					Lexer.CurrentPeekToken.Kind==EOF ||
					Lexer.CurrentPeekToken.Kind==__EOF__)
				{
					Peek(1);
					return false;
				}
			}
			else if (IsStorageClass)
				return false;

			Peek(1);
			return true;
		}

		public IExpression AssignExpression(IBlockNode Scope = null)
		{
			var left = ConditionalExpression(Scope);
			if (Lexer.IsEOF)
			{
				if (!TrackerVariables.IsParsingAssignExpression)
				{
					LastParsedObject = left;
					TrackerVariables.IsParsingAssignExpression = true;
				}
				return left;
			}
			if (!AssignOps[laKind])
				return left;

			Step();
			var ate = new AssignExpression(t.Kind);
			LastParsedObject = ate;
			ate.LeftOperand = left;
			ate.RightOperand = AssignExpression(Scope);
			return ate;
		}

		IExpression ConditionalExpression(IBlockNode Scope = null)
		{
			var trigger = OrOrExpression(Scope);
			if (laKind != (Question))
				return trigger;

			Expect(Question);
			var se = new ConditionalExpression() { OrOrExpression = trigger };
			se.TrueCaseExpression = Expression(Scope);
			if (Expect (Colon))
				se.FalseCaseExpression = ConditionalExpression (Scope);
			if (!IsEOF)
				LastParsedObject = se;
			else
				TrackerVariables.IsParsingAssignExpression = true;
			return se;
		}

		IExpression OrOrExpression(IBlockNode Scope = null)
		{
			var left = AndAndExpression(Scope);
			if (laKind != LogicalOr)
				return left;

			Step();
			var ae = new OrOrExpression();
			LastParsedObject = ae;
			ae.LeftOperand = left;
			ae.RightOperand = OrOrExpression(Scope);
			return ae;
		}

		IExpression AndAndExpression(IBlockNode Scope = null)
		{
			// Note: Due to making it easier to parse, we ignore the OrExpression-CmpExpression rule
			// -> So we only assume that there's a OrExpression

			var left = OrExpression(Scope);
			if (laKind != LogicalAnd)
				return left;

			Step();
			var ae = new AndAndExpression();
			LastParsedObject = ae;
			ae.LeftOperand = left;
			ae.RightOperand = AndAndExpression(Scope);
			return ae;
		}

		IExpression OrExpression(IBlockNode Scope = null)
		{
			var left = XorExpression(Scope);
			if (laKind != BitwiseOr)
				return left;

			Step();
			var ae = new OrExpression(); LastParsedObject = ae;
			ae.LeftOperand = left;
			ae.RightOperand = OrExpression(Scope);
			return ae;
		}

		IExpression XorExpression(IBlockNode Scope = null)
		{
			var left = AndExpression(Scope);
			if (laKind != Xor)
				return left;

			Step();
			var ae = new XorExpression(); LastParsedObject = ae;
			ae.LeftOperand = left;
			ae.RightOperand = XorExpression(Scope);
			return ae;
		}

		IExpression AndExpression(IBlockNode Scope = null)
		{
			// Note: Since we ignored all kinds of CmpExpressions in AndAndExpression(), we have to take CmpExpression instead of ShiftExpression here!
			var left = CmpExpression(Scope);
			if (laKind != BitwiseAnd)
				return left;

			Step();
			var ae = new AndExpression(); LastParsedObject = ae;
			ae.LeftOperand = left;
			ae.RightOperand = AndExpression(Scope);
			return ae;
		}

		IExpression CmpExpression(IBlockNode Scope = null)
		{
			var left = ShiftExpression(Scope);

			OperatorBasedExpression ae = null;

			// Equality Expressions
			if (laKind == Equal || laKind == NotEqual)
				ae = new EqualExpression(laKind == NotEqual);

			// Relational Expressions
			else if (RelationalOperators[laKind])
				ae = new RelExpression(laKind);

			// Identity Expressions
			else if (laKind == Is || (laKind == Not && Peek(1).Kind == Is))
				ae = new IdendityExpression(laKind == Not);

			// In Expressions
			else if (laKind == In || (laKind == Not && Peek(1).Kind == In))
				ae = new InExpression(laKind == Not);

			else return left;

			LastParsedObject = ae;

			// Skip possible !-Token
			if (laKind == Not)
				Step();

			// Skip operator
			Step();

			ae.LeftOperand = left;
			ae.RightOperand = ShiftExpression(Scope);
			return ae;
		}

		IExpression ShiftExpression(IBlockNode Scope = null)
		{
			var left = AddExpression(Scope);
			if (!(laKind == ShiftLeft || laKind == ShiftRight || laKind == ShiftRightUnsigned))
				return left;

			Step();
			var ae = new ShiftExpression(t.Kind); LastParsedObject = ae;
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
				case Plus:
				case Minus:
					ae = new AddExpression(laKind == Minus);
					break;
				case Tilde:
					ae = new CatExpression();
					break;
				case Times:
				case Div:
				case Mod:
					ae = new MulExpression(laKind);
					break;
				default:
					return left;
			}

			LastParsedObject = ae;

			Step();

			ae.LeftOperand = left;
			ae.RightOperand = AddExpression(Scope);
			return ae;
		}

		IExpression UnaryExpression(IBlockNode Scope = null)
		{
			// Note: PowExpressions are handled in PowExpression()

			if (laKind == (BitwiseAnd) || laKind == (Increment) ||
				laKind == (Decrement) || laKind == (Times) ||
				laKind == (Minus) || laKind == (Plus) ||
				laKind == (Not) || laKind == (Tilde))
			{
				Step();

				SimpleUnaryExpression ae;

				switch (t.Kind)
				{
					case BitwiseAnd:
						ae = new UnaryExpression_And();
						break;
					case Increment:
						ae = new UnaryExpression_Increment();
						break;
					case Decrement:
						ae = new UnaryExpression_Decrement();
						break;
					case Times:
						ae = new UnaryExpression_Mul();
						break;
					case Minus:
						ae = new UnaryExpression_Sub();
						break;
					case Plus:
						ae = new UnaryExpression_Add();
						break;
					case Tilde:
						ae = new UnaryExpression_Cat();
						break;
					case Not:
						ae = new UnaryExpression_Not();
						break;
					default:
						SynErr(t.Kind, "Illegal token for unary expressions");
						return null;
				}

				LastParsedObject = ae;

				ae.Location = t.Location;

				ae.UnaryExpression = UnaryExpression(Scope);

				return ae;
			}

			// CastExpression
			if (laKind == (Cast))
			{
				Step();
				var ae = new CastExpression { Location= t.Location };

				if (Expect(OpenParenthesis))
				{
					if (laKind != CloseParenthesis) // Yes, it is possible that a cast() can contain an empty type!
						ae.Type = Type();
					Expect(CloseParenthesis);
				}

				ae.UnaryExpression = UnaryExpression(Scope);

				ae.EndLocation = t.EndLocation;

				return ae;
			}

			// DeleteExpression
			if (laKind == (Delete))
			{
				Step();
				return new DeleteExpression() { UnaryExpression = UnaryExpression(Scope) };
			}

			// PowExpression
			var left = PostfixExpression(Scope);

			if (laKind != Pow)
				return left;

			Step();
			var pe = new PowExpression();
			pe.LeftOperand = left;
			pe.RightOperand = UnaryExpression(Scope);

			return pe;
		}

		IExpression NewExpression(IBlockNode Scope = null)
		{
			Expect(New);
			var startLoc = t.Location;

			IExpression[] newArgs = null;
			// NewArguments
			if (laKind == (OpenParenthesis))
			{
				Step();
				if (laKind != (CloseParenthesis))
					newArgs = ArgumentList(Scope).ToArray();
				Expect(CloseParenthesis);
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
			if (laKind == (Class))
			{
				Step();
				var ac = new AnonymousClassExpression(); LastParsedObject = ac;
				ac.NewArguments = newArgs;

				// ClassArguments
				if (laKind == (OpenParenthesis))
				{
					Step();
					if (laKind == (CloseParenthesis))
						Step();
					else
						ac.ClassArguments = ArgumentList(Scope).ToArray();
				}

				var anclass = new DClassLike(Class) { IsAnonymousClass=true };
				LastParsedObject = anclass;

				anclass.Name = "(Anonymous Class)";

				// BaseClasslist_opt
				if (laKind == (Colon))
				{
					//TODO : Add base classes to expression
					BaseClassList(anclass);
				}
				// SuperClass_opt InterfaceClasses_opt
				else if (laKind != OpenCurlyBrace)
					BaseClassList(anclass,false);

				ClassBody(anclass);

				ac.AnonymousClass = anclass;

				ac.Location = startLoc;
				ac.EndLocation = t.EndLocation;

				if (Scope != null)
					Scope.Add(ac.AnonymousClass);

				return ac;
			}

			// NewArguments Type
			else
			{
				var nt = BasicType();

				while (IsBasicType2())
				{
					var bt=BasicType2();
					if (bt == null)
						break;
					bt.InnerDeclaration = nt;
					nt = bt;
				}

				var initExpr = new NewExpression()
				{
					NewArguments = newArgs,
					Type=nt,
					Location=startLoc
				};
				if(!IsEOF)
					LastParsedObject = initExpr;

				var args = new List<IExpression>();

				var ad=nt as ArrayDecl;

				if ((ad == null || ad.ClampsEmpty) && laKind == OpenParenthesis)
				{
					Step();
					if (laKind != CloseParenthesis)
						args = ArgumentList(Scope);

					if (Expect(CloseParenthesis))
						initExpr.EndLocation = t.EndLocation;
					else
						initExpr.EndLocation = CodeLocation.Empty;

					if (ad != null)
					{
						if (args.Count == 0)
						{
							SemErr(CloseParenthesis, "Size for the rightmost array dimension needed");

							initExpr.EndLocation = t.EndLocation;
							return initExpr;
						}

						while (ad != null)
						{
							if (args.Count == 0)
								break;

							ad.ClampsEmpty = false;
							ad.KeyType = null;
							ad.KeyExpression = args[args.Count - 1];

							args.RemoveAt(args.Count - 1);

							ad = ad.InnerDeclaration as ArrayDecl;
						}
					}
				}
				else
					initExpr.EndLocation = t.EndLocation;

				ad = nt as ArrayDecl;

				if (ad != null && ad.KeyExpression == null)
				{
					if (ad.KeyType != null)
						SemErr(ad.KeyType is DTokenDeclaration ? (ad.KeyType as DTokenDeclaration).Token : CloseSquareBracket, "Size of array expected, not type " + ad.KeyType);
				}

				initExpr.Arguments = args.ToArray();

				return initExpr;
			}
		}

		public List<IExpression> ArgumentList(IBlockNode Scope = null)
		{
			var ret = new List<IExpression>();

			ret.Add(AssignExpression(Scope));

			while (laKind == (Comma))
			{
				Step();
				if (laKind == CloseParenthesis)
					break;
				ret.Add(AssignExpression(Scope));
			}

			return ret;
		}

		IExpression PostfixExpression(IBlockNode Scope = null)
		{
			var curLastParsedObj = LastParsedObject;
			IExpression leftExpr = null;

			/*
			 * Despite the following syntax is an explicit UnaryExpression (see http://dlang.org/expression.html#UnaryExpression),
			 * stuff like (MyType).init[] is actually allowed - so it's obviously a PostfixExpression! (Nov 13 2013)
			 */

			// ( Type ) . Identifier
			if (laKind == OpenParenthesis)
			{
				Lexer.StartPeek();
				OverPeekBrackets(OpenParenthesis, false);
				var dotToken = Lexer.CurrentPeekToken;

				if (Lexer.CurrentPeekToken.Kind == DTokens.Dot && 
					(Peek().Kind == DTokens.Identifier || Lexer.CurrentPeekToken.Kind == EOF))
				{
					var wkParsing = AllowWeakTypeParsing;
					AllowWeakTypeParsing = true;
					Lexer.PushLookAheadBackup();
					Step();
					var startLoc = t.Location;

					var td = Type();

					AllowWeakTypeParsing = wkParsing;

					/*				
					 * (a. -- expression: (a.myProp + 2) / b;
					 * (int. -- must be expression anyway
					 * (const).asdf -- definitely unary expression ("type")
					 * (const). -- also treat it as type accessor
					 */
					if (td != null && 
						laKind == CloseParenthesis && Lexer.CurrentPeekToken == dotToken) // Also take it as a type declaration if there's nothing following (see Expression Resolving)
					{
						Step();  // Skip to )
						if (laKind == DTokens.Dot)
						{
							Step();  // Skip to .
							if (laKind == DTokens.Identifier || IsEOF)
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
			if(leftExpr == null)
				leftExpr = PrimaryExpression(Scope);
			
			if(curLastParsedObj==LastParsedObject)
				LastParsedObject = leftExpr;

			while (!IsEOF)
			{
				if (laKind == Dot)
				{
					Step();

					var e = new PostfixExpression_Access { 
						PostfixForeExpression=leftExpr
					};
					LastParsedObject = e;

					leftExpr = e;

					if (laKind == New)
						e.AccessExpression = PostfixExpression(Scope);
					else if (IsTemplateInstance)
						e.AccessExpression = TemplateInstance(Scope);
					else if (Expect(Identifier))
                        e.AccessExpression = new IdentifierExpression(t.Value) { Location=t.Location, EndLocation=t.EndLocation };

					e.EndLocation = t.EndLocation;
				}
				else if (laKind == Increment || laKind == Decrement)
				{
					Step();
					var e = t.Kind == Increment ? (PostfixExpression)new PostfixExpression_Increment() : new PostfixExpression_Decrement();
					LastParsedObject = e;
					e.EndLocation = t.EndLocation;					
					e.PostfixForeExpression = leftExpr;
					leftExpr = e;
				}

				// Function call
				else if (laKind == OpenParenthesis)
				{
					Step();
					var ae = new PostfixExpression_MethodCall();
					LastParsedObject = ae;
					ae.PostfixForeExpression = leftExpr;
					leftExpr = ae;
					
					if (laKind == CloseParenthesis)
						Step();
					else
					{
						ae.Arguments = ArgumentList(Scope).ToArray();
						Expect(CloseParenthesis);
					}
					
					if(IsEOF)
						ae.EndLocation = CodeLocation.Empty;
					else
						ae.EndLocation = t.EndLocation;
				}

				// IndexExpression | SliceExpression
				else if (laKind == OpenSquareBracket)
				{
					Step();

					if (laKind != CloseSquareBracket)
					{
						var firstEx = AssignExpression(Scope);
						// [ AssignExpression .. AssignExpression ]
						if (laKind == DoubleDot)
						{
							Step();

							leftExpr = new PostfixExpression_Slice()
							{
								FromExpression = firstEx,
								PostfixForeExpression = leftExpr,
								ToExpression = AssignExpression(Scope)
							};
							LastParsedObject = leftExpr;
						}
						// [ ArgumentList ]
						else if (laKind == CloseSquareBracket || laKind == (Comma))
						{
							var args = new List<IExpression>();
							args.Add(firstEx);
							if (laKind == Comma)
							{
								Step();
								args.AddRange(ArgumentList(Scope));
							}

							leftExpr = new PostfixExpression_Index()
							{
								PostfixForeExpression = leftExpr,
								Arguments = args.ToArray()
							};
							LastParsedObject = leftExpr;
						}
					}
					else // Empty array literal = SliceExpression
					{
						leftExpr = new PostfixExpression_Slice()
						{
							PostfixForeExpression=leftExpr
						}; 
						LastParsedObject = leftExpr;
					}

					Expect(CloseSquareBracket);
					if(leftExpr is PostfixExpression)
						((PostfixExpression)leftExpr).EndLocation = t.EndLocation;
				}
				else break;
			}

			return leftExpr;
		}

		IExpression PrimaryExpression(IBlockNode Scope=null)
		{
			bool isModuleScoped = laKind == Dot;
			if (isModuleScoped)
			{
				Step();
				if (IsEOF)
				{
					LastParsedObject = new TokenExpression(Dot) { Location = t.Location, EndLocation = t.EndLocation };
					TrackerVariables.ExpectingIdentifier = true;
				}
			}

			// Dollar (== Array length expression)
			if (laKind == Dollar || DTokens.MetaIdentifiers[laKind])
			{
				Step();
				return new TokenExpression(t.Kind)
				{
					Location = t.Location,
					EndLocation = t.EndLocation
				};
			}

			// TemplateInstance
			if (IsTemplateInstance)
			{
				var tix = TemplateInstance(Scope);
				if (tix != null)
					tix.ModuleScopedIdentifier = isModuleScoped;
				return tix;
			}

			if (IsLambaExpression())
				return LambaExpression(Scope);

			// Identifier
			if (laKind == Identifier)
			{
				Step();

				return new IdentifierExpression(t.Value)
					{
						Location = t.Location,
						EndLocation = t.EndLocation,
						ModuleScoped = isModuleScoped
					};
			}

			// SpecialTokens (this,super,null,true,false,$) // $ has been handled before
			if (laKind == (This) || laKind == (Super) || laKind == (Null) || laKind == (True) || laKind == (False))
			{
				Step();
				return new TokenExpression(t.Kind)
				{
					Location = t.Location,
					EndLocation = t.EndLocation
				};
			}

			#region Literal
			if (laKind == Literal)
			{
				Step();
				var startLoc = t.Location;

				// Concatenate multiple string literals here
				if (t.LiteralFormat == LiteralFormat.StringLiteral || t.LiteralFormat == LiteralFormat.VerbatimStringLiteral)
				{
					var sb = new StringBuilder(t.RawCodeRepresentation ?? t.Value);
					while (la.LiteralFormat == LiteralFormat.StringLiteral || la.LiteralFormat == LiteralFormat.VerbatimStringLiteral)
					{
						Step();
						sb.Append(t.RawCodeRepresentation ?? t.Value);
					}
					return new IdentifierExpression(sb.ToString(), t.LiteralFormat, t.Subformat) { Location = startLoc, EndLocation = t.EndLocation };
				}
				//else if (t.LiteralFormat == LiteralFormat.CharLiteral)return new IdentifierExpression(t.LiteralValue) { LiteralFormat=t.LiteralFormat,Location = startLoc, EndLocation = t.EndLocation };
				return new IdentifierExpression(t.LiteralValue, t.LiteralFormat, t.Subformat) { Location = startLoc, EndLocation = t.EndLocation };
			}
			#endregion

			#region ArrayLiteral | AssocArrayLiteral
			if (laKind == (OpenSquareBracket))
			{
				Step();
				var startLoc = t.Location;

				// Empty array literal
				if (laKind == CloseSquareBracket)
				{
					Step();
					return new ArrayLiteralExpression(null) {Location=startLoc, EndLocation = t.EndLocation };
				}

				/*
				 * If it's an initializer, allow NonVoidInitializers as values.
				 * Normal AssignExpressions otherwise.
				 */
				bool isInitializer = TrackerVariables.IsParsingInitializer;

				var firstExpression = isInitializer? NonVoidInitializer(Scope) : AssignExpression(Scope);

				// Associtative array
				if (laKind == Colon)
				{
					Step();

					var ae = isInitializer ?
						new ArrayInitializer { Location = startLoc } : 
						new AssocArrayExpression { Location=startLoc };
					LastParsedObject = ae;

					var firstValueExpression = isInitializer? NonVoidInitializer(Scope) : AssignExpression();

					ae.Elements.Add(new KeyValuePair<IExpression,IExpression>(firstExpression, firstValueExpression));

					while (laKind == Comma)
					{
						Step();

						if (laKind == CloseSquareBracket)
							break;

						var keyExpr = AssignExpression();
						var valExpr=Expect(Colon) ? 
							(isInitializer? 
								NonVoidInitializer(Scope) : 
								AssignExpression(Scope)) : 
							null;

						ae.Elements.Add(new KeyValuePair<IExpression,IExpression>(keyExpr,valExpr));
					}

					Expect(CloseSquareBracket);
					ae.EndLocation = t.EndLocation;
					return ae;
				}
				else // Normal array literal
				{
					var ae = new List<IExpression>();
					LastParsedObject = ae;
					
					ae.Add(firstExpression);

					while (laKind == Comma)
					{
						Step();
						if (laKind == CloseSquareBracket) // And again, empty expressions are allowed
							break;
						ae.Add(isInitializer? NonVoidInitializer(Scope) : AssignExpression(Scope));
					}

					Expect(CloseSquareBracket);
					return new ArrayLiteralExpression(ae){ Location=startLoc, EndLocation = t.EndLocation };
				}
			}
			#endregion

			#region FunctionLiteral
			if (laKind == Delegate || laKind == Function || laKind == OpenCurlyBrace || (laKind == OpenParenthesis && IsFunctionLiteral()))
			{
				var fl = new FunctionLiteral() { Location=la.Location};
				LastParsedObject = fl;
				fl.AnonymousMethod.Location = la.Location;

				if (laKind == Delegate || laKind == Function)
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
				if (laKind != OpenCurlyBrace) // foo( 1, {bar();} ); -> is a legal delegate
				{
					if (!IsFunctionAttribute && Lexer.CurrentPeekToken.Kind == OpenParenthesis)
						fl.AnonymousMethod.Type = BasicType();
					else if (laKind != OpenParenthesis && laKind != OpenCurlyBrace)
						fl.AnonymousMethod.Type = Type();

					if (laKind == OpenParenthesis)
						fl.AnonymousMethod.Parameters = Parameters(fl.AnonymousMethod);

					FunctionAttributes(fl.AnonymousMethod);
				}
				
				if(!IsEOF)
					FunctionBody(fl.AnonymousMethod);

				fl.EndLocation = t.EndLocation;

				if (Scope != null)
					Scope.Add(fl.AnonymousMethod);

				return fl;
			}
			#endregion

			#region AssertExpression
			if (laKind == (Assert))
			{
				Step();
				var startLoc = t.Location;
				Expect(OpenParenthesis);
				var ce = new AssertExpression() { Location=startLoc};
				LastParsedObject = ce;

				var exprs = new List<IExpression>();
				exprs.Add(AssignExpression());

				if (laKind == (Comma))
				{
					Step();
					exprs.Add(AssignExpression());
				}
				ce.AssignExpressions = exprs.ToArray();
				Expect(CloseParenthesis);
				ce.EndLocation = t.EndLocation;
				return ce;
			}
			#endregion

			#region MixinExpression | ImportExpression
			if (laKind == Mixin)
			{
				Step();
				var e = new MixinExpression() { Location=t.Location};
				LastParsedObject = e;
				if (Expect(OpenParenthesis))
				{
					e.AssignExpression = AssignExpression();
					Expect(CloseParenthesis);
				}
				e.EndLocation = t.EndLocation;
				return e;
			}

			if (laKind == Import)
			{
				Step();
				var e = new ImportExpression() { Location=t.Location};
				LastParsedObject = e;
				Expect(OpenParenthesis);

				e.AssignExpression = AssignExpression();

				Expect(CloseParenthesis);
				e.EndLocation = t.EndLocation;
				return e;
			}
			#endregion

			if (laKind == (Typeof))
			{
				return new TypeDeclarationExpression(TypeOf());
			}

			// TypeidExpression
			if (laKind == (Typeid))
			{
				Step();
				var ce = new TypeidExpression() { Location=t.Location};
				LastParsedObject = ce;
				Expect(OpenParenthesis);

				if (IsAssignExpression())
					ce.Expression = AssignExpression(Scope);
				else
				{
					Lexer.PushLookAheadBackup();
					AllowWeakTypeParsing = true;
					ce.Type = Type();
					AllowWeakTypeParsing = false;

					if (ce.Type == null || laKind != CloseParenthesis)
					{
						Lexer.RestoreLookAheadBackup();
						ce.Expression = AssignExpression();
					}
					else
						Lexer.PopLookAheadBackup();
				}

				if (!Expect(CloseParenthesis) && IsEOF)
					LastParsedObject = (ISyntaxRegion)ce.Type ?? ce.Expression;

				ce.EndLocation = t.EndLocation;
				return ce;
			}

			#region IsExpression
			if (laKind == Is)
			{
				Step();
				var ce = new IsExpression() { Location=t.Location};
				LastParsedObject = ce;
				Expect(OpenParenthesis);

				if((ce.TestedType = Type())==null)
					SynErr(laKind, "In an IsExpression, either a type or an expression is required!");

				if (ce.TestedType!=null && laKind == Identifier && (Lexer.CurrentPeekToken.Kind == CloseParenthesis || Lexer.CurrentPeekToken.Kind == Equal
					|| Lexer.CurrentPeekToken.Kind == Colon))
				{
					Step();
					Strings.Add(strVal);
					ce.TypeAliasIdentifierHash = strVal.GetHashCode();
					ce.TypeAliasIdLocation = t.Location;
				}

				if (laKind == CloseParenthesis)
				{
					Step();
					ce.EndLocation = t.EndLocation;
					return ce;
				}

				if (laKind == Colon || laKind == Equal)
				{
					Step();
					ce.EqualityTest = t.Kind == Equal;
				}
				else if (laKind == CloseParenthesis)
				{
					Step();
					ce.EndLocation = t.EndLocation;
					return ce;
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

				if (ce.EqualityTest && (ClassLike[laKind] || laKind==Typedef || // typedef is possible although it's not yet documented in the syntax docs
					laKind==Enum || laKind==Delegate || laKind==Function || laKind==Super || laKind==Return ||
					((laKind==Const || laKind == Immutable || laKind == InOut || laKind == Shared) && 
					(Peek(1).Kind==CloseParenthesis || Lexer.CurrentPeekToken.Kind==Comma))))
				{
					Step();
					ce.TypeSpecializationToken = t.Kind;
				}
				else
					ce.TypeSpecialization = Type();

				// TemplateParameterList
				if (laKind == Comma)
				{
					var ret = new List<TemplateParameter>();
					do
					{
						Step();
						ret.Add(TemplateParameter(null));
					}
					while (laKind == Comma);
					ce.TemplateParameterList = ret.ToArray();
				}

				Expect(CloseParenthesis);
				ce.EndLocation = t.EndLocation;
				return ce;
			}
			#endregion

			// NewExpression
			if (laKind == (New))
				return NewExpression(Scope);

			// ( Expression )
			if (laKind == OpenParenthesis)
			{
				Step();
				var ret = new SurroundingParenthesesExpression() {Location=t.Location };
				LastParsedObject = ret;

				ret.Expression = Expression();

				Expect(CloseParenthesis);
				ret.EndLocation = t.EndLocation;
				return ret;
			}

			// TraitsExpression
			if (laKind == (__traits))
				return TraitsExpression();

			#region BasicType . Identifier
			if (IsBasicType())
			{
				var startLoc = la.Location;

				var bt=BasicType();

				if ((bt is TypeOfDeclaration || bt is MemberFunctionAttributeDecl) && laKind!=Dot)
					return new TypeDeclarationExpression(bt);
				
				// Things like incomplete 'float.' expressions shall be parseable, too
				if (Expect(Dot) && (Expect(Identifier) || IsEOF))
                    return new PostfixExpression_Access()
                    {
                        PostfixForeExpression = new TypeDeclarationExpression(bt),
                        AccessExpression = string.IsNullOrEmpty(t.Value) ? null : new IdentifierExpression(t.Value) { Location=t.Location, EndLocation=t.EndLocation },
                        EndLocation = t.EndLocation
                    };

				return null;
			}
			#endregion

			SynErr(Identifier);
			if(laKind != CloseCurlyBrace)
				Step();

			// Don't know why, in rare situations, t tends to be null..
			if (t == null)
				return null;
			return new TokenExpression() { Location = t.Location, EndLocation = t.EndLocation };
		}

		bool IsLambaExpression()
		{
			Lexer.StartPeek();
			
			if(laKind == Function || laKind == Delegate)
				Lexer.Peek();
			
			if (Lexer.CurrentPeekToken.Kind != OpenParenthesis)
			{
				if (Lexer.CurrentPeekToken.Kind == Identifier && Peek().Kind == GoesTo)
					return true;

				return false;
			}

			OverPeekBrackets(OpenParenthesis, false);

			return Lexer.CurrentPeekToken.Kind == GoesTo;
		}

		bool IsFunctionLiteral()
		{
			if (laKind != OpenParenthesis)
				return false;

			Lexer.StartPeek();

			OverPeekBrackets(OpenParenthesis, false);

			bool at = false;
			while (DTokens.StorageClass [Lexer.CurrentPeekToken.Kind] || (at = Lexer.CurrentPeekToken.Kind == At)) {
				Lexer.Peek ();
				if (at)
					Lexer.Peek ();
				if (Lexer.CurrentPeekToken.Kind == OpenParenthesis)
					OverPeekBrackets (OpenParenthesis, false);
			}

			return Lexer.CurrentPeekToken.Kind == OpenCurlyBrace;
		}

		FunctionLiteral LambaExpression(IBlockNode Scope=null)
		{
			var fl = new FunctionLiteral(true);
			
			fl.Location = fl.AnonymousMethod.Location = la.Location;
			
			if(laKind == Function || laKind == Delegate)
			{
				fl.LiteralToken = laKind;
				Step();
			}

			if (laKind == Identifier)
			{
				Step();

				var p = new DVariable { 
					Name = t.Value, 
					Location = t.Location, 
					EndLocation = t.EndLocation,
					Attributes =  new List<DAttribute>{new Modifier(Auto)}
				};

				fl.AnonymousMethod.Parameters.Add(p);
			}
			else if (laKind == OpenParenthesis)
				fl.AnonymousMethod.Parameters = Parameters(fl.AnonymousMethod);



			if (Expect(GoesTo))
			{
				if (laKind == OpenCurlyBrace)
					fl.AnonymousMethod.Body = BlockStatement (fl.AnonymousMethod);
				else
					LambdaSingleStatementBody (fl.AnonymousMethod);
			}

			fl.EndLocation = fl.AnonymousMethod.EndLocation = t.EndLocation;

			if (Scope != null)
				Scope.Add(fl.AnonymousMethod);

			return fl;
		}

		internal void LambdaSingleStatementBody(DMethod lambdaFunction)
		{
			lambdaFunction.Body = new BlockStatement { Location= la.Location, ParentNode = lambdaFunction };

			var ae = AssignExpression(lambdaFunction);

			lambdaFunction.Body.Add(new ReturnStatement
				{
					Location = ae.Location,
					EndLocation = ae.EndLocation,
					ReturnExpression=ae
				});

			lambdaFunction.Body.EndLocation = t.EndLocation;
		}
		#endregion

		#region Statements
		void IfCondition(IfStatement par)
		{
			var wkType = AllowWeakTypeParsing;
			AllowWeakTypeParsing = true;
			
			Lexer.PushLookAheadBackup();

			ITypeDeclaration tp;
			if (laKind == Auto)
			{
				Step();
				tp = new DTokenDeclaration(Auto) { Location=t.Location, EndLocation=t.EndLocation };
			}
			else
				tp = Type();

			AllowWeakTypeParsing = wkType;

			if (tp != null && laKind == Identifier)
			{
				var dv = Declarator(tp, false, par.ParentNode) as DVariable;
				if (dv == null)
				{
					SynErr(t.Kind, "Invalid node type! - Variable expected!");
					return;
				}

				if (laKind == Assign)
				{
					Step ();
					dv.Location = tp.Location;
					dv.Initializer = Expression();
					dv.EndLocation = t.EndLocation;

					par.IfVariable = dv;
					return;
				}
			}

			if (IsEOF && tp != null && !(tp is IdentifierDeclaration || 
			                             tp is DTokenDeclaration && (tp as DTokenDeclaration).Token == DTokens.INVALID))
			{
				/*
				 * Ambigious situation: if(e| -- is this an inline declaration or the start of a postfix_access expression á la 'e.'? 
				 */
				TrackerVariables.ExpectingNodeName = true;
			}
			else
			{
				Lexer.RestoreLookAheadBackup();
				par.IfCondition = Expression();
			}
		}

		public bool IsStatement
		{
			get {
				return laKind == OpenCurlyBrace ||
					(laKind == Identifier && Peek(1).Kind == Colon) ||
					laKind == If || (laKind == Static && 
						(Lexer.CurrentPeekToken.Kind == If || 
						Lexer.CurrentPeekToken.Kind==Assert)) ||
					laKind == While || laKind == Do ||
					laKind == For ||
					laKind == Foreach || laKind == Foreach_Reverse ||
					(laKind == Final && Lexer.CurrentPeekToken.Kind == Switch) || laKind == Switch ||
					laKind == Case || laKind == Default ||
					laKind == Continue || laKind == Break||
					laKind==Return ||
					laKind==Goto ||
					laKind==With||
					laKind==Synchronized||
					laKind==Try||
					laKind==Throw||
					laKind==Scope||
					laKind==Asm||
					laKind==Pragma||
					laKind==Mixin||
					laKind==Version||
					laKind==Debug||
					laKind==Assert||
					laKind==Volatile
					;
			}
		}

		public IStatement Statement(bool BlocksAllowed = true, bool EmptyAllowed = true, IBlockNode Scope = null, IStatement Parent=null)
		{
			if (EmptyAllowed && laKind == Semicolon)
			{
				LastParsedObject = null;
				Step();
				return null;
			}

			if (BlocksAllowed && laKind == OpenCurlyBrace)
				return BlockStatement(Scope,Parent);

			#region LabeledStatement (loc:... goto loc;)
			if (laKind == Identifier && Lexer.CurrentPeekToken.Kind == Colon)
			{
				Step();

				var ret = new LabeledStatement() { Location = t.Location, Identifier = t.Value, Parent = Parent };
				LastParsedObject = null;
				Step();
				ret.EndLocation = t.EndLocation;

				return ret;
			}
			#endregion

			#region IfStatement
			else if (laKind == (If))
			{
				Step();

				var dbs = new IfStatement{	Location = t.Location, Parent = Parent	};

				LastParsedObject = dbs;
				Expect(OpenParenthesis);

				// IfCondition
				IfCondition(dbs);

				// ThenStatement
				if(Expect(CloseParenthesis))
					dbs.ThenStatement = Statement(Scope: Scope, Parent: dbs);

				// ElseStatement
				if (laKind == (Else))
				{
					Step();
					dbs.ElseStatement = Statement(Scope: Scope, Parent: dbs);
				}

				if(t != null)
					dbs.EndLocation = t.EndLocation;

				return dbs;
			}
			#endregion

			#region Conditions
			else if ((laKind == Static && Lexer.CurrentPeekToken.Kind == If) || laKind == Version || laKind == Debug)
				return StmtCondition(Parent, Scope);
			#endregion

			#region WhileStatement
			else if (laKind == While)
			{
				Step();

				var dbs = new WhileStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = dbs;
				Expect(OpenParenthesis);
				dbs.Condition = Expression(Scope);
				Expect(CloseParenthesis);

				if(!IsEOF)
				{
					dbs.ScopedStatement = Statement(Scope: Scope, Parent: dbs);
					dbs.EndLocation = t.EndLocation;
				}

				return dbs;
			}
			#endregion

			#region DoStatement
			else if (laKind == (Do))
			{
				Step();

				var dbs = new WhileStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = dbs;
				if(!IsEOF)
					dbs.ScopedStatement = Statement(true, false, Scope, dbs);

				if(Expect(While) && Expect(OpenParenthesis))
				{
					dbs.Condition = Expression(Scope);
					Expect(CloseParenthesis);
					if (Expect(Semicolon))
						LastParsedObject = null;
	
					dbs.EndLocation = t.EndLocation;
				}

				return dbs;
			}
			#endregion

			#region ForStatement
			else if (laKind == (For))
				return ForStatement(Scope, Parent);
			#endregion

			#region ForeachStatement
			else if (laKind == Foreach || laKind == Foreach_Reverse)
				return ForeachStatement(Scope, Parent);
			#endregion

			#region [Final] SwitchStatement
			else if ((laKind == (Final) && Lexer.CurrentPeekToken.Kind == (Switch)) || laKind == (Switch))
			{
				var dbs = new SwitchStatement { Location = la.Location, Parent = Parent };
				LastParsedObject = dbs;
				if (laKind == (Final))
				{
					dbs.IsFinal = true;
					Step();
				}
				Step();
				Expect(OpenParenthesis);
				dbs.SwitchExpression = Expression(Scope);
				Expect(CloseParenthesis);

				if(!IsEOF)
					dbs.ScopedStatement = Statement(Scope: Scope, Parent: dbs);
				dbs.EndLocation = t.EndLocation;

				return dbs;
			}
			#endregion

			#region CaseStatement
			else if (laKind == (Case))
			{
				Step();

				var dbs = new SwitchStatement.CaseStatement() { Location = la.Location, Parent = Parent };
				LastParsedObject = dbs;
				dbs.ArgumentList = Expression(Scope);

				if (Expect(Colon))
					LastParsedObject = null;

				// CaseRangeStatement
				if (laKind == DoubleDot)
				{
					Step();
					Expect(Case);
					dbs.LastExpression = AssignExpression();
					if (Expect(Colon))
						LastParsedObject = null;
				}

				var sl = new List<IStatement>();

				while (laKind != Case && laKind != Default && laKind != CloseCurlyBrace && !IsEOF)
				{
					var stmt = Statement(Scope: Scope, Parent: dbs);

					if (stmt != null)
					{
						stmt.Parent = dbs;
						sl.Add(stmt);
					}
				}

				dbs.ScopeStatementList = sl.ToArray();
				dbs.EndLocation = t.EndLocation;

				return dbs;
			}
			#endregion

			#region Default
			else if (laKind == (Default))
			{
				Step();

				var dbs = new SwitchStatement.DefaultStatement()
				{
					Location = la.Location,
					Parent = Parent
				};
				LastParsedObject = dbs;

				Expect(Colon);

				var sl = new List<IStatement>();

				while (laKind != Case && laKind != Default && laKind != CloseCurlyBrace && !IsEOF)
				{
					var stmt = Statement(Scope: Scope, Parent: dbs);

					if (stmt != null)
					{
						stmt.Parent = dbs;
						sl.Add(stmt);
					}
				}

				dbs.ScopeStatementList = sl.ToArray();
				dbs.EndLocation = t.EndLocation;

				return dbs;
			}
			#endregion

			#region Continue | Break
			else if (laKind == (Continue))
			{
				Step();
				var s = new ContinueStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = s;
				if (laKind == (Identifier))
				{
					Step();
					s.Identifier = t.Value;
				}
				if (Expect(Semicolon))
					LastParsedObject = null;
				s.EndLocation = t.EndLocation;

				return s;
			}

			else if (laKind == (Break))
			{
				Step();
				var s = new BreakStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = s;
				if (laKind == (Identifier))
				{
					Step();
					s.Identifier = t.Value;
				}
				if (Expect(Semicolon))
					LastParsedObject = null;
				s.EndLocation = t.EndLocation;

				return s;
			}
			#endregion

			#region Return
			else if (laKind == (Return))
			{
				Step();
				var s = new ReturnStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = s;
				if (laKind != (Semicolon))
					s.ReturnExpression = Expression(Scope);

				if (Expect(Semicolon))
					LastParsedObject = null;
				s.EndLocation = t.EndLocation;

				return s;
			}
			#endregion

			#region Goto
			else if (laKind == (Goto))
			{
				Step();
				var s = new GotoStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = s;

				if (laKind == (Identifier))
				{
					Step();
					s.StmtType = GotoStatement.GotoStmtType.Identifier;
					s.LabelIdentifier = t.Value;
				}
				else if (laKind == Default)
				{
					Step();
					s.StmtType = GotoStatement.GotoStmtType.Default;
				}
				else if (laKind == (Case))
				{
					Step();
					s.StmtType = GotoStatement.GotoStmtType.Case;

					if (laKind != (Semicolon))
						s.CaseExpression = Expression(Scope);
				}

				if (Expect(Semicolon))
					LastParsedObject = null;
				s.EndLocation = t.EndLocation;

				return s;
			}
			#endregion

			#region WithStatement
			else if (laKind == (With))
			{
				Step();

				var dbs = new WithStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = dbs;
				if(Expect(OpenParenthesis))
				{
					// Symbol
					dbs.WithExpression = Expression(Scope);
	
					Expect(CloseParenthesis);
	
					if(!IsEOF)
						dbs.ScopedStatement = Statement(Scope: Scope, Parent: dbs);
				}
				dbs.EndLocation = t.EndLocation;
				return dbs;
			}
			#endregion

			#region SynchronizedStatement
			else if (laKind == (Synchronized))
			{
				Step();
				var dbs = new SynchronizedStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = dbs;

				if (laKind == (OpenParenthesis))
				{
					Step();
					dbs.SyncExpression = Expression(Scope);
					Expect(CloseParenthesis);
				}

				if(!IsEOF)
					dbs.ScopedStatement = Statement(Scope: Scope, Parent: dbs);
				dbs.EndLocation = t.EndLocation;
				
				return dbs;
			}
			#endregion

			#region TryStatement
			else if (laKind == (Try))
			{
				Step();

				var s = new TryStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = s;

				s.ScopedStatement = Statement(Scope: Scope, Parent: s);

				if (!(laKind == (Catch) || laKind == (Finally)))
					SemErr(Catch, "At least one catch or a finally block expected!");

				var catches = new List<TryStatement.CatchStatement>();
				// Catches
				while (laKind == (Catch))
				{
					Step();

					var c = new TryStatement.CatchStatement() { Location = t.Location, Parent = s };
					LastParsedObject = c;

					// CatchParameter
					if (laKind == (OpenParenthesis))
					{
						Step();

						if (laKind == CloseParenthesis || IsEOF)
						{
							SemErr(CloseParenthesis, "Catch parameter expected, not ')'");
							Step();
						}
						else
						{
							var catchVar = new DVariable { Parent = Scope, Location = t.Location };
							LastParsedObject = catchVar;
							Lexer.PushLookAheadBackup();
							catchVar.Type = BasicType();
							if (laKind == CloseParenthesis)
							{
								Lexer.RestoreLookAheadBackup();
								catchVar.Type = new IdentifierDeclaration("Exception");
							}
							else
								Lexer.PopLookAheadBackup();

							if (Expect(Identifier))
							{
								catchVar.Name = t.Value;
								catchVar.NameLocation = t.Location;
								Expect(CloseParenthesis);
							}
							else if(IsEOF)
								ExpectingNodeName = true;

							catchVar.EndLocation = t.EndLocation;
							c.CatchParameter = catchVar;
						}
					}

					if(!IsEOF)
						c.ScopedStatement = Statement(Scope: Scope, Parent: c);
					c.EndLocation = t.EndLocation;

					catches.Add(c);
				}

				if (catches.Count > 0)
					s.Catches = catches.ToArray();

				if (laKind == (Finally))
				{
					Step();

					var f = new TryStatement.FinallyStatement() { Location = t.Location, Parent = Parent };
					LastParsedObject = f;

					f.ScopedStatement = Statement();
					f.EndLocation = t.EndLocation;

					s.FinallyStmt = f;
				}

				s.EndLocation = t.EndLocation;
				return s;
			}
			#endregion

			#region ThrowStatement
			else if (laKind == (Throw))
			{
				Step();
				var s = new ThrowStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = s;

				s.ThrowExpression = Expression(Scope);
				Expect(Semicolon);
				s.EndLocation = t.EndLocation;

				return s;
			}
			#endregion

			#region ScopeGuardStatement
			else if (laKind == DTokens.Scope)
			{
				Step();

				if (laKind == OpenParenthesis)
				{
					var s = new ScopeGuardStatement() { Location = t.Location, Parent = Parent };
					LastParsedObject = s;

					Step();

					if (Expect(Identifier) && t.Value != null) // exit, failure, success
						s.GuardedScope = t.Value.ToLower();

					if (Expect(CloseParenthesis))
						TrackerVariables.ExpectingIdentifier = false;

					if (!IsEOF)
						s.ScopedStatement = Statement(Scope: Scope, Parent: s);

					s.EndLocation = t.EndLocation;
					return s;
				}
				else
					PushAttribute(new Modifier(DTokens.Scope), false);
			}
			#endregion

			#region AsmStmt
			else if (laKind == Asm)
				return AsmStatement(Parent);
			#endregion

			#region PragmaStatement
			else if (laKind == (Pragma))
			{
				var s = new PragmaStatement { Location = la.Location };

				s.Pragma = _Pragma();
				s.Parent = Parent;

				s.ScopedStatement = Statement(Scope: Scope, Parent: s);
				s.EndLocation = t.EndLocation;
				return s;
			}
			#endregion

			#region MixinStatement
			else if (laKind == (Mixin))
			{
				if (Peek(1).Kind == OpenParenthesis)
				{
					OverPeekBrackets(OpenParenthesis);
					if (Lexer.CurrentPeekToken.Kind != Semicolon)
						return ExpressionStatement(Scope, Parent);
					return MixinDeclaration(Scope, Parent);
				}
				else
				{
					var tmx = TemplateMixin(Scope, Parent);
					if (tmx.MixinId == null)
						return tmx;
					else
						return new DeclarationStatement { Declarations = new[] { new NamedTemplateMixinNode(tmx) }, Parent = Parent };
				}
			}
			#endregion

			#region (Static) AssertExpression
			else if (laKind == Assert || (laKind == Static && Lexer.CurrentPeekToken.Kind == Assert))
			{
				var isStatic = laKind == Static;
				AssertStatement s;
				if (isStatic)
				{
					Step();
					s = new StaticAssertStatement { Location = la.Location, Parent = Parent };
				}
				else
					s = new AssertStatement() { Location = la.Location, Parent = Parent };
				LastParsedObject = s;

				Step();

				if (Expect(OpenParenthesis))
				{
					s.AssertedExpression = Expression(Scope);
					if(Expect(CloseParenthesis) && Expect(Semicolon))
						LastParsedObject = null;
				}
				s.EndLocation = t.EndLocation;

				return s;
			}
			#endregion

			#region D1: VolatileStatement
			else if (laKind == Volatile)
			{
				Step();
				var s = new VolatileStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = s;
				s.ScopedStatement = Statement(Scope: Scope, Parent: s);
				s.EndLocation = t.EndLocation;

				return s;
			}
			#endregion

			// ImportDeclaration
			else if (laKind == Import || (laKind == Static && Lexer.CurrentPeekToken.Kind == Import))
			{
				if(laKind == Static)
					Step(); // Will be handled in ImportDeclaration

				return ImportDeclaration(Scope);
			}

			else if (!(ClassLike[laKind] || BasicTypes[laKind] || laKind == Enum || Modifiers[laKind] || IsAtAttribute || laKind == Alias || laKind == Typedef) && IsAssignExpression())
				return ExpressionStatement(Scope, Parent);

			var ds = new DeclarationStatement() { Location = la.Location, Parent = Parent, ParentNode = Scope };
			LastParsedObject = ds;
			ds.Declarations = Declaration(Scope);

			ds.EndLocation = t.EndLocation;
			return ds;
		}

		private IStatement ExpressionStatement(IBlockNode Scope, IStatement Parent)
		{
			var s = new ExpressionStatement() { Location = la.Location, Parent = Parent, ParentNode = Scope };

			if (!IsEOF)
				LastParsedObject = s;
			// a==b, a=9; is possible -> Expressions can be there, not only single AssignExpressions!
			s.Expression = Expression(Scope);

			if (Expect(Semicolon))
				LastParsedObject = null;

			s.EndLocation = t.EndLocation;
			return s;
		}
		
		ForStatement ForStatement(IBlockNode Scope, IStatement Parent)
		{
			Step();

			var dbs = new ForStatement { Location = t.Location, Parent = Parent };
			LastParsedObject = dbs;
			if(!Expect(OpenParenthesis))
				return dbs;

			// Initialize
			if (laKind == Semicolon)
				Step();
			else
				dbs.Initialize = Statement(false, Scope: Scope, Parent: dbs); // Against the spec, blocks aren't allowed here!

			// Test
			if (laKind != (Semicolon) && !IsEOF)
				dbs.Test = Expression(Scope);

			if(Expect(Semicolon))
			{
				// Increment
				if (laKind != (CloseParenthesis))
					dbs.Increment = Expression(Scope);
	
				Expect(CloseParenthesis);
				dbs.ScopedStatement = Statement(Scope: Scope, Parent: dbs);
			}
			dbs.EndLocation = t.EndLocation;

			return dbs;
		}

		ForeachStatement ForeachStatement(IBlockNode Scope,IStatement Parent)
		{
			Step();

			var dbs = new ForeachStatement() { 
				Location = t.Location, 
				IsReverse = t.Kind == Foreach_Reverse, 
				Parent = Parent 
			};

			LastParsedObject = dbs;
			if(!Expect(OpenParenthesis))
				return dbs;

			var tl = new List<DVariable>();

			bool init=true;
			while(init || laKind == Comma)
			{
				if (init) 
					init = false;
				else
					Step();
				
				var forEachVar = new DVariable{ Parent = Scope };
				forEachVar.Location = la.Location;

				if (laKind == Ref || laKind == InOut)
				{
					Step();
					if(forEachVar.Attributes == null)
						forEachVar.Attributes = new List<DAttribute>();
					forEachVar.Attributes.Add(new Modifier(t.Kind));
				}
				
				if(IsEOF){
					TrackerVariables.ExpectingIdentifier = true;
					SynErr(t.Kind,"Basic type or iteration variable identifier expected.");
					return dbs;
				}
				
				LastParsedObject = forEachVar;					
				
				if (laKind == (Identifier) && (Lexer.CurrentPeekToken.Kind == (Semicolon) || Lexer.CurrentPeekToken.Kind == Comma))
				{
					Step();
					forEachVar.NameLocation = t.Location;
					forEachVar.Name = t.Value;
				}
				else
				{
					var type = BasicType();
					
					var tnode = Declarator(type, false, Scope);
					if (tnode == null)
						break;
					if(forEachVar.Attributes != null)
						if(tnode.Attributes == null)
							tnode.Attributes = new List<DAttribute>(forEachVar.Attributes);
						else
							tnode.Attributes.AddRange(forEachVar.Attributes);
					tnode.Location = forEachVar.Location;
					forEachVar = (DVariable)tnode;
				}
				forEachVar.EndLocation = t.EndLocation;

				tl.Add(forEachVar);
			}
			
			dbs.ForeachTypeList = tl.ToArray();

			if(Expect(Semicolon))
				dbs.Aggregate = Expression(Scope);

			// ForeachRangeStatement
			if (laKind == DoubleDot)
			{
				Step();
				dbs.UpperAggregate = Expression();
			}

			if(Expect(CloseParenthesis))
				dbs.ScopedStatement = Statement(Scope: Scope, Parent: dbs);
			dbs.EndLocation = t.EndLocation;

			return dbs;
		}

		AsmStatement AsmStatement(IStatement Parent)
		{
			Step();
			var s = new AsmStatement() { Location = t.Location, Parent = Parent };
			LastParsedObject = s;

			Expect(OpenCurlyBrace);

			var l = new List<string>();
			var curInstr = "";
			while (!IsEOF && laKind != (CloseCurlyBrace))
			{
				if (laKind == Semicolon)
				{
					l.Add(curInstr.Trim());
					curInstr = "";
				}
				else
					curInstr += laKind == Identifier ? la.Value : DTokens.GetTokenString(laKind);

				Step();
			}

			Expect(CloseCurlyBrace);
			s.EndLocation = t.EndLocation;
			return s;
		}

		StatementCondition StmtCondition(IStatement Parent, IBlockNode Scope)
		{
			var sl = la.Location;

			var c = Condition(Scope);
			c.Location = sl;
			c.EndLocation = t.EndLocation;
			var sc = new StatementCondition {
				Condition = c,
				Location = sl,
			};

			sc.ScopedStatement = Statement(true, false, Scope, sc);

			if(laKind == Else)
			{
				Step();
				sc.ElseStatement = Statement(true, false, Scope, sc);
			}
			
			if(IsEOF)
				sc.EndLocation = la.Location;
			else
				sc.EndLocation = t.EndLocation;

			return sc;
		}

		public BlockStatement BlockStatement(INode ParentNode=null, IStatement Parent=null)
		{
			var OldPreviousCommentString = PreviousComment;
			PreviousComment = new StringBuilder ();

			var bs = new BlockStatement() { Location=la.Location, ParentNode=ParentNode, Parent=Parent};
			LastParsedObject = bs;

			if (Expect(OpenCurlyBrace))
			{
				if (ParseStructureOnly && laKind != CloseCurlyBrace)
					Lexer.SkipCurrentBlock();
				else
				{
					while (!IsEOF && laKind != (CloseCurlyBrace))
					{
						var prevLocation = la.Location;
						var s = Statement(Scope: ParentNode as IBlockNode, Parent: bs);

						// Avoid infinite loops -- hacky?
						if (prevLocation == la.Location)
						{
							Step();
							break;
						}

						bs.Add(s);
					}
				}
				if (Expect(CloseCurlyBrace))
					LastParsedObject = null;

				if (!IsEOF)
					LastParsedObject = bs;
			}
			if(t!=null)
				bs.EndLocation = t.EndLocation;

			PreviousComment = OldPreviousCommentString;
			return bs;
		}
		#endregion

		#region Structs & Unions
		private INode AggregateDeclaration(INode Parent)
		{
			var classType = laKind;
			if (!(classType == Union || classType == Struct))
				SynErr(t.Kind, "union or struct required");
			Step();

			var ret = new DClassLike(t.Kind) { 
				Location = t.Location, 
				Description = GetComments(),
                ClassType=classType,
				Parent=Parent
			};
			LastParsedObject = ret;
			ApplyAttributes(ret);

			// Allow anonymous structs&unions
			if (laKind == Identifier)
			{
				Expect(Identifier);
				ret.Name = t.Value;
				ret.NameLocation = t.Location;
			}
			else if (IsEOF)
				ExpectingNodeName = true;

			if (laKind == (Semicolon))
			{
				Step();
				return ret;
			}

			// StructTemplateDeclaration
			if (laKind == (OpenParenthesis))
			{
				TemplateParameterList(ret);

				// Constraint[opt]
				if (laKind == (If))
					Constraint(ret);
			}

			ClassBody(ret);

			return ret;
		}
		#endregion

		#region Classes
		private INode ClassDeclaration(INode Parent)
		{
			Expect(Class);

			var dc = new DClassLike(Class) { 
				Location = t.Location,
				Description=GetComments(),
				Parent=Parent
			};
			LastParsedObject = dc;

			ApplyAttributes(dc);

			if (Expect(Identifier))
			{
				dc.Name = t.Value;
				dc.NameLocation = t.Location;
			}
			else if (IsEOF)
				ExpectingNodeName = true;

			if (laKind == (OpenParenthesis))
				TemplateParameterList(dc);

			// Constraints
			// http://dlang.org/template.html#ClassTemplateDeclaration
			if (Constraint (dc)) { // Constraint_opt BaseClassList_opt
				if (laKind == (Colon))
					BaseClassList (dc);
			} else if (laKind == (Colon)) { // Constraint_opt BaseClassList_opt
				BaseClassList (dc);
				Constraint (dc);
			}

			ClassBody(dc);

			dc.EndLocation = t.EndLocation;
			return dc;
		}

		bool Constraint(DNode dn)
		{
			if (laKind == If) {
				Step ();
				Expect (OpenParenthesis);

				dn.TemplateConstraint = Expression ();

				Expect (CloseParenthesis);

				return true;
			}
			return false;
		}

		private void BaseClassList(DClassLike dc,bool ExpectColon=true)
		{
			if (ExpectColon) Expect(Colon);

			var ret = new List<ITypeDeclaration>();
			dc.BaseClasses = ret;

			bool init = true;
			while (init || laKind == (Comma))
			{
				if (!init) Step();
				init = false;
				if (IsProtectionAttribute() && laKind != (Protected))
					Step();

				var ids=IdentifierList();
				if (ids != null)
					ret.Add(ids);
			}

			if (IsEOF)
			{
				if (ret.Count != 0)
					LastParsedObject = ret[ret.Count - 1];
				TrackerVariables.IsParsingBaseClassList = true;
				TrackerVariables.InitializedNode = dc;
			}
		}

		public void ClassBody(DBlockNode ret,bool KeepBlockAttributes=false,bool UpdateBoundaries=true)
		{
			var OldPreviousCommentString = PreviousComment;
			PreviousComment = new StringBuilder ();

			if (Expect(OpenCurlyBrace))
			{
				var stk_backup = BlockAttributes;

				if(!KeepBlockAttributes)
					BlockAttributes = new Stack<DAttribute>();

				if(UpdateBoundaries)
					ret.BlockStartLocation = t.Location;

				while (!IsEOF && laKind != (CloseCurlyBrace))
				{
					DeclDef(ret);
				}

				if (!IsEOF)
					LastParsedObject = ret;

				if (Expect(CloseCurlyBrace))
					LastParsedObject = null;

				if(UpdateBoundaries)
					ret.EndLocation = t.EndLocation;

				if(!KeepBlockAttributes)
					BlockAttributes = stk_backup;
			}

			PreviousComment = OldPreviousCommentString;

			if(ret!=null)
				ret.Description += CheckForPostSemicolonComment();
		}

		INode Constructor(DBlockNode scope,bool IsStruct)
		{
			Expect(This);
			var dm = new DMethod(){
				Parent = scope,
				SpecialType = DMethod.MethodType.Constructor,
				Location = t.Location,
				Name = DMethod.ConstructorIdentifier,
				NameLocation = t.Location
			};
			dm.Description = GetComments();
			LastParsedObject = dm;

			if (IsTemplateParameterList())
				TemplateParameterList(dm);

			// http://dlang.org/struct.html#StructPostblit
			if (IsStruct && laKind == (OpenParenthesis) && Peek(1).Kind == (This))
			{
				var dv = new DVariable();
				LastParsedObject = dv;
				dv.Parent = dm;
				dv.Name = "this";
				dm.Parameters.Add(dv);
				Step();
				Step();
				Expect(CloseParenthesis);
			}
			else
			{
				dm.Parameters = Parameters(dm);
			}

			// handle post argument attributes
			FunctionAttributes(dm);

			if (!IsEOF)
				LastParsedObject = dm;

			if (laKind == If)
				Constraint(dm);

			// handle post argument attributes
			FunctionAttributes(dm);

			if(IsFunctionBody)
				FunctionBody(dm);
			return dm;
		}

		INode Destructor()
		{
			Expect(Tilde);
			var dm = new DMethod{ Location = t.Location, NameLocation = la.Location };
			Expect(This);
			
			LastParsedObject = dm;

			dm.SpecialType = DMethod.MethodType.Destructor;
			dm.Name = "~this";

			if (IsTemplateParameterList())
				TemplateParameterList(dm);

			dm.Parameters = Parameters(dm);

			if (laKind == (If))
				Constraint(dm);

			FunctionBody(dm);
			return dm;
		}
		#endregion

		#region Interfaces
		private IBlockNode InterfaceDeclaration(INode Parent)
		{
			Expect(Interface);
			var dc = new DClassLike() { 
				Location = t.Location, 
				Description = GetComments(),
                ClassType= DTokens.Interface,
				Parent=Parent
			};
			LastParsedObject = dc;

			ApplyAttributes(dc);

			if (!Expect(Identifier))
			{
				if (IsEOF)
					ExpectingNodeName = true;
				return dc;
			}
			dc.Name = t.Value;
			dc.NameLocation = t.Location;

			if (laKind == (OpenParenthesis))
				TemplateParameterList(dc);

			if (laKind == (If))
				Constraint(dc);

			if (laKind == (Colon))
				BaseClassList(dc);

			// Empty interfaces are allowed
			if (laKind == Semicolon)
				Step();
			else
				ClassBody(dc);

			dc.EndLocation = t.EndLocation;
			return dc;
		}
		#endregion

		#region Enums
		private INode[] EnumDeclaration(INode Parent)
		{
			Expect(Enum);
			var ret = new List<INode>();

			var mye = new DEnum() { Location = t.Location, Description = GetComments(), Parent=Parent };
			LastParsedObject = mye;

			ApplyAttributes(mye);

			if (laKind != Identifier && IsBasicType())
				mye.Type = Type();
			else if (laKind == Auto)
			{
				Step();
				mye.Attributes.Add(new Modifier(Auto));
			}

			if (laKind == (Identifier))
			{
				// Normal enum identifier
				if (Lexer.CurrentPeekToken.Kind == (Assign) || // enum e = 1234;
				    Lexer.CurrentPeekToken.Kind == (OpenCurlyBrace) || // enum e { A,B,C, }
				    Lexer.CurrentPeekToken.Kind == (Semicolon) || // enum e;
				    Lexer.CurrentPeekToken.Kind == Colon) { // enum e : uint {..}
					Step ();
					mye.Name = t.Value;
					mye.NameLocation = t.Location;
				}
				else {
					if (mye.Type == null)
						mye.Type = Type();

					if (Expect(Identifier))
					{
						mye.Name = t.Value;
						mye.NameLocation = t.Location;
					}
				}
			}
			else if (IsEOF)
				ExpectingNodeName = true;

			if (IsDeclaratorSuffix)
			{
				DeclaratorSuffixes(mye);
			}

			// Enum inhertance type
			if (laKind == (Colon))
			{
				Step();
				mye.Type = Type();
			}

			// Variables with 'enum' as base type
			if (laKind == (Assign) || laKind == (Semicolon))
			{
				do
				{
					var enumVar = new DVariable();
					LastParsedObject = enumVar;

					enumVar.AssignFrom(mye);

					enumVar.Attributes.Add(new Modifier(Enum));
					if (mye.Type != null)
						enumVar.Type = mye.Type;
					else
						enumVar.Type = new DTokenDeclaration(Enum);

					if (laKind == (Comma))
					{
						Step();
						Expect(Identifier);
						enumVar.Name = t.Value;
						enumVar.NameLocation = t.Location;
					}

					if (laKind == (Assign))
					{
						//Step(); -- expected by initializer
						enumVar.Initializer = Initializer(); // Seems to be specified wrongly - theoretically there must be an AssignExpression();
					}
					enumVar.EndLocation = t.Location;
					ret.Add(enumVar);
				}
				while (laKind == Comma);

				Expect(Semicolon);
			}
			else if (laKind == OpenCurlyBrace) // Normal enum block
			{
				EnumBody(mye);
				ret.Add(mye);
			}

			mye.Description += CheckForPostSemicolonComment();

			return ret.ToArray();
		}

		public void EnumBody(DEnum mye)
		{
			var OldPreviousComment = PreviousComment;
			PreviousComment = new StringBuilder();
			mye.BlockStartLocation = la.Location;

			// While there are commas, loop through
			do
			{
				Step();

				if (laKind == CloseCurlyBrace)
					break;

				var ev = new DEnumValue() { Location = la.Location, Description = GetComments(), Parent = mye };
				LastParsedObject = ev;

				if (laKind == Identifier && (
					Lexer.CurrentPeekToken.Kind == Assign ||
					Lexer.CurrentPeekToken.Kind == Comma ||
					Lexer.CurrentPeekToken.Kind == CloseCurlyBrace))
				{
					Step();
					ev.Name = t.Value;
					ev.NameLocation = t.Location;
				}
				else
				{
					ev.Type = Type();
					if (Expect(Identifier))
					{
						ev.Name = t.Value;
						ev.NameLocation = t.Location;
					}
					else if (IsEOF)
						ExpectingNodeName = true;
				}

				if (laKind == (Assign))
				{
					Step();
					ev.Initializer = AssignExpression();
				}

				ev.EndLocation = t.EndLocation;
				ev.Description += CheckForPostSemicolonComment();

				mye.Add(ev);
			}
			while (laKind == Comma);

			Expect(CloseCurlyBrace);
			PreviousComment = OldPreviousComment;

			mye.EndLocation = t.EndLocation;
		}
		#endregion

		#region Functions
		bool IsFunctionBody { get { return laKind == In || laKind == Out || laKind == Body || laKind == OpenCurlyBrace; } }

		void FunctionBody(DMethod par)
		{
			if (laKind == Semicolon) // Abstract or virtual functions
			{
				Step();
				par.Description += CheckForPostSemicolonComment();
				par.EndLocation = t.EndLocation;
				return;
			}

			var stk_Backup = BlockAttributes;
			BlockAttributes = new Stack<DAttribute> ();

			while (
				(laKind == In && par.In == null) ||
				(laKind == Out && par.Out == null))
			{
				if (laKind == In)
				{
					Step();
					par.InToken = t.Location;

					par.In = BlockStatement(par);
				}

				if (laKind == Out)
				{
					Step();
					par.OutToken = t.Location;

					if (laKind == OpenParenthesis)
					{
						Step();
						if (Expect(Identifier))
						{
							par.OutResultVariable = new IdentifierDeclaration(t.Value) { Location=t.Location, EndLocation=t.EndLocation };
						}
						Expect(CloseParenthesis);
					}

					par.Out = BlockStatement(par);
				}
			}

			// Although there can be in&out constraints, there doesn't have to be a direct body definition. Used on abstract class/interface methods.
			if (laKind == Body){
				Step();
				par.BodyToken = t.Location;
			}

			if ((par.In==null && par.Out==null) || 
				laKind == OpenCurlyBrace)
			{
				par.Body = BlockStatement(par);
			}

			BlockAttributes = stk_Backup;
			par.EndLocation = t.EndLocation;
		}
		#endregion

		#region Templates
		/*
         * American beer is like sex on a boat - Fucking close to water;)
         */

		private INode TemplateDeclaration(INode Parent)
		{
			var startLoc = la.Location;
			
			// TemplateMixinDeclaration
			Modifier mixinMod;
			if (laKind == Mixin){
				Step();
				mixinMod = new Modifier(Mixin){ Location = t.Location, EndLocation = t.EndLocation };
			}
			else
				mixinMod = null;
			
			Expect(Template);
			var dc = new DClassLike(Template) {
				Description=GetComments(),
				Location=startLoc,
				Parent=Parent
			};
			LastParsedObject = dc;

			ApplyAttributes(dc);

			if (mixinMod != null)
				dc.Attributes.Add(mixinMod);

			if (Expect(Identifier))
			{
				dc.Name = t.Value;
				dc.NameLocation = t.Location;
			}
			else if (IsEOF)
				ExpectingNodeName = true;

			TemplateParameterList(dc);

			if (laKind == (If))
				Constraint(dc);

			// [Must not contain a base class list]

			ClassBody(dc);

			return dc;
		}

		TemplateMixin TemplateMixin(INode Scope, IStatement Parent = null)
		{
			// mixin TemplateIdentifier !( TemplateArgumentList ) MixinIdentifier ;
			//							|<--			optional			 -->|
			var r = new TemplateMixin { Attributes = GetCurrentAttributeSet_Array() };
			if(Parent == null)
				r.ParentNode = Scope;
			else
				r.Parent = Parent;
			LastParsedObject = r;
			ITypeDeclaration preQualifier = null;

			Expect(Mixin);
			r.Location = t.Location;
			
			bool modScope = false;
			if (laKind == Dot)
			{
				modScope = true;
				Step();
			}
			else if(laKind!=Identifier)
			{// See Dsymbol *Parser::parseMixin()
				if (laKind == Typeof)
				{
					preQualifier=TypeOf();
				}
				else if (laKind == __vector)
				{
					//TODO: Parse vectors(?)
				}

				Expect(Dot);
			}

			r.Qualifier= IdentifierList();
			if (r.Qualifier != null)
				r.Qualifier.InnerMost.InnerDeclaration = preQualifier;
			else
				r.Qualifier = preQualifier;
			
			if(modScope)
			{
				var innerMost = r.Qualifier.InnerMost;
				if(innerMost is IdentifierExpression)	
					(innerMost as IdentifierExpression).ModuleScoped = true;
				else if(innerMost is TemplateInstanceExpression)
					(innerMost as TemplateInstanceExpression).ModuleScopedIdentifier = true;
			}

			// MixinIdentifier
			if (laKind == Identifier)
			{
				Step();
				r.IdLocation = t.Location;
				r.MixinId = t.Value;
			}

			Expect(Semicolon);
			r.EndLocation = t.EndLocation;
			
			return r;
		}

		/// <summary>
		/// Be a bit lazy here with checking whether there're templates or not
		/// </summary>
		private bool IsTemplateParameterList()
		{
			Lexer.StartPeek();
			var pk = la;
			int r = 0;
			while (r >= 0 && pk.Kind != EOF && pk.Kind != __EOF__)
			{
				if (pk.Kind == OpenParenthesis)
					r++;
				else if (pk.Kind == CloseParenthesis)
				{
					r--;
					if (r <= 0)
						return Peek().Kind == OpenParenthesis;
				}
				pk = Peek();
			}
			return false;
		}

		void TemplateParameterList(DNode dn)
		{
			if (!Expect(OpenParenthesis))
			{
				SynErr(OpenParenthesis, "Template parameter list expected");
				dn.TemplateParameters = new TemplateParameter[0];
				return;
			}

			if (laKind == (CloseParenthesis))
			{
				Step();
				return;
			}

			var ret = new List<TemplateParameter>();

			bool init = true;
			while (init || laKind == (Comma))
			{
				if (init) init = false;
				else Step();

				if (laKind == CloseParenthesis)
					break;

				ret.Add(TemplateParameter(dn));
			}

			Expect(CloseParenthesis);

			dn.TemplateParameters = ret.ToArray();
		}

		TemplateParameter TemplateParameter(DNode parent)
		{
			CodeLocation startLoc;

			// TemplateThisParameter
			if (laKind == (This))
			{
				Step();

				startLoc = t.Location;
				var end = t.EndLocation;

				var ret= new TemplateThisParameter(TemplateParameter(parent), parent) { Location=startLoc, EndLocation=end };
				LastParsedObject = ret;
				return ret;
			}

			// TemplateTupleParameter
			else if (laKind == (Identifier) && Lexer.CurrentPeekToken.Kind == TripleDot)
			{
				Step();
				startLoc = t.Location;
				var id = t.Value;
				Step();

				var ret=new TemplateTupleParameter(id, startLoc, parent) { Location=startLoc, EndLocation=t.EndLocation	};
				LastParsedObject = ret;
				return ret;
			}

			// TemplateAliasParameter
			else if (laKind == (Alias))
			{
				Step();

				startLoc = t.Location;
				TemplateAliasParameter al;

				if(Expect(Identifier))
					al = new TemplateAliasParameter(t.Value, t.Location, parent);
				else
					al = new TemplateAliasParameter(0, CodeLocation.Empty, parent);
				al.Location = startLoc;
				LastParsedObject = al;

				// TODO?:
				// alias BasicType Declarator TemplateAliasParameterSpecialization_opt TemplateAliasParameterDefault_opt

				// TemplateAliasParameterSpecialization
				if (laKind == (Colon))
				{
					Step();

					AllowWeakTypeParsing=true;
					al.SpecializationType = Type();
					AllowWeakTypeParsing=false;

					if (al.SpecializationType==null)
						al.SpecializationExpression = ConditionalExpression();
				}

				// TemplateAliasParameterDefault
				if (laKind == (Assign))
				{
					Step();

					AllowWeakTypeParsing=true;
					al.DefaultType = Type();
					AllowWeakTypeParsing=false;

					if (al.DefaultType==null)
						al.DefaultExpression = ConditionalExpression();
				}
				al.EndLocation = t.EndLocation;
				return al;
			}

			// TemplateTypeParameter
			else if (laKind == (Identifier) && (Lexer.CurrentPeekToken.Kind == (Colon) || Lexer.CurrentPeekToken.Kind == (Assign) || Lexer.CurrentPeekToken.Kind == (Comma) || Lexer.CurrentPeekToken.Kind == (CloseParenthesis)))
			{
				Expect(Identifier);
				var tt = new TemplateTypeParameter(t.Value, t.Location, parent) { Location = t.Location };
				LastParsedObject = tt;

				if (laKind == Colon)
				{
					Step();
					tt.Specialization = Type();
				}

				if (laKind == Assign)
				{
					Step();
					tt.Default = Type();
				}
				tt.EndLocation = t.EndLocation;
				return tt;
			}

			// TemplateValueParameter
			startLoc = la.Location;
			var bt = BasicType();
			var dv = Declarator(bt,false, null);

			if (dv == null) {
				SynErr (t.Kind, "Declarator expected for parsing template value parameter");
				return null;
			}

			var tv = new TemplateValueParameter(dv.NameHash, dv.NameLocation, parent) { 
				Location=la.Location,
				Type = dv.Type
			};
			LastParsedObject = tv;

			if (laKind == (Colon))
			{
				Step();
				tv.SpecializationExpression = ConditionalExpression();
			}

			if (laKind == (Assign))
			{
				Step();
				tv.DefaultExpression = AssignExpression();
			}
			tv.EndLocation = t.EndLocation;
			return tv;
		}

		bool IsTemplateInstance
		{
			get {
				Lexer.StartPeek ();
				if (laKind != Identifier && (!DTokens.StorageClass [laKind] || Peek ().Kind != Identifier))
					return false;
				
				var r = Peek ().Kind == Not && !(Peek().Kind == Is || Lexer.CurrentPeekToken.Kind == In);
				Peek (1);
				return r;
			}
		}

		public TemplateInstanceExpression TemplateInstance(IBlockNode Scope)
		{
			var loc = la.Location;

			var mod = INVALID;

			if (DTokens.StorageClass [laKind]) {
				mod = laKind;
				Step ();
			}

			if (!Expect (Identifier))
				return null;

			ITypeDeclaration td = new IdentifierDeclaration (t.Value) { 
				Location = t.Location, 
				EndLocation = t.EndLocation
			};

			td = new TemplateInstanceExpression(mod != DTokens.INVALID ? new MemberFunctionAttributeDecl(mod) { InnerType = td } : td) {
				Location = loc
			};
			LastParsedObject = td;

			var args = new List<IExpression>();

			if (!Expect(Not))
				return td as TemplateInstanceExpression;

			if (laKind == (OpenParenthesis))
			{
				Step();

				if (laKind != CloseParenthesis)
				{
					bool init = true;
					while (laKind == Comma || init)
					{
						if (!init) Step();
						init = false;

						if (laKind == CloseParenthesis)
							break;

						if (IsEOF)
						{
							args.Add(new TokenExpression(DTokens.INVALID) { Location= la.Location, EndLocation=la.EndLocation });
							break;
						}
						
						Lexer.PushLookAheadBackup();

						bool wp = AllowWeakTypeParsing;
						AllowWeakTypeParsing = true;

						var typeArg = Type();

						AllowWeakTypeParsing = wp;

						if (typeArg != null && (laKind == CloseParenthesis || laKind==Comma)){
							Lexer.PopLookAheadBackup();
							args.Add(new TypeDeclarationExpression(typeArg));
						}else
						{
							Lexer.RestoreLookAheadBackup();
							args.Add(AssignExpression(Scope));
						}
					}
				}
				Expect(CloseParenthesis);
			}
			else
			{
				Step();

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
				 *		__FILE__ 
				 *		__LINE__
				 */

				IExpression arg= null;

				if (t.Kind == Literal)
					arg = new IdentifierExpression(t.LiteralFormat == LiteralFormat.StringLiteral || 
					                               t.LiteralFormat == LiteralFormat.VerbatimStringLiteral ? 
					                               t.Value :
					                               t.LiteralValue,
					                               t.LiteralFormat, 
					                               t.Subformat)
					{
						Location = t.Location,
						EndLocation = t.EndLocation
					};
				else if (t.Kind == Identifier)
					arg = new IdentifierExpression(t.Value)
					{
						Location = t.Location,
						EndLocation = t.EndLocation
					};
				else if (BasicTypes[t.Kind])
					arg = new TypeDeclarationExpression(new DTokenDeclaration(t.Kind)
					{
						Location = t.Location,
						EndLocation = t.EndLocation
					});
				else if (
					t.Kind == True ||
					t.Kind == False ||
					t.Kind == Null ||
					t.Kind == __FILE__ ||
					t.Kind == __LINE__)
					arg = new TokenExpression(t.Kind)
					{
						Location = t.Location,
						EndLocation = t.EndLocation
					};
				else if (IsEOF)
				{
					TrackerVariables.ExpectingIdentifier = false;
					td.EndLocation = CodeLocation.Empty;
					return td as TemplateInstanceExpression;
				}

				args.Add(arg);
			}
			(td as TemplateInstanceExpression).Arguments = args.ToArray();
			td.EndLocation = t.EndLocation;
			return td as TemplateInstanceExpression;
		}
		#endregion

		#region Traits
		IExpression TraitsExpression()
		{
			Expect(__traits);
			var ce = new TraitsExpression() { Location=t.Location};
			LastParsedObject = ce;
			if(Expect(OpenParenthesis))
			{
				if(Expect(Identifier))
					ce.Keyword = t.Value;

				var al = new List<TraitsArgument>();

				while (laKind == Comma)
				{
					Step();

					if (IsAssignExpression())
						al.Add(new TraitsArgument(AssignExpression()));
					else
						al.Add(new TraitsArgument(Type()));
				}

				if (Expect(CloseParenthesis))
					TrackerVariables.ExpectingIdentifier = false;
				
				if(al.Count != 0)
					ce.Arguments = al.ToArray();
			}
			ce.EndLocation = t.EndLocation;
			return ce;
		}
		#endregion
	}
}