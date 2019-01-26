using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Parser.Implementations
{
	class DBodiedSymbolsParser : DParserImplementationPart
	{
		readonly DParserParts parserParts;

		public DBodiedSymbolsParser(DParserStateContext stateContext, DParserParts parserParts)
			: base(stateContext) { this.parserParts = parserParts; }

		#region Structs & Unions
		public INode AggregateDeclaration(INode Parent)
		{
			var classType = laKind;
			if (!(classType == DTokens.Union || classType == DTokens.Struct))
				SynErr(t.Kind, "union or struct required");
			Step();

			var ret = new DClassLike(t.Kind)
			{
				Location = t.Location,
				Description = GetComments(),
				ClassType = classType,
				Parent = Parent
			};
			parserParts.declarationParser.ApplyAttributes(ret);

			// Allow anonymous structs&unions
			if (laKind == DTokens.Identifier)
			{
				Expect(DTokens.Identifier);
				ret.Name = t.Value;
				ret.NameLocation = t.Location;
			}
			else if (IsEOF)
				ret.NameHash = DTokens.IncompleteIdHash;

			if (laKind == (DTokens.Semicolon))
			{
				Step();
				return ret;
			}

			// StructTemplateDeclaration
			if (laKind == (DTokens.OpenParenthesis))
			{
				parserParts.templatesParser.TemplateParameterList(ret);

				// Constraint[opt]
				if (laKind == (DTokens.If))
					Constraint(ret);
			}

			ClassBody(ret);

			return ret;
		}
		#endregion

		#region Classes
		public INode ClassDeclaration(INode Parent)
		{
			Expect(DTokens.Class);

			var dc = new DClassLike(DTokens.Class)
			{
				Location = t.Location,
				Description = GetComments(),
				Parent = Parent
			};

			parserParts.declarationParser.ApplyAttributes(dc);

			if (Expect(DTokens.Identifier))
			{
				dc.Name = t.Value;
				dc.NameLocation = t.Location;
			}
			else if (IsEOF)
				dc.NameHash = DTokens.IncompleteIdHash;

			if (laKind == (DTokens.OpenParenthesis))
				parserParts.templatesParser.TemplateParameterList(dc);

			// Constraints
			// http://dlang.org/template.html#ClassTemplateDeclaration
			if (Constraint(dc))
			{ // Constraint_opt BaseClassList_opt
				if (laKind == (DTokens.Colon))
					BaseClassList(dc);
			}
			else if (laKind == (DTokens.Colon))
			{ // Constraint_opt BaseClassList_opt
				BaseClassList(dc);
				Constraint(dc);
			}

			ClassBody(dc);

			dc.EndLocation = t.EndLocation;
			return dc;
		}

		public bool Constraint(DNode dn)
		{
			if (laKind == DTokens.If)
			{
				Step();
				Expect(DTokens.OpenParenthesis);

				dn.TemplateConstraint = parserParts.expressionsParser.Expression();

				Expect(DTokens.CloseParenthesis);

				return true;
			}
			return false;
		}

		public void BaseClassList(DClassLike dc, bool ExpectColon = true)
		{
			if (ExpectColon) Expect(DTokens.Colon);

			var ret = dc.BaseClasses ?? (dc.BaseClasses = new List<ITypeDeclaration>());

			do
			{
				if (parserParts.attributesParser.IsProtectionAttribute() && laKind != (DTokens.Protected)) //TODO
					Step();

				var ids = parserParts.declarationParser.Type(dc);
				if (ids != null)
					ret.Add(ids);
			}
			while (laKind == DTokens.Comma && Expect(DTokens.Comma) && laKind != DTokens.OpenCurlyBrace);
		}

		public void ClassBody(DBlockNode ret, bool KeepBlockAttributes = false, bool UpdateBoundaries = true)
		{
			var OldPreviousCommentString = PreviousComment;
			PreviousComment = new StringBuilder();

			if (laKind == DTokens.OpenCurlyBrace)
			{
				Step();
				var stk_backup = BlockAttributes;

				if (!KeepBlockAttributes)
					BlockAttributes = new Stack<DAttribute>();

				if (UpdateBoundaries)
					ret.BlockStartLocation = t.Location;

				while (!IsEOF && laKind != (DTokens.CloseCurlyBrace))
					parserParts.modulesParser.DeclDef(ret);

				Expect(DTokens.CloseCurlyBrace);

				if (UpdateBoundaries)
					ret.EndLocation = t.EndLocation;

				BlockAttributes = stk_backup;
			}
			else
				Expect(DTokens.Semicolon);

			PreviousComment = OldPreviousCommentString;

			if (ret != null)
				ret.Description += CheckForPostSemicolonComment();
		}

		public INode Constructor(DBlockNode scope, bool IsStruct)
		{
			Expect(DTokens.This);
			var dm = new DMethod()
			{
				Parent = scope,
				SpecialType = DMethod.MethodType.Constructor,
				Location = t.Location,
				Name = DMethod.ConstructorIdentifier,
				NameLocation = t.Location
			};
			parserParts.declarationParser.ApplyAttributes(dm);
			dm.Description = GetComments();

			if (parserParts.templatesParser.IsTemplateParameterList())
				parserParts.templatesParser.TemplateParameterList(dm);

			// http://dlang.org/struct.html#StructPostblit
			if (IsStruct && laKind == (DTokens.OpenParenthesis) && Peek(1).Kind == (DTokens.This))
			{
				var dv = new DVariable { Parent = dm, Name = "this" };
				dm.Parameters.Add(dv);
				Step();
				Step();
				Expect(DTokens.CloseParenthesis);
			}
			else
			{
				parserParts.declarationParser.Parameters(dm);
			}

			// handle post argument attributes
			parserParts.attributesParser.FunctionAttributes(dm);

			if (laKind == DTokens.If)
				Constraint(dm);

			// handle post argument attributes
			parserParts.attributesParser.FunctionAttributes(dm);

			if (IsFunctionBody)
				FunctionBody(dm);
			return dm;
		}

		public INode Destructor()
		{
			Expect(DTokens.Tilde);
			var dm = new DMethod { Location = t.Location, NameLocation = la.Location };
			Expect(DTokens.This);
			parserParts.declarationParser.ApplyAttributes(dm);

			dm.SpecialType = DMethod.MethodType.Destructor;
			dm.Name = "~this";

			if (parserParts.templatesParser.IsTemplateParameterList())
				parserParts.templatesParser.TemplateParameterList(dm);

			parserParts.declarationParser.Parameters(dm);

			// handle post argument attributes
			parserParts.attributesParser.FunctionAttributes(dm);

			if (laKind == DTokens.If)
				Constraint(dm);

			// handle post argument attributes
			parserParts.attributesParser.FunctionAttributes(dm);

			FunctionBody(dm);
			return dm;
		}
		#endregion

		#region Interfaces
		public IBlockNode InterfaceDeclaration(INode Parent)
		{
			Expect(DTokens.Interface);
			var dc = new DClassLike()
			{
				Location = t.Location,
				Description = GetComments(),
				ClassType = DTokens.Interface,
				Parent = Parent
			};

			parserParts.declarationParser.ApplyAttributes(dc);

			if (Expect(DTokens.Identifier))
			{
				dc.Name = t.Value;
				dc.NameLocation = t.Location;
			}
			else if (IsEOF)
				dc.NameHash = DTokens.IncompleteIdHash;

			if (laKind == (DTokens.OpenParenthesis))
				parserParts.templatesParser.TemplateParameterList(dc);

			if (laKind == (DTokens.If))
				Constraint(dc);

			if (laKind == (DTokens.Colon))
				BaseClassList(dc);

			if (laKind == (DTokens.If))
				Constraint(dc);

			// Empty interfaces are allowed
			if (laKind == DTokens.Semicolon)
				Step();
			else
				ClassBody(dc);

			dc.EndLocation = t.EndLocation;
			return dc;
		}
		#endregion

		#region Enums
		public DEnum EnumDeclaration(IBlockNode Parent)
		{
			var mye = new DEnum() { Location = t.Location, Description = GetComments(), Parent = Parent };

			parserParts.declarationParser.ApplyAttributes(mye);

			if (laKind == (DTokens.Identifier))
			{
				Step();
				mye.Name = t.Value;
				mye.NameLocation = t.Location;
			}
			else if (IsEOF)
				mye.NameHash = DTokens.IncompleteIdHash;

			// Enum inhertance type
			if (laKind == (DTokens.Colon))
			{
				Step();
				mye.Type = parserParts.declarationParser.Type(Parent as IBlockNode);
			}

			if (laKind == DTokens.OpenCurlyBrace)
				EnumBody(mye);
			else
				Expect(DTokens.Semicolon);

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

				if (laKind == DTokens.CloseCurlyBrace)
					break;

				EnumValue(mye);
			}
			while (laKind == DTokens.Comma);

			Expect(DTokens.CloseCurlyBrace);
			PreviousComment = OldPreviousComment;

			mye.EndLocation = t.EndLocation;
		}

		public void EnumValue(DEnum mye)
		{
			var ev = new DEnumValue() { Location = la.Location, Description = GetComments(), Parent = mye };

			while (laKind == DTokens.Deprecated || laKind == DTokens.At)
			{
				parserParts.attributesParser.AttributeSpecifier(mye);
			}

			if (laKind == DTokens.Identifier && (
				Lexer.CurrentPeekToken.Kind == DTokens.Assign ||
				Lexer.CurrentPeekToken.Kind == DTokens.Comma ||
				Lexer.CurrentPeekToken.Kind == DTokens.CloseCurlyBrace))
			{
				Step();
				ev.Name = t.Value;
				ev.NameLocation = t.Location;
			}
			else
			{
				ev.Type = parserParts.declarationParser.Type(mye);
				if (Expect(DTokens.Identifier))
				{
					ev.Name = t.Value;
					ev.NameLocation = t.Location;
				}
				else if (IsEOF)
					ev.NameHash = DTokens.IncompleteIdHash;
			}

			if (laKind == (DTokens.Assign))
			{
				Step();
				ev.Initializer = parserParts.expressionsParser.AssignExpression(mye);
			}

			parserParts.declarationParser.ApplyAttributes(ev);

			ev.EndLocation = t.EndLocation;
			ev.Description += CheckForPostSemicolonComment();

			mye.Add(ev);
		}
		#endregion

		#region Functions
		public bool IsFunctionBody
		{
			get
			{
				switch (laKind)
				{
					case DTokens.In:
					case DTokens.Out:
					case DTokens.Body:
					case DTokens.Do:
					case DTokens.OpenCurlyBrace:
						return true;
					default:
						return false;
				}
			}
		}

		public void FunctionBody(DMethod par)
		{
			if (laKind == DTokens.Semicolon) // Abstract or virtual functions
			{
				Step();
				par.Description += CheckForPostSemicolonComment();
				par.EndLocation = t.EndLocation;
				return;
			}

			if (laKind == DTokens.GoesTo)
			{
				parserParts.expressionsParser.LambdaBody(par);
				return;
			}

			var stk_Backup = BlockAttributes;
			BlockAttributes = new Stack<DAttribute>();

			while (laKind == DTokens.In || laKind == DTokens.Out)
			{
				bool inOut = laKind == DTokens.Out;
				Step();
				var contractStmt = new ContractStatement() { isOut = inOut, Location = t.Location };
				if (!inOut)
				{
					if (laKind == DTokens.OpenParenthesis)
					{
						// in(expr)
						Step();
						contractStmt.Condition = parserParts.expressionsParser.Expression(par);
						if (laKind == DTokens.Comma)
						{
							Step();
							contractStmt.Message = parserParts.expressionsParser.Expression(par);
						}
						Expect(DTokens.CloseParenthesis);
					}
					else
					{
						// in { stmt; }
						contractStmt.ScopedStatement = parserParts.statementParser.BlockStatement(par);
					}
				}
				else
				{
					if (laKind == DTokens.OpenParenthesis)
					{
						Step();
						if (laKind == DTokens.Identifier)
						{
							// out(res
							Step();
							contractStmt.OutResultVariable = new IdentifierDeclaration(t.Value) { Location = t.Location, EndLocation = t.EndLocation };
						}
						if (laKind == DTokens.Semicolon)
						{
							// out(res; expr)
							Step();
							contractStmt.Condition = parserParts.expressionsParser.Expression(par);
							if (laKind == DTokens.Comma)
							{
								Step();
								contractStmt.Message = parserParts.expressionsParser.Expression(par);
							}
							Expect(DTokens.CloseParenthesis);
						}
						else
						{
							// out(res) { stmt; }
							Expect(DTokens.CloseParenthesis);
							contractStmt.ScopedStatement = parserParts.statementParser.BlockStatement(par);
						}
					}
					else
					{
						// out { stmt; }
						contractStmt.ScopedStatement = parserParts.statementParser.BlockStatement(par);
					}
				}
				par.Contracts.Add(contractStmt);
			}

			// Although there can be in&out constraints, there doesn't have to be a direct body definition. Used on abstract class/interface methods.
			if (laKind == DTokens.Body || laKind == DTokens.Do)
			{
				Step();
				par.BodyToken = t.Location;
			}

			if (par.Contracts.Count == 0 || laKind == DTokens.OpenCurlyBrace)
			{
				par.Body = parserParts.statementParser.BlockStatement(par);
			}

			BlockAttributes = stk_Backup;
			par.EndLocation = IsEOF && t.Kind != DTokens.CloseCurlyBrace ? la.Location : par.Body != null ? par.Body.EndLocation : t.EndLocation;
		}
		#endregion
	}
}
