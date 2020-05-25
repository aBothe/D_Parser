using D_Parser.Dom;

namespace D_Parser.Completion.Providers
{
	public abstract class AbstractCompletionProvider
	{
		protected readonly ICompletionDataGenerator CompletionDataGenerator;
		
		protected AbstractCompletionProvider(ICompletionDataGenerator completionDataGenerator)
		{
			CompletionDataGenerator = completionDataGenerator;
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

		protected abstract void BuildCompletionDataInternal(IEditorData editor, char enteredChar);

		public void BuildCompletionData(IEditorData editor, char enteredChar) =>
			BuildCompletionDataInternal(editor, enteredChar);
	}
}
