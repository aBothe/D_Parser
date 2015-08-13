using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

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
			module.Location = new CodeLocation(1,1);
			module.BlockStartLocation = new CodeLocation(1, 1);
			doc = module;

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

			Comments.AddRange(Lexer.Comments);
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
						Comments.Add(c);
						continue;
					}
					else if (c.StartPosition.Line > ExpectedLine)
						break;

					ret.AppendLine(c.CommentText);
				}
				
				i++;
				Comments.Add(c);
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

		public void DeclDef(DBlockNode module)
		{
			if (IsAttributeSpecifier) {
				do
					AttributeSpecifier (module);
				while(IsAttributeSpecifier);

				var tkind = t.Kind;
				if(tkind == Semicolon || tkind == CloseCurlyBrace || tkind == Colon)
					return;
			}

			if (laKind == Semicolon)
			{
				Step();
				return;
			}

			switch (laKind)
			{
				case DTokens.Module:
					var mod = module as DModule;

					var ddoc = GetComments ();
					var ms = ModuleDeclaration ();
					ms.ParentNode = module;
					ddoc += CheckForPostSemicolonComment ();

					if (mod != null) {
						if (mod.StaticStatements.Count != 0 ||
						    mod.Children.Count != 0)
							SynErr (DTokens.Module, "Module declaration must stand at a module's beginning.");
							
						mod.OptionalModuleStatement = ms;
						mod.Description = ddoc;

						if (ms.ModuleName!=null)
							mod.ModuleName = ms.ModuleName.ToString();
					} else
						SynErr (DTokens.Module, "Module statements only allowed in module scope.");

					module.Add (ms);
					break;
				case Import:
					module.Add(ImportDeclaration(module));
					break;
				case This:
					module.Add(Constructor(module, module is DClassLike && ((DClassLike)module).ClassType == DTokens.Struct));
					break;
				case Tilde:
					if (Lexer.CurrentPeekToken.Kind != This)
						goto default;
					module.Add(Destructor());
					break;
				case Invariant:
					module.Add(_Invariant());
					break;
				case Unittest:
					Step();
					var dbs = new DMethod(DMethod.MethodType.Unittest);
					ApplyAttributes(dbs);
					dbs.Location = t.Location;
					FunctionBody(dbs);
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
				case Version:
				case Debug:
					if (Peek(1).Kind == Assign)
					{
						DebugSpecification ds = null;
						VersionSpecification vs = null;

						if (laKind == Version)
							vs = new VersionSpecification {
								Location = la.Location,
								Attributes = GetCurrentAttributeSet_Array()
							};
						else
							ds = new DebugSpecification {
								Location = la.Location,
								Attributes = GetCurrentAttributeSet_Array()
							};

						Step();
						Step();

						if (laKind == Literal) {
							Step ();
							if (t.LiteralFormat != LiteralFormat.Scalar)
								SynErr (t.Kind, "Integer literal expected!");
							try {
								if (vs != null)
									vs.SpecifiedNumber = Convert.ToUInt64 (t.LiteralValue);
								else
									ds.SpecifiedDebugLevel = Convert.ToUInt64 (t.LiteralValue);
							} catch {
							}
						} else if (laKind == Identifier) {
							Step ();
							if (vs != null)
								vs.SpecifiedId = t.Value;
							else
								ds.SpecifiedId = t.Value;
						} else if (IsEOF) {
							if (vs != null)
								vs.SpecifiedId = DTokens.IncompleteId;
							else
								ds.SpecifiedId = DTokens.IncompleteId;
						}
						else if (ds == null)
							Expect(Identifier);

						Expect(Semicolon);

						((AbstractStatement)ds ?? vs).EndLocation = t.EndLocation;

						module.Add(vs as StaticStatement ?? ds);
					}
					else
						DeclarationCondition(module);
					break;
				case Static:
					if (Lexer.CurrentPeekToken.Kind == If)
						goto case Version;
					goto default;
				case Assert:
					Step();
					CheckForStorageClasses(module);
					if (!Modifier.ContainsAttribute(DeclarationAttributes, Static))
						SynErr(Static, "Static assert statements must be explicitly marked as static");

					module.Add(ParseStaticAssertStatement(module));
					Expect(Semicolon);
					break;
				case Mixin:
					switch(Peek(1).Kind)
					{
						case Template:
							module.Add (TemplateDeclaration (module));
							break;
						
						case DTokens.__vector:
						case DTokens.Typeof:
						case Dot:
						case Identifier://TemplateMixin
							var tmx = TemplateMixin (module);
							if (tmx.MixinId == null)
								module.Add (tmx);
							else
								module.Add (new NamedTemplateMixinNode (tmx));
							break;

						case OpenParenthesis:
							module.Add (MixinDeclaration (module, null));
							break;
						default:
							Step ();
							SynErr (Identifier);
							break;
					}
					break;
				case OpenCurlyBrace:
					AttributeBlock(module);
					break;
				// Class Allocators
				// Note: Although occuring in global scope, parse it anyway but declare it as semantic nonsense;)
				case New:
					Step();

					var dm = new DMethod(DMethod.MethodType.Allocator) { Location = t.Location };
					ApplyAttributes(dm);

					Parameters(dm);
					FunctionBody(dm);
					module.Add(dm);
					break;
				case Delete:
					Step();

					var ddm = new DMethod(DMethod.MethodType.Deallocator) { Location = t.Location };
					ddm.Name = "delete";
					ApplyAttributes(ddm);

					Parameters(ddm);
					FunctionBody(ddm);
					module.Add(ddm);
					break;
				default:
					var decls = Declaration(module);
					if(module != null && decls!=null)
						module.AddRange(decls);
					break;
			}
		}

		StaticAssertStatement ParseStaticAssertStatement(IBlockNode scope)
		{
			DeclarationAttributes.Clear();

			var ass = new StaticAssertStatement {
				Attributes = GetCurrentAttributeSet_Array(),
				Location = t.Location
			};

			if (Expect(OpenParenthesis))
			{
				ass.AssertedExpression = AssignExpression();
				if (laKind == (Comma))
				{
					Step();
					ass.Message = AssignExpression();
				}

				Expect (CloseParenthesis);
			}

			ass.EndLocation = t.EndLocation;
			return ass;
		}

		IMetaDeclarationBlock AttributeBlock(DBlockNode module)
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
						case DTokens.Virtual:
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

			ClassBody(module, true, false);

			BlockAttributes = stk_backup;

			// Store the meta block
			metaDeclBlock.EndLocation = t.EndLocation;
			if(module!=null)
				module.Add(metaDeclBlock);
			return metaDeclBlock;
		}

		DeclarationCondition Condition(IBlockNode parent)
		{
			DeclarationCondition c = null;

			switch (laKind) {
			case Version:
				/*				 
				 * http://www.dlang.org/version.html#VersionSpecification
				 * VersionCondition: 
				 *		version ( IntegerLiteral ) 
				 *		version ( Identifier ) 
				 *		version ( unittest )
				 *		version ( assert )
				 */
				Step ();
				if (Expect (OpenParenthesis)) {
					switch (laKind) {
					case Unittest:
						Step ();
						c = new VersionCondition ("unittest") { IdLocation = t.Location };
						break;
					case Assert:
						Step ();
						c = new VersionCondition ("assert") { IdLocation = t.Location };
						break;
					case Literal:
						Step ();
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
						if (Expect (Identifier))
							c = new VersionCondition (t.Value) { IdLocation = t.Location };
						else if (IsEOF) {
							c = new VersionCondition (DTokens.IncompleteId);
							parent.Add (new DVariable{ Attributes = new List<DAttribute>{ c } });
						}
						break;
					}

					Expect (CloseParenthesis);
				}

				if (c == null)
					c = new VersionCondition (0);
				break;

			case Debug:
				/*				
				 * DebugCondition:
				 *		debug 
				 *		debug ( IntegerLiteral )
				 *		debug ( Identifier )
				 */
				Step ();
				if (laKind == OpenParenthesis) {
					Step ();

					if (laKind == Literal) {
						Step ();
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
					} else if (Expect (Identifier))
						c = new DebugCondition ((string)t.LiteralValue) { IdLocation = t.Location };
					else if (IsEOF) {
						c = new DebugCondition (DTokens.IncompleteId);
						parent.Add (new DVariable{ Attributes = new List<DAttribute>{ c } });
					}

					Expect (CloseParenthesis);
				}

				if (c == null)
					c = new DebugCondition ();
				break;

			case Static:
				/*				
				 * StaticIfCondition: 
				 *		static if ( AssignExpression )
				 */
				Step ();
				if (Expect (If) && Expect (OpenParenthesis)) {
					var x = AssignExpression (parent);
					c = new StaticIfCondition (x);

					if (!Expect (CloseParenthesis) && IsEOF)
						parent.Add (new DVariable{ Attributes = new List<DAttribute>{ c } });
				} else
					c = new StaticIfCondition (null);
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

			bool allowElse = laKind != Colon;

			var metaBlock = AttributeSpecifier(module, c, true) as AttributeMetaDeclaration;

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
				if (laKind == OpenCurlyBrace) {
					metaBlock.OptionalElseBlock = new ElseMetaDeclarationBlock {
						Location = t.Location,
						BlockStartLocation = la.Location
					};
					ClassBody (module, true, false);
				} else if (laKind == Colon) {
					metaBlock.OptionalElseBlock = new ElseMetaDeclarationSection { 
						Location = t.Location, 
						EndLocation =la.EndLocation };
					Step ();
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
			Expect(Module);
			var ret = new ModuleStatement { Location=t.Location };
			ret.ModuleName = ModuleFullyQualifiedName();
			Expect(Semicolon);
			ret.EndLocation = t.EndLocation;
			return ret;
		}

		ITypeDeclaration ModuleFullyQualifiedName()
		{
			if (!Expect (Identifier))
				return IsEOF ? new DTokenDeclaration(DTokens.Incomplete) : null;

			var td = new IdentifierDeclaration(t.Value) { Location=t.Location,EndLocation=t.EndLocation };

			while (laKind == Dot)
			{
				Step();
				if(Expect(Identifier))
					td = new IdentifierDeclaration(t.Value) { Location=t.Location, EndLocation=t.EndLocation, InnerDeclaration = td };
				else if(IsEOF)
					td = new IdentifierDeclaration(DTokens.IncompleteIdHash) { InnerDeclaration = td };
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

			Expect(Semicolon);

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
			if (Lexer.CurrentPeekToken.Kind == Assign)
			{
				if(Expect(Identifier))
					import.ModuleAlias = new IdentifierDeclaration(t.Value) { Location = t.Location, EndLocation = t.EndLocation };
				Step();
			}

			import.ModuleIdentifier = ModuleFullyQualifiedName();

			return import;
		}

		ImportStatement.ImportBindings ImportBindings(ImportStatement.Import imp)
		{
			var importBindings = new ImportStatement.ImportBindings { Module=imp };

			bool init = true;
			while (laKind == Comma || init)
			{
				if (init)
					init = false;
				else
					Step();

				var symbolAlias = Expect(Identifier) ? 
					new IdentifierDeclaration(t.Value){ Location = t.Location, EndLocation = t.EndLocation } :
					(IsEOF ? new IdentifierDeclaration(DTokens.IncompleteIdHash) : null);
						
				if (laKind == Assign)
				{
					Step();
					if (Expect (Identifier))
						importBindings.SelectedSymbols.Add (new ImportStatement.ImportBinding (new IdentifierDeclaration (t.Value) {
							Location = t.Location,
							EndLocation = t.EndLocation
						}, symbolAlias));
					else if(IsEOF)
						importBindings.SelectedSymbols.Add (new ImportStatement.ImportBinding (new IdentifierDeclaration (DTokens.IncompleteIdHash), symbolAlias));
				}
				else if(symbolAlias != null)
					importBindings.SelectedSymbols.Add(new ImportStatement.ImportBinding(symbolAlias));
			}

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

		bool CheckForStorageClasses(IBlockNode scope)
		{
			bool ret = false;
			while (IsStorageClass)
			{
				if (IsAttributeSpecifier) // extern, align
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

		public IEnumerable<INode> Declaration(IBlockNode Scope)
		{
			CheckForStorageClasses (Scope);
			
			switch (laKind)
			{
				case Alias:
				case Typedef:
					foreach (var e in AliasDeclaration(Scope))
						yield return e;
					break;
				case Struct:
				case Union:
					yield return AggregateDeclaration (Scope);
					break;
				case Enum:
					Step ();

					switch (laKind) {
						case Identifier:
							switch (Lexer.CurrentPeekToken.Kind) {
								case __EOF__:
								case EOF:
								case DTokens.Semicolon: // enum E;
								case DTokens.Colon: // enum E : int {...}
								case DTokens.OpenCurlyBrace: // enum E {...}
									yield return EnumDeclaration (Scope);
									yield break;
							}
							break;

						case __EOF__:
						case EOF:

						case DTokens.Semicolon: // enum;
						case DTokens.Colon: // enum : int {...}
						case DTokens.OpenCurlyBrace: // enum {...}
							yield return EnumDeclaration (Scope);
							yield break;
					}

					var enumAttr = new Modifier (DTokens.Enum) { Location = t.Location, EndLocation = t.EndLocation };
					PushAttribute (enumAttr, false);
					foreach (var i in Decl (Scope, enumAttr))
						yield return i;
					break;
				case Class:
					yield return ClassDeclaration (Scope);
					break;
				case Template:
					yield return TemplateDeclaration (Scope);
					break;
				case Mixin:
					if (Peek(1).Kind == Template)
						goto case Template;
					goto default;
				case Interface:
					yield return InterfaceDeclaration (Scope);
					break;
				case Ref:
					foreach (var i in Decl(Scope))
						yield return i;
					break;
				default:
					if (IsBasicType())
						goto case Ref;
					else if (IsEOF)
					{
						if (CheckForStorageClasses(Scope))
							goto case Ref;
						foreach (var i in Decl(Scope))
						{
							// If we're at EOF, there should only be exactly 1 node returned
							i.NameHash = 0;
							yield return i;
						}
						break;
					}
					SynErr(laKind,"Declaration expected, not "+GetTokenString(laKind));
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
			if ((laKind == Identifier && Lexer.CurrentPeekToken.Kind == This) ||
				(laKind == This && Lexer.CurrentPeekToken.Kind == Assign)){
				yield return AliasThisDeclaration(_t, Scope);
				yield break;
			}

			// AliasInitializerList
			else if(laKind == Identifier && (Lexer.CurrentPeekToken.Kind == Assign || 
				(Lexer.CurrentPeekToken.Kind == OpenParenthesis && OverPeekBrackets(OpenParenthesis) && Lexer.CurrentPeekToken.Kind == Assign)))
			{
				DVariable dv = null;
				do{
					if(laKind == Comma)
						Step();
					if(!Expect(Identifier))
						break;
					dv = new DVariable{
						IsAlias = true,
						Attributes = _t.Attributes,
						Description = _t.Description,
						Name = t.Value,
						NameLocation = t.Location,
						Location = t.Location,
						Parent = Scope
					};

					if(laKind == OpenParenthesis){
						var ep = new EponymousTemplate();
						ep.AssignFrom(dv);
						dv = ep;
						TemplateParameterList(ep);
					}

					if(Expect(Assign))
					{
						// alias fnRtlAllocateHeap = extern(Windows) void* function(void* HeapHandle, uint Flags, size_t Size) nothrow;
						CheckForStorageClasses(Scope);
						ApplyAttributes(dv);

						Lexer.PushLookAheadBackup();
						var wkTypeParsingBackup = AllowWeakTypeParsing;
						AllowWeakTypeParsing = true;
						dv.Type = Type(Scope);
						AllowWeakTypeParsing = wkTypeParsingBackup;
						if(!(laKind == Comma || laKind == Semicolon))
						{
							Lexer.RestoreLookAheadBackup();
							dv.Initializer = AssignExpression(Scope);
						}
						else
							Lexer.PopLookAheadBackup();
					}
					yield return dv;
				}
				while(laKind == Comma);

				Expect(Semicolon);
				if(dv != null)
					dv.Description += CheckForPostSemicolonComment();
				yield break;
			}

			// alias BasicType Declarator
			foreach(var n in Decl(Scope, laKind != Identifier || Lexer.CurrentPeekToken.Kind != OpenParenthesis ? null : new Modifier(DTokens.Alias), true))
			{
				var dv = n as DVariable;
				if (dv != null) {
					if (n.NameHash == DTokens.IncompleteIdHash && n.Type == null) // 'alias |' shall trigger completion, 'alias int |' not
						n.NameHash = 0;
					dv.Attributes.AddRange (_t.Attributes);
					dv.IsAlias = true;
				}
				yield return n;
			}
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

		IEnumerable<INode> Decl(IBlockNode Scope, DAttribute StorageClass = null, bool isAlias = false)
		{
			var startLocation = la.Location;
			var initialComment = GetComments();
			ITypeDeclaration ttd = null;

			CheckForStorageClasses(Scope);

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
			    laKind == (Identifier) && (DeclarationAttributes.Count > 0 || Lexer.CurrentPeekToken.Kind == OpenParenthesis))) { // public auto var=0; // const foo(...) {} 
				if (Lexer.CurrentPeekToken.Kind == Assign || Lexer.CurrentPeekToken.Kind == OpenParenthesis) {
				} else if (Lexer.CurrentPeekToken.Kind == Semicolon) {
					SemErr (t.Kind, "Initializer expected for auto type, semicolon found!");
				} else
					ttd = BasicType (Scope);
			} else if (!IsEOF) {
				// standalone this/super only allowed in alias declarations
				if (isAlias && (laKind == DTokens.This || laKind == DTokens.Super) && Lexer.CurrentPeekToken.Kind != DTokens.Dot) {
					ttd = new DTokenDeclaration (laKind) { Location = la.Location, EndLocation = la.EndLocation };
					Step ();
				}
				else
					ttd = BasicType (Scope);
			}


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
							yield break;
					}
				} else if (ttd is MemberFunctionAttributeDecl && (ttd as MemberFunctionAttributeDecl).InnerType == null) {
					yield break;
				}
			}

			// Declarators
			var firstNode = Declarator(ttd,false, Scope);
			if (firstNode == null)
				yield break;
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
					var init = Initializer (Scope);
					var dv = firstNode as DVariable;
					if (dv != null)
						dv.Initializer = init;
				}
				firstNode.EndLocation = t.EndLocation;
				yield return firstNode;

				// DeclaratorIdentifierList
				var otherNode = firstNode;
				while (laKind == Comma)
				{
					Step();
					if (IsEOF || Expect (Identifier)) {
						otherNode = new DVariable ();

						// Note: In DDoc, all declarations that are made at once (e.g. int a,b,c;) get the same pre-declaration-description!
						otherNode.Description = initialComment;

						otherNode.AssignFrom (firstNode);
						otherNode.Location = t.Location;
						if (t.Kind == DTokens.Identifier)
							otherNode.Name = t.Value;
						else if(IsEOF)
							otherNode.NameHash = IncompleteIdHash;
						otherNode.NameLocation = t.Location;

						if (laKind == OpenParenthesis)
							TemplateParameterList (otherNode);

						if (laKind == Assign)
							(otherNode as DVariable).Initializer = Initializer (Scope);

						otherNode.EndLocation = t.EndLocation;
						yield return otherNode;
					} else
						break;
				}

				Expect(Semicolon);

				// Note: In DDoc, only the really last declaration will get the post semicolon comment appended
				otherNode.Description += CheckForPostSemicolonComment();

				yield break;
			}

			// BasicType Declarator FunctionBody
			else if (firstNode is DMethod && (IsFunctionBody || IsEOF))
			{
				firstNode.Description += CheckForPostSemicolonComment();

				FunctionBody((DMethod)firstNode);

				firstNode.Description += CheckForPostSemicolonComment();

				yield return firstNode;
				yield break;
			}
			else
				SynErr(OpenCurlyBrace, "; or function body expected after declaration stub.");

			if (IsEOF)
				yield return firstNode;
		}

		bool IsBasicType()
		{
			return IsBasicType (la);
		}

		bool IsBasicType(DToken tk)
		{
			switch (tk.Kind) {
				case DTokens.Typeof:
				case DTokens.__vector:
				case DTokens.Identifier:
					return true;
				case DTokens.Dot:
					return tk.Next != null && tk.Next.Kind == (Identifier);
				case DTokens.This:
				case DTokens.Super:
					return tk.Next != null && tk.Next.Kind == DTokens.Dot;
				default:
					return IsBasicType (tk.Kind) || IsFunctionAttribute_(tk.Kind);
			}
		}

		/// <summary>
		/// Used if the parser is unsure if there's a type or an expression - then, instead of throwing exceptions, the Type()-Methods will simply return null;
		/// </summary>
		public bool AllowWeakTypeParsing = false;

		ITypeDeclaration BasicType(IBlockNode scope)
		{
			bool isModuleScoped = laKind == Dot;
			if (isModuleScoped)
				Step();

			ITypeDeclaration td = null;
			if (IsBasicType(laKind))
			{
				Step();
				return new DTokenDeclaration(t.Kind) { Location=t.Location, EndLocation=t.EndLocation };
			}

			if (IsMemberFunctionAttribute(laKind))
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
					md.InnerType = p ? Type(scope) : BasicType(scope);

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
				td = TypeOf(scope);
				if (laKind != Dot)
					return td;
                Step();
			}

			else if (laKind == __vector)
			{
				td = Vector(scope);
				if (laKind != Dot)
					return td;
                Step();
			}

			if (AllowWeakTypeParsing && laKind != Identifier)
				return null;

            if (td == null)
                td = IdentifierList(scope);
            else
            {
                var td_back = td;
                td = IdentifierList(scope);
                td.InnerMost = td_back;
            }

			if(isModuleScoped && td != null)
			{
				var innerMost = td.InnerMost;
				if (innerMost is IntermediateIdType)
					((IntermediateIdType)innerMost).ModuleScoped = true;
			}

			return td;
		}

		bool IsBasicType2()
		{
			return laKind == (Times) || laKind == (OpenSquareBracket) || laKind == (Delegate) || laKind == (Function);
		}

		ITypeDeclaration BasicType2(IBlockNode scope)
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

				var keyType = Type(scope);

				AllowWeakTypeParsing = weaktype;

				if (keyType != null && laKind == CloseSquareBracket && !(keyType is IdentifierDeclaration))
				{
					//HACK: Both new int[size_t] as well as new int[someConstNumber] are legal. So better treat them as expressions.
					cd = new ArrayDecl() { KeyType = keyType, Location = startLoc };
					Lexer.PopLookAheadBackup();
				}
				else
				{
					Lexer.RestoreLookAheadBackup();

					var fromExpression = AssignExpression(scope);

					// [ AssignExpression .. AssignExpression ]
					if (laKind == DoubleDot)
					{
						Step();
						cd = new ArrayDecl() {
							Location=startLoc,
							KeyType=null,
							KeyExpression= new PostfixExpression_ArrayAccess(fromExpression, AssignExpression(scope))};
					}
					else
						cd = new ArrayDecl() { KeyType=null, KeyExpression=fromExpression,Location=startLoc };
				}

				if ((AllowWeakTypeParsing && laKind != CloseSquareBracket))
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

				if (AllowWeakTypeParsing && laKind != OpenParenthesis)
					return null;

				var _dm = new DMethod();
				Parameters(_dm);
				dd.Parameters = _dm.Parameters;

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

		void ParseBasicType2(ref ITypeDeclaration td, IBlockNode scope)
		{
			if (td == null) {
				if (!IsBasicType2 ())
					return;

				td = BasicType2 (scope);
				if (td == null)
					return;
			}

			while (IsBasicType2())
			{
				var ttd = BasicType2(scope); 
				if (ttd != null)
					ttd.InnerDeclaration = td;
				else if (AllowWeakTypeParsing) {
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
		DNode Declarator(ITypeDeclaration basicType,bool IsParam, INode parent)
		{
			DNode ret = new DVariable() { Location = la.Location, Parent = parent };
			ApplyAttributes (ret);

			ParseBasicType2 (ref basicType, parent as IBlockNode);
			ret.Type = basicType;

			if (laKind != (OpenParenthesis))
			{
				// On external function declarations, no parameter names are required.
				// extern void Cfoo(HANDLE,char**);
				if (IsParam && laKind != (Identifier))
				{
					if(IsEOF)
					{
						var tokDecl = ret.Type as DTokenDeclaration;
						var ad = ret.Type as ArrayDecl;
						if ((tokDecl == null || tokDecl.Token != DTokens.Incomplete) && // 'T!|' or similar
							(ad == null || !(ad.KeyExpression is TokenExpression) || (ad.KeyExpression as TokenExpression).Token != DTokens.Incomplete)) // 'string[|'
							ret.NameHash = DTokens.IncompleteIdHash;
					}
					return ret;
				}

				if (Expect(Identifier))
				{
					ret.Name = t.Value;
					ret.NameLocation = t.Location;

					// enum asdf(...) = ...;
					if (laKind == OpenParenthesis && OverPeekBrackets(DTokens.OpenParenthesis, true) &&
						Lexer.CurrentPeekToken.Kind == Assign)
					{
						var eponymousTemplateDecl = new EponymousTemplate ();
						eponymousTemplateDecl.AssignFrom (ret);
						ret = eponymousTemplateDecl;

						TemplateParameterList (eponymousTemplateDecl);

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
					// Code error! - to prevent infinite declaration loops, step one token forward anyway!
					if(laKind != CloseCurlyBrace && laKind != CloseParenthesis)
						Step();
					return null;
				}
			}
			else
				OldCStyleFunctionPointer(ret, IsParam);

			if (IsDeclaratorSuffix || IsFunctionAttribute)
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
			ret.Type = cd;
			var deleg = cd as DelegateDeclaration;

			/*			 
			 * Parse all basictype2's that are following the initial '('
			 */
			ITypeDeclaration retType = null;
			ParseBasicType2 (ref retType, ret.Parent as IBlockNode);
			deleg.ReturnType = retType;

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
						DeclaratorSuffixes(ref ret);
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
		void DeclaratorSuffixes(ref DNode dn)
		{
			FunctionAttributes(ref dn.Attributes);

			while (laKind == (OpenSquareBracket))
			{
				Step();
				var ad = new ArrayDecl() { Location=t.Location,InnerDeclaration = dn.Type };

				if (laKind != (CloseSquareBracket))
				{
					ITypeDeclaration keyType=null;
					Lexer.PushLookAheadBackup();
					if (!IsAssignExpression())
					{
						var weakType = AllowWeakTypeParsing;
						AllowWeakTypeParsing = true;
						
						keyType= ad.KeyType = Type(dn.Parent as IBlockNode);

						AllowWeakTypeParsing = weakType;
					}
					if (keyType == null || laKind != CloseSquareBracket)
					{
						Lexer.RestoreLookAheadBackup();
						keyType = ad.KeyType = null;
						ad.KeyExpression = AssignExpression(dn.Parent as IBlockNode);
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
				if (dm == null)
				{
					dm = new DMethod();
					dm.AssignFrom(dn);
					dn = dm;
				}

				Parameters(dm);
			}

			FunctionAttributes(ref dn.Attributes);
		}

		public ITypeDeclaration IdentifierList(IBlockNode scope = null)
		{
			ITypeDeclaration td = null;

			switch (laKind) {
				case DTokens.This:
				case DTokens.Super:
					Step ();
					td = new DTokenDeclaration (t.Kind) { Location = t.Location, EndLocation = t.EndLocation };

					if (!Expect (DTokens.Dot))
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
					ttd = TemplateInstance(scope);
				else if (Expect(Identifier))
					ttd = new IdentifierDeclaration(t.Value) { Location = t.Location, EndLocation = t.EndLocation };
				else if (IsEOF)
					return new DTokenDeclaration(DTokens.Incomplete, td);
				else 
					ttd = null;
				if (ttd != null)
					ttd.InnerDeclaration = td;
				td = ttd;
			}
			while (laKind == Dot);

			return td;
		}

		new bool IsStorageClass
		{
			get
			{
				switch (laKind)
				{
					case Abstract:
					case Auto:
					case Deprecated:
					case Extern:
					case Final:
					case Override:
					case Scope:
					case Synchronized:
					case __gshared:
					case Ref:
					case At:
						return true;
					default:
						return IsAttributeSpecifier;
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
			return IsBasicType2() || laKind == (OpenParenthesis);
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
			if (laKind == (OpenParenthesis))
			{
				Step();
				td = Declarator2(scope);
				
				if (AllowWeakTypeParsing && (td == null||(t.Kind==OpenParenthesis && laKind==CloseParenthesis) /* -- means if an argumentless function call has been made, return null because this would be an expression */|| laKind!=CloseParenthesis))
					return null;

				Expect(CloseParenthesis);

				// DeclaratorSuffixes
				if (laKind == (OpenSquareBracket))
				{
					DNode dn = new DVariable();
					dn.Type = td;
					DeclaratorSuffixes(ref dn);
					td = dn.Type;

					if(dn.Attributes!= null && dn.Attributes.Count != 0)
						foreach(var attr in dn.Attributes)
							DeclarationAttributes.Push(attr);
				}
				return td;
			}

			ParseBasicType2 (ref td, scope);

			return td;
		}

		/// <summary>
		/// Parse parameters
		/// </summary>
		void Parameters(DMethod Parent)
		{
			var ret = Parent.Parameters;
			Expect(OpenParenthesis);

			// Empty parameter list
			if (laKind == (CloseParenthesis))
			{
				Step();
				return;
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
					dv.Type = new VarArgDecl();
					dv.Parent = Parent;
					ret.Add(dv);
				}
			}

			Expect(CloseParenthesis);
			BlockAttributes = stk_backup;
		}

		private DNode Parameter(IBlockNode Scope = null)
		{
			var attr = new List<DAttribute>();
			var startLocation = la.Location;

			CheckForStorageClasses (Scope);

			while ((IsParamModifier(laKind) && laKind != InOut) || (IsMemberFunctionAttribute(laKind) && Lexer.CurrentPeekToken.Kind != OpenParenthesis))
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

			var td = BasicType(Scope);

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

				var defInit = AssignExpression(Scope);

				var dv = ret as DVariable;
				if (dv!=null)
					dv.Initializer = defInit;
			}

			ret.EndLocation = IsEOF ? la.EndLocation : t.EndLocation;

			return ret;
		}

		private IExpression Initializer(IBlockNode Scope = null)
		{
			Expect(Assign);

			// VoidInitializer
			if (laKind == Void && Lexer.CurrentPeekToken.Kind != DTokens.Dot)
			{
				Step();
				return new VoidInitializer() { Location=t.Location,EndLocation=t.EndLocation};
			}

			return NonVoidInitializer(Scope);
		}

		IExpression NonVoidInitializer(IBlockNode Scope = null)
		{
			// ArrayInitializers are handled in PrimaryExpression(), whereas setting IsParsingInitializer to true is required!

			#region StructInitializer
			if (laKind == OpenCurlyBrace && IsStructInitializer)
			{
				// StructMemberInitializations
				var ae = new StructInitializer() { Location = la.Location };
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

					if (laKind == Identifier && Lexer.CurrentPeekToken.Kind == Colon)
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
			if (laKind == OpenSquareBracket && IsArrayInitializer)
				return ArrayLiteral(Scope, false);
			#endregion

			return AssignExpression(Scope);
		}

		/// <summary>
		/// Scan ahead to see if it is an array initializer or an expression.
		/// If it ends with a ';' ',' or '}', it is an array initializer.
		/// </summary>
		bool IsArrayInitializer
		{
			get{
				OverPeekBrackets (DTokens.OpenSquareBracket, laKind == OpenSquareBracket);
				var k = Lexer.CurrentPeekToken.Kind;
				return k == Comma || k == Semicolon || k == CloseCurlyBrace;
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

		TypeOfDeclaration TypeOf(IBlockNode scope)
		{
			Expect(Typeof);
			var md = new TypeOfDeclaration { Location = t.Location };

			if (Expect(OpenParenthesis))
			{
				if (laKind == Return)
				{
					Step();
					md.Expression = new TokenExpression(Return) { Location = t.Location, EndLocation = t.EndLocation };
				}
				else
					md.Expression = Expression(scope);
				Expect(CloseParenthesis);
			}
			md.EndLocation = t.EndLocation;
			return md;
		}

		VectorDeclaration Vector(IBlockNode scope)
		{
			var startLoc = t == null ? new CodeLocation() : t.Location;
			Expect(__vector);
			var md = new VectorDeclaration { Location = startLoc };

			if (Expect(OpenParenthesis))
			{
				if (IsAssignExpression())
					md.Id = Expression(scope);
				else
					md.IdDeclaration = Type(scope);
				Expect(CloseParenthesis);
			}

			md.EndLocation = t.EndLocation;
			return md;
		}

		#endregion

		#region Attributes

		DMethod _Invariant()
		{
            var inv = new DMethod { SpecialType= DMethod.MethodType.ClassInvariant };

			Expect(Invariant);
			inv.Location = t.Location;
			if (laKind == OpenParenthesis)
			{
				Step();
				Expect(CloseParenthesis);
			}
            if(!IsEOF)
			    inv.Body=BlockStatement(inv);
			inv.EndLocation = t.EndLocation;
			return inv;
		}

		PragmaAttribute _Pragma()
		{
			Expect(Pragma);
			var s = new PragmaAttribute { Location = t.Location };
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
				Expect (CloseParenthesis);
			}
			s.EndLocation = t.EndLocation;
			return s;
		}

		bool IsAttributeSpecifier
		{
			get
			{
				switch (laKind)
				{
					case Extern:
					case Export:
					case Align:
					case Pragma:
					case Deprecated:
					case Final:
					case Override:
					case Abstract:
					case Scope:
					case __gshared:
					case Synchronized:
					case At:
						return true;
					case Static:
						if (Lexer.CurrentPeekToken.Kind != If)
							return true;
						return false;
					case Auto:
						if (Lexer.CurrentPeekToken.Kind != OpenParenthesis && Lexer.CurrentPeekToken.Kind != Identifier)
							return true;
						return false;
					default:
						if (IsMemberFunctionAttribute(laKind))
							return Lexer.CurrentPeekToken.Kind != OpenParenthesis;
						return IsProtectionAttribute();
				}
			}
		}

		bool IsProtectionAttribute()
		{
			switch (laKind)
			{
				case Public:
				case Private:
				case Protected:
				case Extern:
				case Package:
					return true;
				default:
					return false;
			}
		}

		private void AttributeSpecifier(IBlockNode scope)
		{
			DAttribute attr;
			Modifier m;

			switch (laKind) {
				case DTokens.At:
				attr = AtAttribute(scope);
				break;

				case DTokens.Pragma:
				attr=_Pragma();
				break;

				case DTokens.Deprecated:
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
				break;

				case DTokens.Extern:
				attr = m = new Modifier (laKind, la.Value) { Location = la.Location };
				Step ();
				if (laKind == DTokens.OpenParenthesis) {
					Step(); // Skip (

					var sb = new StringBuilder ();
					// Check if EOF and append IncompleteID
					while (!IsEOF && laKind != CloseParenthesis)
					{
						Step();
						sb.Append(t.ToString());

						if (t.Kind == Identifier && laKind == Identifier)
							sb.Append(' ');
					}
					if (IsEOF)
						m.LiteralContent = DTokens.IncompleteId;
					else
						m.LiteralContent = sb.ToString();

					Expect (CloseParenthesis);
				}

				m.EndLocation = t.EndLocation;
				break;

				case DTokens.Align:
				attr = m = new Modifier (laKind, la.Value) { Location = la.Location };
				Step ();
				if (laKind == DTokens.OpenParenthesis) {
					Step();
					if (Expect(Literal))
						m.LiteralContent = new IdentifierExpression(t.LiteralValue, t.LiteralFormat);

					if (!Expect(CloseParenthesis))
						return;
				}

				m.EndLocation = t.EndLocation;
				break;

				case DTokens.Package:
				attr = m = new Modifier (laKind, la.Value) { Location = la.Location };
				Step ();

				if (laKind == OpenParenthesis) {
					// This isn't documented anywhere. http://dlang.org/attribute.html#ProtectionAttribute
					//TODO: Semantically handle this.
					Step ();
					m.LiteralContent = IdentifierList (scope); // Reassigns a symbol's package/'namespace' or so

					Expect (DTokens.CloseParenthesis);
				}

				m.EndLocation = t.EndLocation;
				break;

				default:
				attr = m = new Modifier (laKind, la.Value) { Location = la.Location };
				Step ();
				m.EndLocation = t.EndLocation;
				break;
			}

			//TODO: What about these semicolons after e.g. a pragma? Enlist these attributes anyway in the meta decl list?
			if (laKind != Semicolon)
			{
				if (scope is DBlockNode)
					AttributeSpecifier(scope as DBlockNode, attr);
				else
					PushAttribute(attr, false);
			}
		}
		
		/// <summary>
		/// Parses an attribute that starts with an @. Might be user-defined or a built-in attribute.
		/// Due to the fact that
		/// </summary>
		AtAttribute AtAttribute(IBlockNode scope)
		{
			var sl = la.Location;
			Expect(At);

			if (laKind == Identifier)
			{
				var att = BuiltInAtAttribute.BuiltInAttributes.None;
				switch (la.Value)
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

				if (att != BuiltInAtAttribute.BuiltInAttributes.None)
				{
					Step();
					return new BuiltInAtAttribute(att) { Location = sl, EndLocation = t.EndLocation };
				}
			}
			else if (laKind == OpenParenthesis)
			{
				Step();
				var args = ArgumentList(scope);
				Expect(CloseParenthesis);
				return new UserDeclarationAttribute(args.ToArray()) { Location = sl, EndLocation = t.EndLocation };
			}

			var x = PostfixExpression(scope);
			return new UserDeclarationAttribute(x != null ? new[]{ x } : null) { Location = sl, EndLocation = t.EndLocation };
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="module"></param>
		/// <param name="previouslyParsedAttribute"></param>
		/// <param name="RequireDeclDef">If no colon and no open curly brace is given as lookahead, a DeclDef may be parsed otherwise, if parameter is true.</param>
		/// <returns></returns>
		IMetaDeclaration AttributeSpecifier(DBlockNode module, DAttribute previouslyParsedAttribute, bool RequireDeclDef = false)
		{
			DAttribute[] attrs;

			if (laKind == Colon)
			{
				Step();
				PushAttribute(previouslyParsedAttribute, true);

				attrs = new DAttribute[1 + DeclarationAttributes.Count];
				DeclarationAttributes.CopyTo(attrs, 0);
				DeclarationAttributes.Clear();
				attrs[attrs.Length - 1] = previouslyParsedAttribute;

				AttributeMetaDeclarationSection metaDecl = null;
				//TODO: Put all remaining block/decl(?) attributes into the section definition..
				if(module!=null)
					module.Add(metaDecl = new AttributeMetaDeclarationSection(attrs) { EndLocation = t.EndLocation });
				return metaDecl;
			}
			else 
				PushAttribute(previouslyParsedAttribute, false);

			if (laKind == OpenCurlyBrace)
				return AttributeBlock(module);
			else
			{
				if (IsEOF && module != null && previouslyParsedAttribute != null) // To enable attribute completion, add dummy node
					module.Add (new DVariable{ Attributes = new List<DAttribute>{ previouslyParsedAttribute } });

				if (RequireDeclDef)
				{
					DeclDef(module);

					attrs = new DAttribute[1 + DeclarationAttributes.Count];
					DeclarationAttributes.CopyTo(attrs, 0);
					DeclarationAttributes.Clear();
					attrs[attrs.Length - 1] = previouslyParsedAttribute;

					return new AttributeMetaDeclaration(attrs) { EndLocation = previouslyParsedAttribute.EndLocation };
				}
			}

			return null;
		}
		
		bool IsFunctionAttribute
		{
			get { return IsFunctionAttribute_(laKind); }
		}

		bool IsFunctionAttribute_(byte kind)
		{
			return IsMemberFunctionAttribute(kind) || kind == At;
		}


		void FunctionAttributes(DNode n)
		{
			FunctionAttributes(ref n.Attributes);
		}

		void FunctionAttributes(ref List<DAttribute> attributes)
		{
			DAttribute attr=null;
			attributes = attributes ?? new List<DAttribute> ();
			while (IsFunctionAttribute)
			{
                if(laKind == At)
                	attr = AtAttribute(null);
                else
                {
					attributes.Add(attr = new Modifier(laKind, la.Value) { Location = la.Location, EndLocation = la.EndLocation });
					Step();
                }
			}
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
			if (IsStorageClass)
				return false;

			if (!IsBasicType ())
				return true;

			if (DTokens.IsBasicType (laKind)) {
				if(Lexer.CurrentPeekToken.Kind != Dot && Peek().Kind != Identifier)
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
				if (laKind == Dot)
					Peek ();

				if (Lexer.CurrentPeekToken.Kind != Identifier)
				{
					if (laKind == Identifier || laKind == Dot)
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
							bool isPrimitiveExpr = Lexer.CurrentPeekToken.Kind == Dot || Lexer.CurrentPeekToken.Kind == OpenParenthesis;
							Peek(1);
							return isPrimitiveExpr;
						}
						else if (laKind == Typeof || laKind == __vector)
							OverPeekBrackets(OpenParenthesis);
					}
				}
			}

			if (Lexer.CurrentPeekToken == null)
				Peek();


			// Skip basictype2's
			bool HadPointerDeclaration = false;
			while (Lexer.CurrentPeekToken.Kind == Times || Lexer.CurrentPeekToken.Kind == OpenSquareBracket)
			{
				if (Lexer.CurrentPeekToken.Kind == Times) {
					HadPointerDeclaration = true;
					Peek ();
					if (Lexer.CurrentPeekToken.Kind == Literal) { // char[a.member*8] abc; // conv.d:3278
						Peek(1);
						return true;
					}
				}

				else // if (Lexer.CurrentPeekToken.Kind == OpenSquareBracket)
				{
					Peek ();
					if (IsBasicType (Lexer.CurrentPeekToken) && !(Lexer.CurrentPeekToken.Kind == DTokens.Identifier || Lexer.CurrentPeekToken.Kind == DTokens.Dot)) {
						Peek (1);
						return false;
					}
					OverPeekBrackets(OpenSquareBracket, true);
					if (Lexer.CurrentPeekToken.Kind == DTokens.EOF) // Due to completion purposes
						return true;
				}
			}

			var pkKind = Lexer.CurrentPeekToken.Kind;
			Peek (1);

			// And now, after having skipped the basictype and possible trailing basictype2's,
			// we check for an identifier or delegate declaration to ensure that there's a declaration and not an expression
			// Addition: If a times token ('*') follows an identifier list, we can assume that we have a declaration and NOT an expression!
			// Example: *a=b is an expression; a*=b is not possible (and a Multiply-Assign-Expression) - instead something like A* a should be taken...
			switch (pkKind) {
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
			if (!IsAssignOperator(laKind))
				return left;

			Step();
			var ate = new AssignExpression(t.Kind);
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
			return se;
		}

		IExpression OrOrExpression(IBlockNode Scope = null)
		{
			var left = AndAndExpression(Scope);
			if (laKind != LogicalOr)
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
			if (laKind != LogicalAnd)
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
			if (laKind != BitwiseOr)
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
			if (laKind != Xor)
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
			if (laKind != BitwiseAnd)
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

			switch (laKind) {
			case DTokens.Equal:
			case DTokens.NotEqual:
				ae = new EqualExpression (laKind == NotEqual);
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
				ae = new RelExpression (laKind);
				break;

			case DTokens.Is:
				ae = new IdentityExpression (false);
				break;

			case DTokens.In:
				ae = new InExpression (false);
				break;

			case Not:
				switch (Peek (1).Kind) {
				case DTokens.Is:
					ae = new IdentityExpression (false);
					Step ();
					break;
				case DTokens.In:
					ae = new InExpression (true);
					Step ();
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
			if (!(laKind == ShiftLeft || laKind == ShiftRight || laKind == ShiftRightUnsigned))
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
				case BitwiseAnd:
				case Increment:
				case Decrement:
				case Times:
				case Minus:
				case Plus:
				case Not:
				case Tilde:
					Step();
					SimpleUnaryExpression sue;
					switch (t.Kind)
					{
						case BitwiseAnd:
							sue = new UnaryExpression_And();
							break;
						case Increment:
							sue = new UnaryExpression_Increment();
							break;
						case Decrement:
							sue = new UnaryExpression_Decrement();
							break;
						case Times:
							sue = new UnaryExpression_Mul();
							break;
						case Minus:
							sue = new UnaryExpression_Sub();
							break;
						case Plus:
							sue = new UnaryExpression_Add();
							break;
						case Tilde:
							sue = new UnaryExpression_Cat();
							break;
						case Not:
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
				case Cast:
					Step();
					var ce = new CastExpression { Location= t.Location };

					if (Expect(OpenParenthesis))
					{
						if (laKind != CloseParenthesis) // Yes, it is possible that a cast() can contain an empty type!
							ce.Type = Type(Scope);
						Expect(CloseParenthesis);
					}
					ce.UnaryExpression = UnaryExpression(Scope);
					ce.EndLocation = t.EndLocation;
					return ce;

				// DeleteExpression
				case Delete:
					Step();
					return new DeleteExpression() { UnaryExpression = UnaryExpression(Scope) };

				// PowExpression
				default:
					var left = PostfixExpression(Scope);

					if (laKind != Pow)
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
				var ac = new AnonymousClassExpression();
				ac.NewArguments = newArgs;
				ac.Location = startLoc;

				// ClassArguments
				if (laKind == (OpenParenthesis))
				{
					Step();
					if(laKind != DTokens.CloseParenthesis)
						ac.ClassArguments = ArgumentList(Scope).ToArray();
					Expect(DTokens.CloseParenthesis);
				}

				var anclass = new DClassLike(Class) { IsAnonymousClass=true,
					Location = startLoc
				};

				// BaseClasslist_opt
				if (laKind != DTokens.OpenCurlyBrace){
					BaseClassList(anclass, laKind == Colon);
					// SuperClass_opt InterfaceClasses_opt
					if (laKind != OpenCurlyBrace)
						BaseClassList(anclass,false);
				}

				ClassBody(anclass);

				ac.AnonymousClass = anclass;

				ac.EndLocation = t.EndLocation;

				if (Scope != null && !AllowWeakTypeParsing)
					Scope.Add(ac.AnonymousClass);

				return ac;
			}

			// NewArguments Type
			else
			{
				var nt = BasicType(Scope);
				ParseBasicType2 (ref nt, Scope);

				var initExpr = new NewExpression()
				{
					NewArguments = newArgs,
					Type=nt,
					Location=startLoc
				};

				List<IExpression> args;

				var ad=nt as ArrayDecl;

				if ((ad == null || ad.ClampsEmpty) && laKind == OpenParenthesis) {
					Step ();
					if (laKind != CloseParenthesis)
						args = ArgumentList (Scope);
					else
						args = new List<IExpression> ();

					if (Expect (CloseParenthesis))
						initExpr.EndLocation = t.EndLocation;
					else
						initExpr.EndLocation = CodeLocation.Empty;

					if (ad != null) {
						if (args.Count == 0) {
							SemErr (CloseParenthesis, "Size for the rightmost array dimension needed");

							initExpr.EndLocation = t.EndLocation;
							return initExpr;
						}

						while (ad != null) {
							if (args.Count == 0)
								break;

							ad.KeyType = null;
							ad.KeyExpression = args [args.Count - 1];

							args.RemoveAt (args.Count - 1);

							ad = ad.InnerDeclaration as ArrayDecl;
						}
					}
				} else {
					initExpr.EndLocation = t.EndLocation;
					args = new List<IExpression> ();
				}

				ad = nt as ArrayDecl;

				if (ad != null && ad.KeyExpression == null)
				{
					if (ad.KeyType == null)
						SemErr(CloseSquareBracket, "Size of array expected");
				}

				initExpr.Arguments = args.ToArray();

				return initExpr;
			}
		}

		public List<IExpression> ArgumentList(IBlockNode Scope = null)
		{
			var ret = new List<IExpression>();

			if (laKind == CloseParenthesis)
				return ret;

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

					var td = Type(Scope);

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
							if ((laKind == DTokens.Identifier && Peek(1).Kind != Not && Peek(1).Kind != OpenParenthesis) || IsEOF)
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

			while (!IsEOF)
			{
				switch (laKind)
				{
					case Dot:
						Step();

						var pea = new PostfixExpression_Access { 
							PostfixForeExpression = leftExpr
						};

						leftExpr = pea;

						if (laKind == New)
							pea.AccessExpression = PostfixExpression(Scope);
						else if (IsTemplateInstance)
							pea.AccessExpression = TemplateInstance(Scope);
						else if (Expect(Identifier))
							pea.AccessExpression = new IdentifierExpression(t.Value) {
								Location = t.Location,
								EndLocation = t.EndLocation
							};
						else if (IsEOF)
							pea.AccessExpression = new TokenExpression(DTokens.Incomplete);

						pea.EndLocation = t.EndLocation;
						break;
					case Increment:
					case Decrement:
						Step();
						var peid = t.Kind == Increment ? (PostfixExpression)new PostfixExpression_Increment() : new PostfixExpression_Decrement();
						peid.EndLocation = t.EndLocation;					
						peid.PostfixForeExpression = leftExpr;
						leftExpr = peid;
						break;
					// Function call
					case OpenParenthesis:
						Step();
						var pemc = new PostfixExpression_MethodCall();
						pemc.PostfixForeExpression = leftExpr;
						leftExpr = pemc;

						if (laKind == CloseParenthesis)
							Step();
						else
						{
							pemc.Arguments = ArgumentList(Scope).ToArray();
							Expect(CloseParenthesis);
						}

						if(IsEOF)
							pemc.EndLocation = CodeLocation.Empty;
						else
							pemc.EndLocation = t.EndLocation;
						break;
					// IndexExpression | SliceExpression
					case OpenSquareBracket:
						Step ();
						var loc = t.Location;
						var args = new List<PostfixExpression_ArrayAccess.IndexArgument> ();

						if (laKind != CloseSquareBracket) {
							do {
								var firstEx = AssignExpression (Scope);
								// [ AssignExpression .. AssignExpression ] || ArgumentList
								if (laKind == DoubleDot) {
									Step ();
									args.Add (new PostfixExpression_ArrayAccess.SliceArgument (firstEx, AssignExpression (Scope)));
								} else
									args.Add (new PostfixExpression_ArrayAccess.IndexArgument (firstEx));
							} while(laKind == Comma && Expect (Comma) &&
								laKind != CloseSquareBracket); // Trailing comma allowed https://github.com/aBothe/D_Parser/issues/170
						}

						Expect (CloseSquareBracket);
						leftExpr = new PostfixExpression_ArrayAccess(args.ToArray()){ 
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

		IExpression PrimaryExpression(IBlockNode Scope=null)
		{
			bool isModuleScoped = laKind == Dot;
			if (isModuleScoped)
			{
				Step();
				if (IsEOF)
				{
					var dot = new TokenExpression(Dot) { Location = t.Location, EndLocation = t.EndLocation };
					return new PostfixExpression_Access{ PostfixForeExpression = dot, AccessExpression = new TokenExpression(DTokens.Incomplete) };
				}
			}

			// TemplateInstance
			if (IsTemplateInstance)
			{
				var tix = TemplateInstance(Scope);
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
				case OpenSquareBracket:
					return ArrayLiteral(Scope);
				case New:
					return NewExpression(Scope);
				case Typeof:
					return new TypeDeclarationExpression(TypeOf(Scope));
				case __traits:
					return TraitsExpression(Scope);
				// Dollar (== Array length expression)
				case Dollar:
					Step();
					return new TokenExpression(t.Kind)
					{
						Location = t.Location,
						EndLocation = t.EndLocation
					};
				case Identifier:
					Step();
					return new IdentifierExpression(t.Value)
					{
						Location = t.Location,
						EndLocation = t.EndLocation,
						ModuleScoped = isModuleScoped
					};
				// SpecialTokens (this,super,null,true,false,$) // $ has been handled before
				case This:
				case Super:
				case Null:
				case True:
				case False:
					Step();
					return new TokenExpression(t.Kind)
					{
						Location = t.Location,
						EndLocation = t.EndLocation
					};
				case OpenParenthesis:
					if (IsFunctionLiteral())
						goto case Function;
					// ( Expression )
					Step();
					var ret = new SurroundingParenthesesExpression() {Location=t.Location };

					ret.Expression = Expression();

					Expect(CloseParenthesis);
					ret.EndLocation = t.EndLocation;
					return ret;
				case Literal:
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
						return new IdentifierExpression(sb.ToString(), t.LiteralFormat, t.Subformat) { Location = startLoc, EndLocation = t.EndLocation };
					}
					//else if (t.LiteralFormat == LiteralFormat.CharLiteral)return new IdentifierExpression(t.LiteralValue) { LiteralFormat=t.LiteralFormat,Location = startLoc, EndLocation = t.EndLocation };
					return new IdentifierExpression(t.LiteralValue, t.LiteralFormat, t.Subformat, t.RawCodeRepresentation) { Location = startLoc, EndLocation = t.EndLocation };
				// FunctionLiteral
				case Delegate:
				case Function:
				case OpenCurlyBrace:
					var fl = new FunctionLiteral() { Location=la.Location};
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
							fl.AnonymousMethod.Type = BasicType(Scope);
						else if (laKind != OpenParenthesis && laKind != OpenCurlyBrace)
							fl.AnonymousMethod.Type = Type(Scope);

						if (laKind == OpenParenthesis)
							Parameters(fl.AnonymousMethod);

						FunctionAttributes(fl.AnonymousMethod);
					}

					FunctionBody(fl.AnonymousMethod);

					if(IsEOF)
						fl.AnonymousMethod.EndLocation = CodeLocation.Empty;

					fl.EndLocation = fl.AnonymousMethod.EndLocation;

					if (Scope != null && !AllowWeakTypeParsing) // HACK -- not only on AllowWeakTypeParsing! But apparently, this stuff may be parsed twice, so force-skip results of the first attempt although this is a rather stupid solution
						Scope.Add(fl.AnonymousMethod);

					return fl;
				// AssertExpression
				case Assert:
					Step();
					startLoc = t.Location;
					Expect(OpenParenthesis);
					var ce = new AssertExpression() { Location=startLoc};

					var exprs = new List<IExpression>();
					var assertedExpr = AssignExpression(Scope);
					if(assertedExpr!=null)
						exprs.Add(assertedExpr);

					if (laKind == (Comma))
					{
						Step();
						assertedExpr = AssignExpression(Scope);
						if (assertedExpr != null)
							exprs.Add(assertedExpr);
					}
					ce.AssignExpressions = exprs.ToArray();
					Expect(CloseParenthesis);
					ce.EndLocation = t.EndLocation;
					return ce;
				// MixinExpression
				case Mixin:
					Step();
					var me = new MixinExpression() { Location=t.Location};
					if (Expect(OpenParenthesis))
					{
						me.AssignExpression = AssignExpression(Scope);
						Expect(CloseParenthesis);
					}
					me.EndLocation = t.EndLocation;
					return me;
				// ImportExpression
				case Import:
					Step();
					var ie = new ImportExpression() { Location=t.Location};
					Expect(OpenParenthesis);

                    ie.AssignExpression = AssignExpression(Scope);

					Expect(CloseParenthesis);
					ie.EndLocation = t.EndLocation;
					return ie;
				// TypeidExpression
				case Typeid:
					Step();
					var tide = new TypeidExpression() { Location=t.Location};
					Expect(OpenParenthesis);

					if (IsAssignExpression())
						tide.Expression = AssignExpression(Scope);
					else
					{
						Lexer.PushLookAheadBackup();
						AllowWeakTypeParsing = true;
						tide.Type = Type(Scope);
						AllowWeakTypeParsing = false;

						if (tide.Type == null || laKind != CloseParenthesis)
						{
							Lexer.RestoreLookAheadBackup();
							tide.Expression = AssignExpression(Scope);
						}
						else
							Lexer.PopLookAheadBackup();
					}

					Expect (CloseParenthesis);

					tide.EndLocation = t.EndLocation;
					return tide;
				// IsExpression
				case Is:
					Step ();
					var ise = new IsExpression () { Location = t.Location };
					Expect (OpenParenthesis);

					if (laKind == DTokens.This && Lexer.CurrentPeekToken.Kind != DTokens.Dot) {
						Step ();
						ise.TestedType = new DTokenDeclaration (DTokens.This) { Location = t.Location, EndLocation = t.EndLocation };
					} else
						ise.TestedType = Type (Scope);

					if (ise.TestedType == null)
						SynErr(laKind, "In an IsExpression, either a type or an expression is required!");

					if (ise.TestedType != null)
					{
						if (laKind == Identifier && (Lexer.CurrentPeekToken.Kind == CloseParenthesis || Lexer.CurrentPeekToken.Kind == Equal
						    || Lexer.CurrentPeekToken.Kind == Colon))
						{
							Step();
							Strings.Add(strVal);
							ise.TypeAliasIdentifierHash = strVal.GetHashCode();
							ise.TypeAliasIdLocation = t.Location;
						}
						else if (IsEOF)
							ise.TypeAliasIdentifierHash = DTokens.IncompleteIdHash;
					}

					if (laKind == Colon || laKind == Equal)
					{
						Step();
						ise.EqualityTest = t.Kind == Equal;
					}
					else if (laKind == CloseParenthesis)
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
							case Typedef: // typedef is possible although it's not yet documented in the syntax docs
							case Enum:
							case Delegate:
							case Function:
							case Super:
							case Return:
								specialTest = true;
								break;
							case Const:
							case Immutable:
							case InOut:
							case Shared:
								specialTest = Peek(1).Kind == CloseParenthesis || Lexer.CurrentPeekToken.Kind == Comma;
								break;
							default:
								specialTest = IsClassLike(laKind);
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
						ise.TypeSpecialization = Type(Scope);

					// TemplateParameterList
					if (laKind == Comma)
					{
						var tempParam = new List<TemplateParameter>();
						do
						{
							Step();
							tempParam.Add(TemplateParameter(Scope as DNode));
						}
						while (laKind == Comma);
						ise.TemplateParameterList = tempParam.ToArray();
					}

					Expect(CloseParenthesis);
					ise.EndLocation = t.EndLocation;
					return ise;
				default:
					if (DTokens.IsMetaIdentifier(laKind))
						goto case Dollar;
					else if (IsBasicType())
					{
						startLoc = la.Location;

						var bt=BasicType(Scope);

						switch (laKind)
						{
							case DTokens.Dot: // BasicType . Identifier
								Step();
								// Things like incomplete 'float.' expressions shall be parseable, too
								if (Expect(Identifier) || IsEOF)
									return new PostfixExpression_Access()
									{
										PostfixForeExpression = new TypeDeclarationExpression(bt),
										AccessExpression = IsEOF ? new TokenExpression(Incomplete) as IExpression : new IdentifierExpression(t.Value) { Location = t.Location, EndLocation = t.EndLocation },
										EndLocation = t.EndLocation
									};
								break;
							case DTokens.OpenParenthesis:
								Step();

								var callExp = new PostfixExpression_MethodCall { PostfixForeExpression = new TypeDeclarationExpression(bt) };
								callExp.Arguments = ArgumentList(Scope).ToArray();

								Expect(DTokens.CloseParenthesis);
								return callExp;
							default:
								if (bt is TypeOfDeclaration || bt is MemberFunctionAttributeDecl)
									return new TypeDeclarationExpression(bt);
								break;
						}

						return null;
					}

					SynErr(Identifier);
					if(laKind != CloseCurlyBrace)
						Step();

					if (IsEOF)
						return new TokenExpression (DTokens.Incomplete) { Location = t.Location, EndLocation = t.Location };

					// Don't know why, in rare situations, t tends to be null..
					if (t == null)
						return null;
					return new TokenExpression() { Location = t.Location, EndLocation = t.EndLocation };
			}
		}

		IExpression ArrayLiteral(IBlockNode scope, bool nonInitializer = true)
		{
			Expect (OpenSquareBracket);
			var startLoc = t.Location;

			// Empty array literal
			if (laKind == CloseSquareBracket)
			{
				Step();
				return new ArrayLiteralExpression(null) {Location=startLoc, EndLocation = t.EndLocation };
			}

			var firstExpression = nonInitializer ? AssignExpression(scope) : NonVoidInitializer(scope);

			// Associtative array
			if (laKind == Colon)
			{
				Step();

				var ae = nonInitializer ? new AssocArrayExpression { Location=startLoc } : new ArrayInitializer{ Location = startLoc };

				var firstValueExpression = nonInitializer ? AssignExpression(scope) : NonVoidInitializer(scope);

				ae.Elements.Add(new KeyValuePair<IExpression,IExpression>(firstExpression, firstValueExpression));

				while (laKind == Comma)
				{
					Step();

					if (laKind == CloseSquareBracket)
						break;

					var keyExpr = nonInitializer ? AssignExpression(scope) : NonVoidInitializer(scope);
					IExpression valExpr;
					if (laKind == DTokens.Colon) { // http://dlang.org/expression.html#AssocArrayLiteral Spec failure
						Step ();
						valExpr = nonInitializer ? AssignExpression (scope) : NonVoidInitializer (scope);
					}
					else {
						valExpr = keyExpr;
						keyExpr = null; // Key will be deduced by incrementing the first key value ever given in the literal
					}

					ae.Elements.Add(new KeyValuePair<IExpression,IExpression>(keyExpr,valExpr));
				}

				Expect(CloseSquareBracket);
				ae.EndLocation = t.EndLocation;
				return ae;
			}
			else // Normal array literal
			{
				var ae = new List<IExpression>();
				if(firstExpression != null)
					ae.Add(firstExpression);

				while (laKind == Comma)
				{
					Step();
					if (laKind == CloseSquareBracket) // And again, empty expressions are allowed
						break;
					ae.Add(nonInitializer ? AssignExpression(scope) : NonVoidInitializer(scope));
				}

				Expect(CloseSquareBracket);
				return new ArrayLiteralExpression(ae){ Location=startLoc, EndLocation = t.EndLocation };
			}
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

			var k = Lexer.CurrentPeekToken.Kind;
			// (string |
			// (const |
			// (string a, |
			// (char[] |
			// (char a |
			// NOT (char* |
			if (k == __EOF__ || k == EOF) {
				var pk = Lexer.CurrentPeekToken;
				var next = la;
				while (pk != next.next)
					next = next.next;

				k = next.Kind;
				return k == Comma || k == Identifier || k == CloseSquareBracket || IsBasicType(k) || IsStorageClass(k);
			}

			// (...) => | 
			// (...) pure @nothrow => |
			return k == GoesTo || IsFunctionAttribute_(k);
		}

		bool IsFunctionLiteral()
		{
			if (laKind != OpenParenthesis)
				return false;

			Lexer.StartPeek();

			OverPeekBrackets(OpenParenthesis, false);

			bool at = false;
			while (DTokens.IsStorageClass(Lexer.CurrentPeekToken.Kind) || (at = Lexer.CurrentPeekToken.Kind == At)) {
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
				Parameters(fl.AnonymousMethod);

			LambdaBody(fl.AnonymousMethod);
			fl.EndLocation = fl.AnonymousMethod.EndLocation;

			if (Scope != null && !AllowWeakTypeParsing)
				Scope.Add(fl.AnonymousMethod);

			return fl;
		}

		void LambdaBody(DMethod anonymousMethod)
		{
			FunctionAttributes (anonymousMethod);

			if (laKind == OpenCurlyBrace)
			{
				anonymousMethod.Body = BlockStatement (anonymousMethod);
				anonymousMethod.EndLocation = anonymousMethod.Body.EndLocation;
			}
			else if (Expect(GoesTo))
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
		#endregion

		#region Statements
		void IfCondition(IfStatement par, IBlockNode scope)
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
				tp = Type(scope);

			AllowWeakTypeParsing = wkType;

			if (tp != null && ((laKind == Identifier &&
				(Peek(1).Kind == Assign || Lexer.CurrentPeekToken.Kind == CloseParenthesis)) || // if(a * b * c) is an expression, if(a * b = 123) may be a pointer variable
				(IsEOF && tp.InnerDeclaration == null))) // if(inst. is an expression, TODO if(int. not
			{
				Lexer.PopLookAheadBackup ();
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
					dv.Initializer = Expression(scope);
					dv.EndLocation = t.EndLocation;
				}

				par.IfVariable = dv;
				return;
			}
				
			Lexer.RestoreLookAheadBackup();
			par.IfCondition = Expression(scope);
		}

		public bool IsStatement
		{
			get
			{
				switch (laKind)
				{
					case OpenCurlyBrace:
					case If:
					case While:
					case Do:
					case For:
					case Foreach:
					case Foreach_Reverse:
					case Switch:
					case Case:
					case Default:
					case Continue:
					case Break:
					case Return:
					case Goto:
					case With:
					case Synchronized:
					case Try:
					case Throw:
					case Scope:
					case Asm:
					case Pragma:
					case Mixin:
					case Version:
					case Debug:
					case Assert:
					case Volatile:
						return true;
					case Static:
						return Lexer.CurrentPeekToken.Kind == If || Lexer.CurrentPeekToken.Kind == Assert;
					case Final:
						return Lexer.CurrentPeekToken.Kind == Switch;
					case Identifier:
						return Peek(1).Kind == Colon;
					default:
						return false;
				}
			}
		}

		public IStatement Statement(bool BlocksAllowed = true, bool EmptyAllowed = true, IBlockNode Scope = null, IStatement Parent=null)
		{
			switch (laKind)
			{
				case Semicolon:
					if (!EmptyAllowed)
						goto default;
					Step();
					return null;
				case OpenCurlyBrace:
					if (!BlocksAllowed)
						goto default;
					return BlockStatement(Scope,Parent);
				// LabeledStatement (loc:... goto loc;)
				case Identifier:
					if (Lexer.CurrentPeekToken.Kind != Colon)
						goto default;
					Step();

					var ls = new LabeledStatement() { Location = t.Location, Identifier = t.Value, Parent = Parent };
					Step();
					ls.EndLocation = t.EndLocation;

					return ls;
				// IfStatement
				case If:
					Step();

					var iS = new IfStatement{	Location = t.Location, Parent = Parent	};

					Expect(OpenParenthesis);
					// IfCondition
					IfCondition(iS, Scope);

					// ThenStatement
					if(Expect(CloseParenthesis))
						iS.ThenStatement = Statement(Scope: Scope, Parent: iS);

					// ElseStatement
					if (laKind == (Else))
					{
						Step();
						iS.ElseStatement = Statement(Scope: Scope, Parent: iS);
					}

					if(t != null)
						iS.EndLocation = t.EndLocation;

					return iS;
				// Conditions
				case Version:
				case Debug:
					return StmtCondition(Parent, Scope);
				case Static:
					if (Lexer.CurrentPeekToken.Kind == If)
						return StmtCondition(Parent, Scope);
					else if (Lexer.CurrentPeekToken.Kind == Assert)
						goto case Assert;
					else if (Lexer.CurrentPeekToken.Kind == Import)
						goto case Import;
					goto default;
				case For:
					return ForStatement(Scope, Parent);
				case Foreach:
				case Foreach_Reverse:
					return ForeachStatement(Scope, Parent);
				case While:
					Step();

					var ws = new WhileStatement() { Location = t.Location, Parent = Parent };

					Expect(OpenParenthesis);
					ws.Condition = Expression(Scope);
					Expect(CloseParenthesis);

					if(!IsEOF)
					{
						ws.ScopedStatement = Statement(Scope: Scope, Parent: ws);
						ws.EndLocation = t.EndLocation;
					}

					return ws;
				case Do:
					Step();

					var dws = new WhileStatement() { Location = t.Location, Parent = Parent };
					if(!IsEOF)
						dws.ScopedStatement = Statement(true, false, Scope, dws);

					if(Expect(While) && Expect(OpenParenthesis))
					{
						dws.Condition = Expression(Scope);
						Expect(CloseParenthesis);
						Expect(Semicolon);

						dws.EndLocation = t.EndLocation;
					}

					return dws;
				// [Final] SwitchStatement
				case Final:
					if (Lexer.CurrentPeekToken.Kind != Switch)
						goto default;
					goto case Switch;
				case Switch:
					var ss = new SwitchStatement { Location = la.Location, Parent = Parent };
					if (laKind == (Final))
					{
						ss.IsFinal = true;
						Step();
					}
					Step();
					Expect(OpenParenthesis);
					ss.SwitchExpression = Expression(Scope);
					Expect(CloseParenthesis);

					if(!IsEOF)
						ss.ScopedStatement = Statement(Scope: Scope, Parent: ss);
					ss.EndLocation = t.EndLocation;

					return ss;
				case Case:
					Step();

					var sscs = new SwitchStatement.CaseStatement() { Location = la.Location, Parent = Parent };
					sscs.ArgumentList = Expression(Scope);

					Expect(Colon);

					// CaseRangeStatement
					if (laKind == DoubleDot)
					{
						Step();
						Expect(Case);
						sscs.LastExpression = AssignExpression();
						Expect(Colon);
					}

					var sscssl = new List<IStatement>();

					while (laKind != Case && laKind != Default && laKind != CloseCurlyBrace && !IsEOF)
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
				case Default:
					Step();

					var ssds = new SwitchStatement.DefaultStatement()
					{
						Location = la.Location,
						Parent = Parent
					};

					Expect(Colon);

					var ssdssl = new List<IStatement>();

					while (laKind != Case && laKind != Default && laKind != CloseCurlyBrace && !IsEOF)
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
				case Continue:
					Step();
					var cs = new ContinueStatement() { Location = t.Location, Parent = Parent };
					if (laKind == (Identifier))
					{
						Step();
						cs.Identifier = t.Value;
					}
					else if(IsEOF)
						cs.IdentifierHash = DTokens.IncompleteIdHash;

					Expect(Semicolon);
					cs.EndLocation = t.EndLocation;

					return cs;
				case Break:
					Step();
					var bs = new BreakStatement() { Location = t.Location, Parent = Parent };

					if (laKind == (Identifier))
					{
						Step();
						bs.Identifier = t.Value;
					}
					else if(IsEOF)
						bs.IdentifierHash = DTokens.IncompleteIdHash;

					Expect(Semicolon);

					bs.EndLocation = t.EndLocation;

					return bs;
				case Return:
					Step();
					var rs = new ReturnStatement() { Location = t.Location, Parent = Parent };

					if (laKind != (Semicolon))
						rs.ReturnExpression = Expression(Scope);

					Expect(Semicolon);
					rs.EndLocation = t.EndLocation;

					return rs;
				case Goto:
					Step();
					var gs = new GotoStatement() { Location = t.Location, Parent = Parent };

					switch(laKind)
					{
						case Identifier:
							Step();
							gs.StmtType = GotoStatement.GotoStmtType.Identifier;
							gs.LabelIdentifier = t.Value;
							break;
						case Default:
							Step();
							gs.StmtType = GotoStatement.GotoStmtType.Default;
							break;
						case Case:
							Step();
							gs.StmtType = GotoStatement.GotoStmtType.Case;

							if (laKind != (Semicolon))
								gs.CaseExpression = Expression(Scope);
							break;
						default:
							if (IsEOF)
								gs.LabelIdentifierHash = DTokens.IncompleteIdHash;
							break;
					}
					Expect(Semicolon);
					gs.EndLocation = t.EndLocation;

					return gs;
				case With:
					Step();

					var wS = new WithStatement() { Location = t.Location, Parent = Parent };

					if(Expect(OpenParenthesis))
					{
						// Symbol
						wS.WithExpression = Expression(Scope);

						Expect(CloseParenthesis);

						if(!IsEOF)
							wS.ScopedStatement = Statement(Scope: Scope, Parent: wS);
					}
					wS.EndLocation = t.EndLocation;
					return wS;
				case Synchronized:
					Step();
					var syncS = new SynchronizedStatement() { Location = t.Location, Parent = Parent };

					if (laKind == (OpenParenthesis))
					{
						Step();
						syncS.SyncExpression = Expression(Scope);
						Expect(CloseParenthesis);
					}

					if(!IsEOF)
						syncS.ScopedStatement = Statement(Scope: Scope, Parent: syncS);
					syncS.EndLocation = t.EndLocation;

					return syncS;
				case Try:
					Step();

					var ts = new TryStatement() { Location = t.Location, Parent = Parent };

					ts.ScopedStatement = Statement(Scope: Scope, Parent: ts);

					if (!(laKind == (Catch) || laKind == (Finally)))
						SemErr(Catch, "At least one catch or a finally block expected!");

					var catches = new List<TryStatement.CatchStatement>();
					// Catches
					while (laKind == (Catch))
					{
						Step();

						var c = new TryStatement.CatchStatement() { Location = t.Location, Parent = ts };

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

								Lexer.PushLookAheadBackup();
								catchVar.Type = BasicType(Scope);
								if (laKind == CloseParenthesis)
								{
									Lexer.RestoreLookAheadBackup();
									catchVar.Type = new IdentifierDeclaration("Exception") { InnerDeclaration = new IdentifierDeclaration("object") };
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
									catchVar.NameHash = DTokens.IncompleteIdHash;

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
						ts.Catches = catches.ToArray();

					if (laKind == (Finally))
					{
						Step();

						var f = new TryStatement.FinallyStatement() { Location = t.Location, Parent = Parent };

						f.ScopedStatement = Statement();
						f.EndLocation = t.EndLocation;

						ts.FinallyStmt = f;
					}

					ts.EndLocation = t.EndLocation;
					return ts;
				case Throw:
					Step();
					var ths = new ThrowStatement() { Location = t.Location, Parent = Parent };

					ths.ThrowExpression = Expression(Scope);
					Expect(Semicolon);
					ths.EndLocation = t.EndLocation;

					return ths;
				case DTokens.Scope:
					Step();

					if (laKind == OpenParenthesis)
					{
						var s = new ScopeGuardStatement() {
							Location = t.Location,
							Parent = Parent
						};

						Step();

						if (Expect(Identifier) && t.Value != null) // exit, failure, success
							s.GuardedScope = t.Value.ToLower();
						else if (IsEOF)
							s.GuardedScope = DTokens.IncompleteId;

						Expect(CloseParenthesis);

						s.ScopedStatement = Statement(Scope: Scope, Parent: s);

						s.EndLocation = t.EndLocation;
						return s;
					}
					else
						PushAttribute(new Modifier(DTokens.Scope), false);
					goto default;
				case Asm:
					return ParseAsmStatement(Scope, Parent);
				case Pragma:
					var ps = new PragmaStatement { Location = la.Location };

					ps.Pragma = _Pragma();
					ps.Parent = Parent;

					ps.ScopedStatement = Statement(Scope: Scope, Parent: ps);
					ps.EndLocation = t.EndLocation;
					return ps;
				case Mixin:
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
				case Assert:
					CheckForStorageClasses(Scope);
					if (Modifier.ContainsAttribute(DeclarationAttributes, Static))
                    {
                        Step();
						return ParseStaticAssertStatement (Scope);
                    }
                    else
                        return ExpressionStatement(Scope, Parent);
				case Volatile:
					Step();
					var vs = new VolatileStatement() { Location = t.Location, Parent = Parent };

					vs.ScopedStatement = Statement(Scope: Scope, Parent: vs);
					vs.EndLocation = t.EndLocation;

					return vs;
				case Import:
					if(laKind == Static)
						Step(); // Will be handled in ImportDeclaration

					return ImportDeclaration(Scope);
				case Enum:
				case Alias:
				case Typedef:
					var ds = new DeclarationStatement() { Location = la.Location, Parent = Parent, ParentNode = Scope };
					ds.Declarations = Declaration(Scope).ToArray();

					if (ds.Declarations != null && 
						ds.Declarations.Length == 1 && 
						!(ds.Declarations[0] is DVariable) &&
						!AllowWeakTypeParsing)
						Scope.Add(ds.Declarations[0]);

					ds.EndLocation = t.EndLocation;
					return ds;
				default:
					if (IsClassLike(laKind) || (IsBasicType(laKind) && Lexer.CurrentPeekToken.Kind != Dot) || IsModifier(laKind))
						goto case Typedef;
					if (IsAssignExpression())
						return ExpressionStatement(Scope, Parent);
					goto case Typedef;

			}
		}

		private IStatement ExpressionStatement(IBlockNode Scope, IStatement Parent)
		{
			var s = new ExpressionStatement() { Location = la.Location, Parent = Parent, ParentNode = Scope };

			// a==b, a=9; is possible -> Expressions can be there, not only single AssignExpressions!
			s.Expression = Expression(Scope);
			s.EndLocation = t.EndLocation;

			Expect (Semicolon);
			if (s.Expression != null)
				return s;
			return null;
		}
		
		ForStatement ForStatement(IBlockNode Scope, IStatement Parent)
		{
			Step();

			var dbs = new ForStatement { Location = t.Location, Parent = Parent };

			if(!Expect(OpenParenthesis))
				return dbs;

			// Initialize
			if (laKind == Semicolon)
				Step();
			else
				dbs.Initialize = Statement(false, Scope: Scope, Parent: dbs); // Against the spec, blocks aren't allowed here!

			// Test
			if (laKind != Semicolon)
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

				CheckForStorageClasses(Scope);
				ApplyAttributes(forEachVar);
				
				if(IsEOF){
					SynErr (Identifier, "Element variable name or type expected");
					forEachVar.NameHash = DTokens.IncompleteIdHash;
				}
				else if (laKind == (Identifier) && (Lexer.CurrentPeekToken.Kind == (Semicolon) || Lexer.CurrentPeekToken.Kind == Comma))
				{
					Step();
					forEachVar.NameLocation = t.Location;
					forEachVar.Name = t.Value;
				}
				else
				{
					var type = BasicType(Scope);
					
					var tnode = Declarator(type, false, Scope);
					if (!(tnode is DVariable))
						break;
					if(forEachVar.Attributes != null)
						if(tnode.Attributes == null)
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

		#region Asm Statement

		AsmStatement ParseAsmStatement(IBlockNode Scope, IStatement Parent)
		{
			Step();
			AsmStatement.AlignStatement als;
			var s = new AsmStatement() { Location = t.Location, Parent = Parent };

			CheckForStorageClasses (Scope); // allowed since dmd 2.067
			ApplyAttributes (new DVariable ());

			Expect(OpenCurlyBrace);

			var l = new List<AbstractStatement>();
			while (!IsEOF && laKind != (CloseCurlyBrace))
			{
				bool retrying = false;
			Retry:
				bool noStatement = false;
				switch(laKind)
				{
					case Align:
					als = new AsmStatement.AlignStatement() { Location = la.Location, Parent = s };
					Step();
					als.ValueExpression = Expression(Scope);
					l.Add(als);
					Step();
					break;
					case DTokens.Identifier:
					var opCode = AsmStatement.InstructionStatement.OpCode.__UNKNOWN__;
					var dataType = AsmStatement.RawDataStatement.DataType.__UNKNOWN__;
					if (Peek(1).Kind == Colon)
					{
						l.Add(new LabeledStatement() { Location = la.Location, Parent = s, Identifier = la.Value, EndLocation = Peek(1).EndLocation });
						Step();
						Step();
						if (laKind == Semicolon)
							Step();
						continue;
					}

					if (AsmStatement.RawDataStatement.TryParseDataType(la.Value, out dataType))
						l.Add(new AsmStatement.RawDataStatement() { Location = la.Location, Parent = s, TypeOfData = dataType });
					else if (AsmStatement.InstructionStatement.TryParseOpCode(la.Value, out opCode))
						l.Add(new AsmStatement.InstructionStatement() { Location = la.Location, Parent = s, Operation = opCode });
					else switch (la.Value.ToLower())
					{
						case "pause":
							SynErr(Identifier, "Pause is not supported by dmd's assembler. Use `rep; nop;` instead to achieve the same effect.");
							break;
						case "even":
							als = new AsmStatement.AlignStatement() { Location = la.Location, Parent = s };
							als.ValueExpression = new IdentifierExpression(2) { Location = la.Location, EndLocation = la.EndLocation };
							l.Add(als);
							break;
						case "naked":
							noStatement = true;
							break;
						default:
							SynErr(Identifier, "Unknown op-code!");
							l.Add(new AsmStatement.InstructionStatement() { Location = la.Location, Parent = s, Operation = AsmStatement.InstructionStatement.OpCode.__UNKNOWN__ });
							break;
					}
					Step();
					
					if (noStatement && laKind != Semicolon)
						SynErr(Semicolon);
					var parentStatement = noStatement ? s : l[l.Count - 1];
					var args = new List<IExpression>();
					if (IsEOF)
						args.Add(new TokenExpression(Incomplete));
					else if (laKind != Semicolon)
					{
						while (true)
						{
							if (laKind == CloseCurlyBrace)
							{
								// This is required as a custom error message because
								// it would complain about finding an identifier instead.
								SynErr(Semicolon, "; expected, } found");
								break;
							}
							var e = ParseAsmExpression(Scope, parentStatement);
							if (e != null)
								args.Add(e);
							if (laKind == Comma)
							{
								Step();
								continue;
							}
							if (IsEOF)
								args.Add(new TokenExpression(Incomplete));
							if (!Expect(Semicolon))
							{
								while (laKind != Semicolon && laKind != CloseCurlyBrace && !IsEOF)
									Step();
								if (laKind == Semicolon)
									Step();
							}

							break;
						}
					}
					else
						Step();
					if (parentStatement is AsmStatement.InstructionStatement)
						((AsmStatement.InstructionStatement)parentStatement).Arguments = args.ToArray();
					else if (parentStatement is AsmStatement.RawDataStatement)
						((AsmStatement.RawDataStatement)parentStatement).Data = args.ToArray();
					break;
					case DTokens.Semicolon:
						Step();
						break;
					case DTokens.Literal:
						l.Add(new AsmStatement.RawDataStatement { 
							Location = la.Location, 
							Data = new[] { ParseAsmPrimaryExpression(Scope, Parent) },
							EndLocation = t.EndLocation, 
							Parent = Parent
						});

						Expect(DTokens.Semicolon);
						break;
					default:
					string val;
					if (!retrying && Keywords.TryGetValue(laKind, out val))
					{
						la.LiteralValue = val;
						la.Kind = Identifier;
						Lexer.laKind = Identifier;
						retrying = true;
						goto Retry;
					}
					else
					{
						noStatement = true;
						SynErr(Identifier);
						Step();
					}
					break;
				}

				if (!noStatement)
					l[l.Count - 1].EndLocation = t.Location;
			}

			if (!Expect(CloseCurlyBrace) && (t.Kind == OpenCurlyBrace || t.Kind == Semicolon) && IsEOF)
				l.Add(new AsmStatement.InstructionStatement() { Operation = AsmStatement.InstructionStatement.OpCode.__UNKNOWN__ });

			s.EndLocation = t.EndLocation;
			s.Instructions = l.ToArray();
			return s;
		}

		IExpression ParseAsmExpression(IBlockNode Scope, IStatement Parent)
		{
			var left = ParseAsmLogOrExpression(Scope, Parent);
			while (laKind == Question)
			{
				Step();
				var e = new ConditionalExpression();
				e.TrueCaseExpression = ParseAsmExpression(Scope, Parent);
				Expect(Colon);
				e.FalseCaseExpression = ParseAsmExpression(Scope, Parent);
				left = e;
			}
			return left;
		}

		IExpression ParseAsmLogOrExpression(IBlockNode Scope, IStatement Parent)
		{
			var left = ParseAsmLogAndExpression(Scope, Parent);
			while (laKind == LogicalOr)
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
			while (laKind == LogicalAnd)
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
			while (laKind == BitwiseOr)
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
			while (laKind == Xor)
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
			while (laKind == BitwiseAnd)
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
			while (laKind == Equal || laKind == NotEqual)
			{
				Step();
				var e = new EqualExpression(t.Kind == NotEqual);
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
					case LessThan:
					case LessEqual:
					case GreaterThan:
					case GreaterEqual:
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
			while (laKind == ShiftRight || laKind == ShiftRightUnsigned || laKind == ShiftLeft)
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
			while (laKind == Plus || laKind == Minus)
			{
				Step();
				var e = new AddExpression(t.Kind == Minus);
				e.LeftOperand = left;
				e.RightOperand = ParseAsmMulExpression(Scope, Parent);
				left = e;
			}
			return left;
		}

		IExpression ParseAsmMulExpression(IBlockNode Scope, IStatement Parent)
		{
			IExpression left = ParseAsmBracketExpression(Scope, Parent);
			while (laKind == Times || laKind == Div || laKind == Mod)
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
			while (laKind == OpenSquareBracket)
			{
				Step();
				left = new PostfixExpression_ArrayAccess(ParseAsmExpression(Scope, Parent)) { PostfixForeExpression = left };
				Expect(CloseSquareBracket);
				(left as PostfixExpression_ArrayAccess).EndLocation = t.EndLocation;
			}
			return left;
		}

		IExpression ParseAsmUnaryExpression(IBlockNode Scope, IStatement Parent)
		{
			switch (laKind)
			{
				case Byte:
					la.LiteralValue = "byte";
					goto case Identifier;
				case Short:
					la.LiteralValue = "short";
					goto case Identifier;
				case Int:
					la.LiteralValue = "int";
					goto case Identifier;
				case Float:
					la.LiteralValue = "float";
					goto case Identifier;
				case Double:
					la.LiteralValue = "double";
					goto case Identifier;
				case Real:
					la.LiteralValue = "real";
					goto case Identifier;

				case Identifier:
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
							if (laKind == Identifier && la.Value == "ptr")
									Step();
							else if (t.Value != "short")
								SynErr(Identifier, "Expected ptr!");
							else if (!(Parent is AsmStatement.InstructionStatement) || !((AsmStatement.InstructionStatement)Parent).IsJmpFamily)
								SynErr(Identifier, "A short reference is only valid for the jmp family of instructions!");
							return ParseAsmExpression(Scope, Parent);

						default:
							return ParseAsmPrimaryExpression(Scope, Parent);
					}
				case Plus:
					Step();
					return new UnaryExpression_Add() { UnaryExpression = ParseAsmUnaryExpression(Scope, Parent) };
				case Minus:
					Step();
					return new UnaryExpression_Sub() { UnaryExpression = ParseAsmUnaryExpression(Scope, Parent) };
				case Not:
					Step();
					return new UnaryExpression_Not() { UnaryExpression = ParseAsmUnaryExpression(Scope, Parent) };
				case Tilde:
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
				case OpenSquareBracket:
					Step ();
					var e = new PostfixExpression_ArrayAccess (ParseAsmExpression (Scope, Parent));
					Expect (CloseSquareBracket);
					e.EndLocation = t.EndLocation;
					return e;
				case Dollar:
					var ins = Parent as AsmStatement.InstructionStatement;
					if (ins == null || (!ins.IsJmpFamily && ins.Operation != AsmStatement.InstructionStatement.OpCode.call))
						SynErr(Dollar, "The $ operator is only valid on jmp and call instructions!");
					Step();
					return new TokenExpression(t.Kind) { Location = t.Location, EndLocation = t.EndLocation };
				case Literal:
					Step();
					return new IdentifierExpression(t.LiteralValue, t.LiteralFormat, t.Subformat) { Location = t.Location, EndLocation = t.EndLocation };
				case This:
					Step();
					return new TokenExpression(This) { Location = t.Location, EndLocation = t.EndLocation };

				// AsmTypePrefix
				case DTokens.Byte:
				case DTokens.Short:
				case DTokens.Int:
				case DTokens.Float:
				case DTokens.Double:
				case DTokens.Real:

				case __LOCAL_SIZE:
					Step ();
					return new TokenExpression(t.Kind)  { Location = t.Location, EndLocation = t.EndLocation };
				case Identifier:
					Step();
					if (AsmRegisterExpression.IsRegister(t.Value))
					{
						string reg = t.Value;
						if (reg == "ST" && laKind == OpenParenthesis)
						{
							reg += "(";
							Step();
							if (Expect(Literal))
							{
								reg += t.LiteralValue.ToString();
								if (laKind != CloseParenthesis)
									SynErr(CloseParenthesis);
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
								if (laKind == Colon)
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
								SynErr(Identifier, "Unknown register!");
								return IsEOF ? new TokenExpression(Incomplete) : null;
						}
					}
					else
					{
						IExpression outer = new IdentifierExpression(t.Value) { Location = t.Location, EndLocation = t.EndLocation };
						while (laKind == Dot)
						{
							Step();
							if (Expect(Identifier))
								outer = new PostfixExpression_Access() { AccessExpression = new IdentifierExpression(t.Value), PostfixForeExpression = outer };
							else
								outer = new TokenExpression(Incomplete);
							Step();
						}
						return outer;
					}
				default:
					SynErr(Identifier, "Expected a $, literal or an identifier!");
					Step();
					if (IsEOF)
						return new TokenExpression(Incomplete);
					return null;
			}
		}

		#endregion

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

			if (laKind == DTokens.Semicolon)
				Step ();

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

						if(s != null)
							bs.Add(s);
					}
				}

				if (!Expect(CloseCurlyBrace) && IsEOF)
				{
					bs.EndLocation = la.Location;
					return bs;
				}
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
			ApplyAttributes(ret);

			// Allow anonymous structs&unions
			if (laKind == Identifier)
			{
				Expect(Identifier);
				ret.Name = t.Value;
				ret.NameLocation = t.Location;
			}
			else if (IsEOF)
				ret.NameHash = DTokens.IncompleteIdHash;

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

			ApplyAttributes(dc);

			if (Expect(Identifier))
			{
				dc.Name = t.Value;
				dc.NameLocation = t.Location;
			}
			else if (IsEOF)
				dc.NameHash = DTokens.IncompleteIdHash;

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

			var ret = dc.BaseClasses ?? (dc.BaseClasses = new List<ITypeDeclaration>());

			do
			{
				if (IsProtectionAttribute() && laKind != (Protected)) //TODO
					Step();

				var ids = Type(dc);
				if (ids != null)
					ret.Add(ids);
			}
			while (laKind == DTokens.Comma && Expect(DTokens.Comma) && laKind != DTokens.OpenCurlyBrace);
		}

		public void ClassBody(DBlockNode ret,bool KeepBlockAttributes=false,bool UpdateBoundaries=true)
		{
			var OldPreviousCommentString = PreviousComment;
			PreviousComment = new StringBuilder ();

			if (laKind == OpenCurlyBrace)
			{
				Step();
				var stk_backup = BlockAttributes;

				if (!KeepBlockAttributes)
					BlockAttributes = new Stack<DAttribute>();

				if (UpdateBoundaries)
					ret.BlockStartLocation = t.Location;

				while (!IsEOF && laKind != (CloseCurlyBrace))
					DeclDef(ret);

				Expect(CloseCurlyBrace);

				if (UpdateBoundaries)
					ret.EndLocation = t.EndLocation;

				BlockAttributes = stk_backup;
			}
			else
				Expect(Semicolon);

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
			ApplyAttributes (dm);
			dm.Description = GetComments();

			if (IsTemplateParameterList())
				TemplateParameterList(dm);

			// http://dlang.org/struct.html#StructPostblit
			if (IsStruct && laKind == (OpenParenthesis) && Peek(1).Kind == (This))
			{
				var dv = new DVariable { Parent = dm, Name = "this" };
				dm.Parameters.Add(dv);
				Step();
				Step();
				Expect(CloseParenthesis);
			}
			else
			{
				Parameters(dm);
			}

			// handle post argument attributes
			FunctionAttributes(dm);

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
			ApplyAttributes (dm);
			
			dm.SpecialType = DMethod.MethodType.Destructor;
			dm.Name = "~this";

			if (IsTemplateParameterList())
				TemplateParameterList(dm);

			Parameters(dm);

			// handle post argument attributes
			FunctionAttributes(dm);

			if (laKind == If)
				Constraint(dm);

			// handle post argument attributes
			FunctionAttributes(dm);

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

			ApplyAttributes(dc);

			if (Expect (Identifier)) {
				dc.Name = t.Value;
				dc.NameLocation = t.Location;
			}
			else if(IsEOF)
				dc.NameHash = DTokens.IncompleteIdHash;

			if (laKind == (OpenParenthesis))
				TemplateParameterList(dc);

			if (laKind == (If))
				Constraint(dc);

			if (laKind == (Colon))
				BaseClassList(dc);

			if (laKind == (If))
				Constraint(dc);

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
		private DEnum EnumDeclaration(IBlockNode Parent)
		{
			var mye = new DEnum() { Location = t.Location, Description = GetComments(), Parent=Parent };

			ApplyAttributes(mye);

			if (laKind == (Identifier))
			{
				Step ();
				mye.Name = t.Value;
				mye.NameLocation = t.Location;
			}
			else if (IsEOF)
				mye.NameHash = DTokens.IncompleteIdHash;

			// Enum inhertance type
			if (laKind == (Colon))
			{
				Step();
				mye.Type = Type(Parent as IBlockNode);
			}

			if (laKind == OpenCurlyBrace)
				EnumBody(mye);
			else 
				Expect(Semicolon);

			mye.Description += CheckForPostSemicolonComment();
			return mye;			
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

				EnumValue(mye);
			}
			while (laKind == Comma);

			Expect(CloseCurlyBrace);
			PreviousComment = OldPreviousComment;

			mye.EndLocation = t.EndLocation;
		}

		public void EnumValue(DEnum mye)
		{
			var ev = new DEnumValue() { Location = la.Location, Description = GetComments(), Parent = mye };

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
				ev.Type = Type(mye);
				if (Expect(Identifier))
				{
					ev.Name = t.Value;
					ev.NameLocation = t.Location;
				}
				else if (IsEOF)
					ev.NameHash = DTokens.IncompleteIdHash;
			}

			if (laKind == (Assign))
			{
				Step();
				ev.Initializer = AssignExpression(mye);
			}

			ev.EndLocation = t.EndLocation;
			ev.Description += CheckForPostSemicolonComment();

			mye.Add(ev);
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

			if (laKind == GoesTo)
			{
				LambdaBody(par);
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
			par.EndLocation = IsEOF && t.Kind != DTokens.CloseCurlyBrace ? la.Location : par.Body != null ? par.Body.EndLocation : t.EndLocation;
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

			ApplyAttributes(dc);

			if (mixinMod != null)
				dc.Attributes.Add(mixinMod);

			if (Expect(Identifier))
			{
				dc.Name = t.Value;
				dc.NameLocation = t.Location;
			}
			else if (IsEOF)
				dc.NameHash = DTokens.IncompleteIdHash;

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
					preQualifier=TypeOf(Scope as IBlockNode);
				}
				else if (laKind == __vector)
				{
					//TODO: Parse vectors(?)
				}

				Expect(Dot);
			}

			r.Qualifier= IdentifierList(Scope as IBlockNode);
			if (r.Qualifier != null)
				r.Qualifier.InnerMost.InnerDeclaration = preQualifier;
			else
				r.Qualifier = preQualifier;
			
			if(modScope)
			{
				var innerMost = r.Qualifier.InnerMost;
				if(innerMost is IntermediateIdType)	
					(innerMost as IntermediateIdType).ModuleScoped = true;
			}

			// MixinIdentifier
			if (laKind == Identifier) {
				Step ();
				r.IdLocation = t.Location;
				r.MixinId = t.Value;
			} else if (r.Qualifier != null && IsEOF)
				r.MixinId = DTokens.IncompleteId;

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
			IBlockNode scope = parent as IBlockNode;
			CodeLocation startLoc;

			// TemplateThisParameter
			if (laKind == (This))
			{
				Step();

				startLoc = t.Location;
				var end = t.EndLocation;

				return new TemplateThisParameter(TemplateParameter(parent), parent) { Location=startLoc, EndLocation=end };
			}

			// TemplateTupleParameter
			else if (laKind == (Identifier) && Lexer.CurrentPeekToken.Kind == TripleDot)
			{
				Step();
				startLoc = t.Location;
				var id = t.Value;
				Step();

				return new TemplateTupleParameter(id, startLoc, parent) { Location=startLoc, EndLocation=t.EndLocation	};
			}

			// TemplateAliasParameter
			else if (laKind == (Alias))
			{
				Step();

				startLoc = t.Location;
				TemplateAliasParameter al;
				ITypeDeclaration bt;

				if(IsEOF)
					al = new TemplateAliasParameter(DTokens.IncompleteIdHash, CodeLocation.Empty, parent);
				else
				{
					bt = BasicType (scope);
					ParseBasicType2 (ref bt, scope);

					if (laKind == Identifier) {
						// alias BasicType Declarator TemplateAliasParameterSpecialization_opt TemplateAliasParameterDefault_opt
						var nn = Declarator (bt, false, parent);
						al = new TemplateAliasParameter (nn.NameHash, nn.NameLocation, parent);
						al.Type = nn.Type;
						//TODO: Assign other parts of the declarator? Parameters and such?
					} else if (bt is IdentifierDeclaration)
						al = new TemplateAliasParameter ((bt as IdentifierDeclaration).IdHash, bt.Location, parent);
					else
						al = new TemplateAliasParameter (0, CodeLocation.Empty, parent);
				}
				al.Location = startLoc;

				// TemplateAliasParameterSpecialization
				if (laKind == (Colon))
				{
					Step();

					AllowWeakTypeParsing=true;
					al.SpecializationType = Type(scope);
					AllowWeakTypeParsing=false;

					if (al.SpecializationType==null)
						al.SpecializationExpression = ConditionalExpression(scope);
				}

				// TemplateAliasParameterDefault
				if (laKind == (Assign))
				{
					Step();

					if (IsAssignExpression ())
						al.DefaultExpression = ConditionalExpression (scope);
					else
						al.DefaultType = Type (scope);
				}
				al.EndLocation = t.EndLocation;
				return al;
			}

			// TemplateTypeParameter
			else if (laKind == (Identifier) && (Lexer.CurrentPeekToken.Kind == (Colon) || Lexer.CurrentPeekToken.Kind == (Assign) || Lexer.CurrentPeekToken.Kind == (Comma) || Lexer.CurrentPeekToken.Kind == (CloseParenthesis)))
			{
				Expect(Identifier);
				var tt = new TemplateTypeParameter(t.Value, t.Location, parent) { Location = t.Location };

				if (laKind == Colon)
				{
					Step();
					tt.Specialization = Type(scope);
				}

				if (laKind == Assign)
				{
					Step();
					tt.Default = Type(scope);
				}
				tt.EndLocation = t.EndLocation;
				return tt;
			}

			// TemplateValueParameter
			startLoc = la.Location;
			var dv = Declarator(BasicType(scope), false, null);

			if (dv == null) {
				SynErr (t.Kind, "Declarator expected for parsing template parameter");
				return new TemplateTypeParameter (DTokens.IncompleteIdHash, t.Location, parent) { Location = t.Location };
			}

			var tv = new TemplateValueParameter(dv.NameHash, dv.NameLocation, parent) { 
				Location=startLoc,
				Type = dv.Type
			};

			if (laKind == (Colon))
			{
				Step();
				tv.SpecializationExpression = ConditionalExpression(scope);
			}

			if (laKind == (Assign))
			{
				Step();
				tv.DefaultExpression = AssignExpression(scope);
			}
			tv.EndLocation = t.EndLocation;
			return tv;
		}

		bool IsTemplateInstance
		{
			get {
				Lexer.StartPeek ();
				if (laKind != Identifier && (!DTokens.IsStorageClass(laKind) || Peek ().Kind != Identifier))
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

			if (DTokens.IsStorageClass(laKind)) {
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
						
						Lexer.PushLookAheadBackup();

						bool wp = AllowWeakTypeParsing;
						AllowWeakTypeParsing = true;

						var typeArg = Type(Scope);

						AllowWeakTypeParsing = wp;

						if (typeArg != null && (laKind == CloseParenthesis || laKind==Comma)){
							Lexer.PopLookAheadBackup();
							args.Add(new TypeDeclarationExpression(typeArg));
						}else
						{
							Lexer.RestoreLookAheadBackup();
							var ex = AssignExpression(Scope);
							if(ex != null)
								args.Add(ex);
						}
					}
				}
				Expect(CloseParenthesis);
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
					case Literal:
					case True:
					case False:
					case Null:
					case This:
					case __FILE__:
					case __MODULE__:
					case __LINE__:
					case __FUNCTION__:
					case __PRETTY_FUNCTION__:
						args.Add(PrimaryExpression(Scope));
						break;
					case Identifier:
						Step();
						args.Add(new IdentifierExpression(t.Value) {
							Location = t.Location,
							EndLocation = t.EndLocation
						});
						break;
					default:
						if (IsBasicType(laKind))
						{
							Step ();
							args.Add (new TypeDeclarationExpression (new DTokenDeclaration (t.Kind) {
								Location = t.Location,
								EndLocation = t.EndLocation
							}));
							break;
						}
						else if (IsEOF)
							goto case Literal;
						SynErr(laKind, "Illegal token found on template instance expression argument");
						Step();
						break;
				}

				if (laKind == Not && Peek(1).Kind != Is && Peek(1).Kind != In)
				{
					SynErr(laKind, "multiple ! arguments are not allowed");
					Step();
				}
			}
			(td as TemplateInstanceExpression).Arguments = args.ToArray();
			td.EndLocation = t.EndLocation;
			return td as TemplateInstanceExpression;
		}
		#endregion

		#region Traits
		IExpression TraitsExpression(IBlockNode scope)
		{
			Expect(__traits);
			var ce = new TraitsExpression() { Location=t.Location};
			if(Expect(OpenParenthesis))
			{
				if (Expect (Identifier))
					ce.Keyword = t.Value;
				else if (IsEOF)
					ce.Keyword = DTokens.IncompleteId;

				var al = new List<TraitsArgument>();

				var weakTypeParsingBackup = AllowWeakTypeParsing;

				while (laKind == Comma)
				{
					Step();

					Lexer.PushLookAheadBackup ();

					AllowWeakTypeParsing = true;
					var td = Type (scope);
					AllowWeakTypeParsing = false;

					if (td != null && (laKind == Comma || laKind == CloseParenthesis || IsEOF)) {
						Lexer.PopLookAheadBackup ();
						al.Add (new TraitsArgument(td));
						continue;
					}

					Lexer.RestoreLookAheadBackup ();

					al.Add(new TraitsArgument(AssignExpression(scope)));
				}

				AllowWeakTypeParsing = weakTypeParsingBackup;

				Expect (CloseParenthesis);
				
				if(al.Count != 0)
					ce.Arguments = al.ToArray();
			}
			ce.EndLocation = t.EndLocation;
			return ce;
		}
		#endregion
	}
}