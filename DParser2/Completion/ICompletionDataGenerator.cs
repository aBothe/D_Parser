using D_Parser.Dom;

namespace D_Parser.Completion
{
	public interface ICompletionDataGenerator
	{
		/// <summary>
		/// Adds a token entry
		/// </summary>
		void Add(byte token);

		/// <summary>
		/// Adds a property attribute
		/// </summary>
		void AddPropertyAttribute(string attributeText);

		void AddIconItem(string iconName, string text, string description);
		void AddTextItem(string text, string description);

		/// <summary>
		/// Adds a node to the completion data
		/// </summary>
		/// <param name="node"></param>
		void Add(INode node);

		void AddModule(DModule module,string nameOverride = null);
		void AddPackage(string packageName);

		/// <summary>
		/// Used for method override completion
		/// </summary>
		void AddCodeGeneratingNodeItem(INode node, string codeToGenerate);

		/// <summary>
		/// Sets a prefix or a 'contains'-Pattern that indicates items that will be added later on.
		/// The first matching item may be pre-selected in the completion list then.
		/// </summary>
		void SetSuggestedItem(string item);

		/// <summary>
		/// Notifies completion high-levels that the completion has been stopped due to taking too much time.
		/// After this message has been sent, other completion items may still be added!
		/// </summary>
		void NotifyTimeout();

		/// <summary>
		/// Used for calculating the text region that shall be replaced on a completion commit.
		/// Can be null.
		/// </summary>
		ISyntaxRegion TriggerSyntaxRegion { set; }
	}
}
