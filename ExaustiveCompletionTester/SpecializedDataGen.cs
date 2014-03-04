using System.Collections.Generic;
using D_Parser.Completion;
using D_Parser.Dom;

namespace ExaustiveCompletionTester
{
	public sealed class SpecializedDataGen : ICompletionDataGenerator
	{
		public SpecializedDataGen()
		{
		}

		public void AddCodeGeneratingNodeItem (INode node, string codeToGenerate)
		{

		}

		public List<byte> Tokens = new List<byte> ();
		public void Add (byte Token)
		{
			Tokens.Add (Token);
		}

		public List<string> Attributes = new List<string> ();
		public void AddPropertyAttribute (string AttributeText)
		{
			Attributes.Add (AttributeText);
		}

		public void AddTextItem (string Text, string Description)
		{

		}

		public void AddIconItem (string iconName, string text, string description)
		{

		}

		public List<INode> addedItems = new List<INode> ();
		public void Add (INode n)
		{
			addedItems.Add (n);
		}

		public void AddModule (DModule module, string nameOverride = null)
		{
			this.Add (module);
		}

		public List<string> Packages = new List<string> ();
		public void AddPackage (string packageName)
		{
			Packages.Add (packageName);
		}
	}

}

