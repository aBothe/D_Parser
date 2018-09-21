using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;

namespace D_Parser.Parser.Implementations
{
	class DAttributesParser : DParserImplementationPart
	{
		readonly DParserParts parserParts;

		public DAttributesParser(DParserStateContext stateContext, DParserParts parserParts)
			: base(stateContext)
		{
			this.parserParts = parserParts;
		}

		public DMethod _Invariant()
		{
			var inv = new DMethod { SpecialType = DMethod.MethodType.ClassInvariant };

			Expect(DTokens.Invariant);
			inv.Location = t.Location;
			if (laKind == DTokens.OpenParenthesis)
			{
				Step();
				Expect(DTokens.CloseParenthesis);
			}
			if (!IsEOF)
				inv.Body = parserParts.statementParser.BlockStatement(inv);
			inv.EndLocation = t.EndLocation;
			return inv;
		}

		public PragmaAttribute _Pragma()
		{
			Expect(DTokens.Pragma);
			var s = new PragmaAttribute { Location = t.Location };
			if (Expect(DTokens.OpenParenthesis))
			{
				if (Expect(DTokens.Identifier))
					s.Identifier = t.Value;

				var l = new List<IExpression>();
				while (laKind == DTokens.Comma)
				{
					Step();
					l.Add(parserParts.expressionsParser.AssignExpression());
				}
				if (l.Count > 0)
					s.Arguments = l.ToArray();
				Expect(DTokens.CloseParenthesis);
			}
			s.EndLocation = t.EndLocation;
			return s;
		}

		public bool IsAttributeSpecifier
		{
			get
			{
				switch (laKind)
				{
					case DTokens.Extern:
					case DTokens.Export:
					case DTokens.Align:
					case DTokens.Pragma:
					case DTokens.Deprecated:
					case DTokens.Final:
					case DTokens.Override:
					case DTokens.Abstract:
					case DTokens.Scope:
					case DTokens.__gshared:
					case DTokens.Synchronized:
					case DTokens.At:
						return true;
					case DTokens.Static:
						if (Lexer.CurrentPeekToken.Kind != DTokens.If && Lexer.CurrentPeekToken.Kind != DTokens.Foreach && Lexer.CurrentPeekToken.Kind != DTokens.Foreach_Reverse)
							return true;
						return false;
					case DTokens.Auto:
						if (Lexer.CurrentPeekToken.Kind != DTokens.OpenParenthesis && Lexer.CurrentPeekToken.Kind != DTokens.Identifier)
							return true;
						return false;
					default:
						if (DTokensSemanticHelpers.IsMemberFunctionAttribute(laKind))
							return Lexer.CurrentPeekToken.Kind != DTokens.OpenParenthesis;
						return IsProtectionAttribute();
				}
			}
		}

		public bool IsProtectionAttribute()
		{
			switch (laKind)
			{
				case DTokens.Public:
				case DTokens.Private:
				case DTokens.Protected:
				case DTokens.Extern:
				case DTokens.Package:
					return true;
				default:
					return false;
			}
		}

		public void AttributeSpecifier(IBlockNode scope)
		{
			DAttribute attr;
			Modifier m;

			switch (laKind)
			{
				case DTokens.At:
					attr = AtAttribute(scope);
					break;

				case DTokens.Pragma:
					attr = _Pragma();
					break;

				case DTokens.Deprecated:
					Step();
					var loc = t.Location;
					IExpression lc = null;
					if (laKind == DTokens.OpenParenthesis)
					{
						Step();
						lc = parserParts.expressionsParser.AssignExpression(scope);
						Expect(DTokens.CloseParenthesis);
					}
					attr = new DeprecatedAttribute(loc, t.EndLocation, lc);
					break;

				case DTokens.Extern:
					attr = m = new Modifier(laKind, la.Value) { Location = la.Location };
					Step();
					if (laKind == DTokens.OpenParenthesis)
					{
						Step(); // Skip (

						var sb = new StringBuilder();
						// Check if EOF and append IncompleteID
						while (!IsEOF && laKind != DTokens.CloseParenthesis)
						{
							Step();
							sb.Append(t.ToString());

							if (t.Kind == DTokens.Identifier && laKind == DTokens.Identifier)
								sb.Append(' ');
						}
						if (IsEOF)
							m.LiteralContent = DTokens.IncompleteId;
						else
							m.LiteralContent = sb.ToString();

						Expect(DTokens.CloseParenthesis);
					}

					m.EndLocation = t.EndLocation;
					break;

				case DTokens.Align:
					attr = m = new Modifier(laKind, la.Value) { Location = la.Location };
					Step();
					if (laKind == DTokens.OpenParenthesis)
					{
						Step();
						m.LiteralContent = parserParts.expressionsParser.AssignExpression(scope);

						if (!Expect(DTokens.CloseParenthesis))
							return;
					}

					m.EndLocation = t.EndLocation;
					break;

				case DTokens.Package:
					attr = m = new Modifier(laKind, la.Value) { Location = la.Location };
					Step();

					if (laKind == DTokens.OpenParenthesis)
					{
						// This isn't documented anywhere. http://dlang.org/attribute.html#ProtectionAttribute
						//TODO: Semantically handle this.
						Step();
						m.LiteralContent = parserParts.declarationParser.IdentifierList(scope); // Reassigns a symbol's package/'namespace' or so

						Expect(DTokens.CloseParenthesis);
					}

					m.EndLocation = t.EndLocation;
					break;

				default:
					attr = m = new Modifier(laKind, la.Value) { Location = la.Location };
					Step();
					m.EndLocation = t.EndLocation;
					break;
			}

			//TODO: What about these semicolons after e.g. a pragma? Enlist these attributes anyway in the meta decl list?
			if (laKind != DTokens.Semicolon)
			{
				if (scope is DBlockNode)
					AttributeSpecifier(scope as DBlockNode, attr);
				else
					parserParts.declarationParser.PushAttribute(attr, false);
			}
		}

