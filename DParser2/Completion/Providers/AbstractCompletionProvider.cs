using D_Parser.Dom.Expressions;
using D_Parser.Dom;

namespace D_Parser.Completion.Providers
{
	abstract class AbstractCompletionProvider
	{
		public readonly ICompletionDataGenerator CompletionDataGenerator;
		
		public AbstractCompletionProvider(ICompletionDataGenerator CompletionDataGenerator)
		{
			this.CompletionDataGenerator = CompletionDataGenerator;
		}

		#region Helper Methods
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
