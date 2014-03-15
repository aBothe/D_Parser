using D_Parser.Dom;

namespace D_Parser.Completion
{
	public interface ICompletionDataGenerator
	{
		/// <summary>
		/// Adds a token entry
		/// </summary>
		void Add(byte Token);

		/// <summary>
		/// Adds a property attribute
		/// </summary>
		void AddPropertyAttribute(string AttributeText);

		void AddIconItem(string iconName, string text, string description);
		void AddTextItem(string Text, string Description);

		/// <summary>
		/// Adds a node to the completion data
		/// </summary>
		/// <param name="Node"></param>
		void Add(INode Node);

		void AddModule(DModule module,string nameOverride = null);
		void AddPackage(string packageName);

		/// <summary>
		/// Used for method override completion
		/// </summary>
		void AddCodeGeneratingNodeItem(INode node, string codeToGenerate);

		/// <summary>
		/// Notifies completion high-levels that the completion has been stopped due to taking too much time.
		/// After this message has been sent, other completion items may still be added!
		/// </summary>
		void NotifyTimeout();
	}
}
