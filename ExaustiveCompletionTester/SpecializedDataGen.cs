using System.Collections.Generic;
using D_Parser.Completion;
using D_Parser.Dom;
using System;

namespace ExaustiveCompletionTester
{
	public sealed class SpecializedDataGen : ICompletionDataGenerator
	{
		public SpecializedDataGen() { }
		public void AddCodeGeneratingNodeItem (INode node, string codeToGenerate) { }
		public void Add (byte token) { }
		public void AddPropertyAttribute (string attributeText) { }
		public void AddTextItem (string text, string description) { }
		public void AddIconItem (string iconName, string text, string description) { }
		public void Add (INode n) { }
		public void AddModule (DModule module, string nameOverride = null) { }
		public void AddPackage (string packageName) { }
		public void NotifyTimeout()
		{
			throw new OperationCanceledException();
		}
	}

}

