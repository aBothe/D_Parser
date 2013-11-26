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

		internal static AbstractCompletionProvider Create(ICompletionDataGenerator dataGen, IEditorData Editor, char ch)
		{
			if (ch == '@')
				return new PropertyAttributeCompletionProvider(dataGen);

			ParserTrackerVariables trackVars;
			IBlockNode curBlock = null;
			IStatement curStmt;

			var parsedBlock = CtrlSpaceCompletionProvider.FindCurrentCaretContext(Editor, out trackVars, ref curBlock, out curStmt);

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
				else if (ex is UnaryExpression_Type)
				{
					pfa = null;
					//TODO: (Type). -- lookup static properties, fields and methods.
				}
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
					else if ((trackVars.LastParsedObject is TemplateParameter || 
						trackVars.LastParsedObject is ForeachStatement) && ch != '\0')
						return null;
				}
				
				if (ch == '(')
					return null;
			}

			return new CtrlSpaceCompletionProvider(dataGen) { 
				trackVars=trackVars,
				curBlock=curBlock,
				curStmt = curStmt,
				parsedBlock=parsedBlock
			};
		}

		[Obsolete("Use CodeCompletion.GenerateCompletionData instead!")]
		public static AbstractCompletionProvider BuildCompletionData(ICompletionDataGenerator dataGen, IEditorData editor, string EnteredText)
		{
			CodeCompletion.GenerateCompletionData (editor, dataGen, string.IsNullOrEmpty (EnteredText) ? '\0' : EnteredText [0]);
			return null;
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

		public static bool CanItemBeShownGenerally(INode dn)
		{
			if (dn == null || dn.NameHash == 0)
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
		#endregion



		protected abstract void BuildCompletionDataInternal(IEditorData Editor, char enteredChar);

		public void BuildCompletionData(IEditorData Editor,
			char enteredChar)
		{
			BuildCompletionDataInternal(Editor, enteredChar);
		}
	}
}