		/// <summary>
		/// Parses an attribute that starts with an @. Might be user-defined or a built-in attribute.
		/// Due to the fact that
		/// </summary>
		AtAttribute AtAttribute(IBlockNode scope)
		{
			var sl = la.Location;
			Expect(DTokens.At);

			if (laKind == DTokens.Identifier)
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
					case "nogc":
						att = BuiltInAtAttribute.BuiltInAttributes.Nogc;
						break;
				}

				if (att != BuiltInAtAttribute.BuiltInAttributes.None)
				{
					Step();
					return new BuiltInAtAttribute(att) { Location = sl, EndLocation = t.EndLocation };
				}
			}
			else if (laKind == DTokens.OpenParenthesis)
			{
				Step();
				var args = parserParts.expressionsParser.ArgumentList(scope);
				Expect(DTokens.CloseParenthesis);
				return new UserDeclarationAttribute(args.ToArray()) { Location = sl, EndLocation = t.EndLocation };
			}

			var x = parserParts.expressionsParser.PostfixExpression(scope);
			return new UserDeclarationAttribute(x != null ? new[] { x } : null) { Location = sl, EndLocation = t.EndLocation };
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="module"></param>
		/// <param name="previouslyParsedAttribute"></param>
		/// <param name="RequireDeclDef">If no colon and no open curly brace is given as lookahead, a DeclDef may be parsed otherwise, if parameter is true.</param>
		/// <returns></returns>
		public IMetaDeclaration AttributeSpecifier(DBlockNode module, DAttribute previouslyParsedAttribute, bool RequireDeclDef = false)
		{
			DAttribute[] attrs;

			if (laKind == DTokens.Colon)
			{
				Step();
				parserParts.declarationParser.PushAttribute(previouslyParsedAttribute, true);

				attrs = new DAttribute[1 + DeclarationAttributes.Count];
				DeclarationAttributes.CopyTo(attrs, 0);
				DeclarationAttributes.Clear();
				attrs[attrs.Length - 1] = previouslyParsedAttribute;

				AttributeMetaDeclarationSection metaDecl = null;
				//TODO: Put all remaining block/decl(?) attributes into the section definition..
				if (module != null)
					module.Add(metaDecl = new AttributeMetaDeclarationSection(attrs) { EndLocation = t.EndLocation });
				return metaDecl;
			}
			else
				parserParts.declarationParser.PushAttribute(previouslyParsedAttribute, false);

			if (laKind == DTokens.OpenCurlyBrace)
				return parserParts.modulesParser.AttributeBlock(module);
			else
			{
				if (IsEOF && module != null && previouslyParsedAttribute != null) // To enable attribute completion, add dummy node
					module.Add(new DVariable { Attributes = new List<DAttribute> { previouslyParsedAttribute } });

				if (RequireDeclDef)
				{
					parserParts.modulesParser.DeclDef(module);

					attrs = new DAttribute[1 + DeclarationAttributes.Count];
					DeclarationAttributes.CopyTo(attrs, 0);
					DeclarationAttributes.Clear();
					attrs[attrs.Length - 1] = previouslyParsedAttribute;

					return new AttributeMetaDeclaration(attrs) { EndLocation = previouslyParsedAttribute.EndLocation };
				}
			}

			return null;
		}

		public bool IsFunctionAttribute
		{
			get { return DTokensSemanticHelpers.IsFunctionAttribute(laKind); }
		}


		public void FunctionAttributes(DNode n)
		{
			FunctionAttributes(ref n.Attributes);
		}

		public void FunctionAttributes(ref List<DAttribute> attributes)
		{
			DAttribute attr = null;
			attributes = attributes ?? new List<DAttribute>();
			while (IsFunctionAttribute)
			{
				if (laKind == DTokens.At)
					attr = AtAttribute(null);
				else
				{
					attributes.Add(attr = new Modifier(laKind, la.Value) { Location = la.Location, EndLocation = la.EndLocation });
					Step();
				}
			}
		}
	}
}
