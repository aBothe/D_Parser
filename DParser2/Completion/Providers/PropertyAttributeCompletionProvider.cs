
namespace D_Parser.Completion.Providers
{
	class PropertyAttributeCompletionProvider : AbstractCompletionProvider
	{
		public PropertyAttributeCompletionProvider(ICompletionDataGenerator cdg) : base(cdg) { }

		private static readonly string[] PropertyAttributeCompletionItems = new[] 
		{
			"disable",
			"property",
			"safe",
			"system",
			"trusted"
		};

		protected override void BuildCompletionDataInternal(IEditorData Editor, char enteredChar)
		{
			foreach (var propAttr in PropertyAttributeCompletionItems)
				CompletionDataGenerator.AddPropertyAttribute(propAttr);
		}
	}
}
