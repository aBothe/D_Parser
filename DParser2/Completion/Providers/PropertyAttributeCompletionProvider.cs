
namespace D_Parser.Completion
{
	class PropertyAttributeCompletionProvider : AbstractCompletionProvider
	{
		public PropertyAttributeCompletionProvider(ICompletionDataGenerator cdg) : base(cdg) { }

		protected override void BuildCompletionDataInternal(IEditorData Editor, char enteredChar)
		{
			foreach (var propAttr in new[] {
					"disable",
					"property",
					"safe",
					"system",
					"trusted"
				})
				CompletionDataGenerator.AddPropertyAttribute(propAttr);
		}
	}
}
