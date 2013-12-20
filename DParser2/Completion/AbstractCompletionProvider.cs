using D_Parser.Dom.Expressions;
using D_Parser.Dom;

namespace D_Parser.Completion
{
	public abstract class AbstractCompletionProvider
	{
		public readonly ICompletionDataGenerator CompletionDataGenerator;
		
		public AbstractCompletionProvider(ICompletionDataGenerator CompletionDataGenerator)
		{
			this.CompletionDataGenerator = CompletionDataGenerator;
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
