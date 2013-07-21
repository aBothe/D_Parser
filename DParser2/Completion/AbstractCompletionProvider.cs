using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Completion.Providers;
using D_Parser.Resolver.ASTScanner;
using System;

namespace D_Parser.Completion
{
	public abstract class AbstractCompletionProvider
	{
		public readonly ICompletionDataGenerator CompletionDataGenerator;
		
		public AbstractCompletionProvider(ICompletionDataGenerator CompletionDataGenerator)
		{
			this.CompletionDataGenerator = CompletionDataGenerator;
		}

		public static AbstractCompletionProvider Create(ICompletionDataGenerator dataGen, IEditorData Editor, string EnteredText)
		{
			if (PropertyAttributeCompletionProvider.CompletesEnteredText(EnteredText))
				return new PropertyAttributeCompletionProvider(dataGen);

			ParserTrackerVariables trackVars=null;
			IStatement curStmt = null;
			var curBlock = DResolver.SearchBlockAt(Editor.SyntaxTree, Editor.CaretLocation, out curStmt);

			if (curBlock == null)
				return null;

			var parsedBlock = CtrlSpaceCompletionProvider.FindCurrentCaretContext(
				Editor.ModuleCode,
				curBlock,
				Editor.CaretOffset,
				Editor.CaretLocation,
				out trackVars);

			if (trackVars != null)
			{
				if(trackVars.ExpectingNodeName)
					return null;

				PostfixExpression_Access pfa;

				// if( asdf == E.| )
				var ex = trackVars.LastParsedObject as IExpression;
				while (ex is OperatorBasedExpression) {
					var opEx = ex as OperatorBasedExpression;
					var rop = opEx.RightOperand;
					if (rop != null && Editor.CaretLocation >= rop.Location)
						ex = rop;
					else if ((rop = opEx.LeftOperand) != null && Editor.CaretLocation <= rop.EndLocation)
						ex = rop;
					else
						break;
				}

				if (ex is PostfixExpression_Access)
					pfa = ex as PostfixExpression_Access;
				else if (trackVars.LastParsedObject is ITypeDeclaration && !(trackVars.LastParsedObject is TemplateInstanceExpression))
					pfa = TryConvertTypeDeclaration(trackVars.LastParsedObject as ITypeDeclaration) as PostfixExpression_Access;
				else
					pfa = null;

				if (pfa != null)
				{
					// myObj. <-- AccessExpression will be null there, 
					// this.fileName | <-- AccessExpression will be 'fileName' - no trigger wished
					if (pfa.AccessExpression == null)
					{
						var mcp = new MemberCompletionProvider(dataGen)
						{
							AccessExpression = pfa,
							ScopedBlock = curBlock,
							ScopedStatement = curStmt,
						};
						if (trackVars.IsParsingBaseClassList)
						{
							if (trackVars.InitializedNode is DClassLike && 
								(trackVars.InitializedNode as DClassLike).ClassType == DTokens.Interface)
								mcp.MemberFilter = MemberFilter.Interfaces | MemberFilter.Templates;
							else
								mcp.MemberFilter = MemberFilter.Classes | MemberFilter.Interfaces | MemberFilter.Templates;
						}
						return mcp;
					}
					else
						return null;
				}

				if(trackVars.ExpectingIdentifier)
				{
					if (trackVars.LastParsedObject is DAttribute)
						return new AttributeCompletionProvider (dataGen) {
							Attribute = trackVars.LastParsedObject as DAttribute
						};
					else if (trackVars.LastParsedObject is ScopeGuardStatement)
						return new ScopeAttributeCompletionProvider (dataGen) {
							//ScopeStmt = trackVars.LastParsedObject as ScopeGuardStatement
						};
					else if (trackVars.LastParsedObject is PragmaStatement)
						return new AttributeCompletionProvider (dataGen) {
							Attribute = (trackVars.LastParsedObject as PragmaStatement).Pragma
						};
					else if (trackVars.LastParsedObject is TraitsExpression)
						return new TraitsExpressionCompletionProvider (dataGen) {
							//TraitsExpr=trackVars.LastParsedObject as TraitsExpression 
						};
					else if (trackVars.LastParsedObject is ImportStatement.Import)
						return new ImportStatementCompletionProvider (dataGen, (ImportStatement.Import)trackVars.LastParsedObject);
					else if (trackVars.LastParsedObject is ImportStatement.ImportBindings)
						return new ImportStatementCompletionProvider (dataGen, (ImportStatement.ImportBindings)trackVars.LastParsedObject);
					else if (trackVars.LastParsedObject is ModuleStatement)
						return new ModuleStatementCompletionProvider(dataGen);
					else if ((trackVars.LastParsedObject is ITemplateParameter || 
					    trackVars.LastParsedObject is ForeachStatement) && EnteredText != null)
						return null;
				}
				
				if (EnteredText == "(")
					return null;
			}


			return new CtrlSpaceCompletionProvider(dataGen) { 
				trackVars=trackVars,
				curBlock=curBlock,
				curStmt = curStmt,
				parsedBlock=parsedBlock
			};
		}

		public static AbstractCompletionProvider BuildCompletionData(ICompletionDataGenerator dataGen, IEditorData editor, string EnteredText)
		{
			if (!IsCompletionAllowed(editor, EnteredText))
				return null;

			var provider = Create(dataGen, editor, EnteredText);

			if (provider != null)
				provider.BuildCompletionData(editor, EnteredText);

			return provider;
		}

		#region Helper Methods
		public static IExpression TryConvertTypeDeclaration(ITypeDeclaration td, bool ignoreInnerDeclaration = false)
		{
			if (td.InnerDeclaration == null || ignoreInnerDeclaration)
			{
				if (td is IdentifierDeclaration)
				{
					var id = td as IdentifierDeclaration;
					if (id.Id == null)
						return null;
					return new IdentifierExpression(id.Id) { Location = id.Location, EndLocation = id.EndLocation };
				}
				if (td is TemplateInstanceExpression)
					return td as IExpression;
				
				return null;
			}

			var pfa = new PostfixExpression_Access{
				PostfixForeExpression = TryConvertTypeDeclaration(td.InnerDeclaration),
				AccessExpression  = TryConvertTypeDeclaration(td, true)
			};
			if (pfa.PostfixForeExpression == null)
				return null;
			return pfa;
		}

		public static bool IsIdentifierChar(char key)
		{
			return char.IsLetterOrDigit(key) || key == '_';
		}

		public static bool CanItemBeShownGenerally(INode dn)
		{
			if (dn == null || string.IsNullOrEmpty(dn.Name))
				return false;

			if (dn is DMethod)
			{
				var dm = dn as DMethod;

				if (dm.SpecialType == DMethod.MethodType.Unittest ||
					dm.SpecialType == DMethod.MethodType.Destructor ||
					dm.SpecialType == DMethod.MethodType.Constructor)
					return false;
			}

			return true;
		}

		public static bool HaveSameAncestors(INode higherLeveledNode, INode lowerLeveledNode)
		{
			var curPar = higherLeveledNode;

			while (curPar != null)
			{
				if (curPar == lowerLeveledNode)
					return true;

				curPar = curPar.Parent;
			}
			return false;
		}

		public static bool IsTypeNode(INode n)
		{
			return n is DEnum || n is DClassLike;
		}

		/// <summary>
		/// Returns C:\fx\a\b when PhysicalFileName was "C:\fx\a\b\c\Module.d" , ModuleName= "a.b.c.Module" and WantedDirectory= "a.b"
		/// 
		/// Used when formatting package names in BuildCompletionData();
		/// </summary>
		public static string GetModulePath(string PhysicalFileName, string ModuleName, string WantedDirectory)
		{
			return GetModulePath(PhysicalFileName, ModuleName.Split('.').Length, WantedDirectory.Split('.').Length);
		}

		public static string GetModulePath(string PhysicalFileName, int ModuleNamePartAmount, int WantedDirectoryNamePartAmount)
		{
			var ret = "";

			var physFileNameParts = PhysicalFileName.Split('\\');
			for (int i = 0; i < physFileNameParts.Length - ModuleNamePartAmount + WantedDirectoryNamePartAmount; i++)
				ret += physFileNameParts[i] + "\\";

			return ret.TrimEnd('\\');
		}
		#endregion

		static bool IsCompletionAllowed(IEditorData Editor, string EnteredText)
		{
			if (Editor.CaretOffset > 0)
			{
				if (Editor.CaretLocation.Line == 1 && Editor.ModuleCode.Length > 0 && Editor.ModuleCode[0] == '#')
					return false;

				var enteredChar = string.IsNullOrEmpty(EnteredText) ? '\0' : EnteredText[0];

				if (enteredChar == '.' || enteredChar == '_')
				{
					// Don't complete on a double/multi-dot
					if (Editor.CaretOffset > 1 && Editor.ModuleCode[Editor.CaretOffset - 2] == enteredChar) 
						// ISSUE: When a dot was typed, off-1 is the dot position, 
						// if a letter was typed, off-1 is the char before the typed letter..
						return false;
				}
				// If typing a begun identifier, return immediately
				else if ((IsIdentifierChar(enteredChar) || enteredChar == '\0') &&
					IsIdentifierChar(Editor.ModuleCode[Editor.CaretOffset - 1]))
					return false;

				return !CaretContextAnalyzer.IsInCommentAreaOrString(Editor.ModuleCode, Editor.CaretOffset);
			}

			return true;
		}

		protected abstract void BuildCompletionDataInternal(IEditorData Editor, string EnteredText);

		public void BuildCompletionData(IEditorData Editor,
			string EnteredText)
		{
			BuildCompletionDataInternal(Editor, EnteredText);
		}
	}
}
