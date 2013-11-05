using System.Xml;

namespace D_Parser.Misc
{
	public class CompletionOptions
	{
		public static CompletionOptions Instance = new CompletionOptions();

		public bool LimitResolutionErrors = System.Diagnostics.Debugger.IsAttached;
		public bool DumpResolutionErrors = System.Diagnostics.Debugger.IsAttached;

		public bool EnableSuggestionMode = false;

		public bool ShowUFCSItems = true;
		/// <summary>
		/// If true, type & expression resolution will happen including checking of existing declaration constraints.
		/// It might be disabled because of huge performance tear-downs.
		/// </summary>
		public bool EnableDeclarationConstraints = true;

		public bool DisableMixinAnalysis = true;
		public bool HideDeprecatedNodes = true;


		public void Load(XmlReader x)
		{
			while (x.Read())
			{
				switch (x.LocalName)
				{
					case "EnableUFCSCompletion":
						ShowUFCSItems = x.ReadString().ToLower() == "true";
						break;
					case "EnableDeclarationConstraints":
						EnableDeclarationConstraints = x.ReadString().ToLower() == "true";
						break;
					case "MixinAnalysis":
						DisableMixinAnalysis = x.ReadString().ToLower() != "true";
						break;
					case "CompletionSuggestionMode":
						EnableSuggestionMode = x.ReadString().ToLower() == "true";
						break;
					case "HideDeprecatedNodes":
						HideDeprecatedNodes = x.ReadString().ToLower() == "true";
						break;
				}
			}
		}

		public void Save(XmlWriter x)
		{
			x.WriteElementString("EnableUFCSCompletion", ShowUFCSItems.ToString());
			x.WriteElementString("EnableDeclarationConstraints", EnableDeclarationConstraints.ToString());
			x.WriteElementString("MixinAnalysis", (!DisableMixinAnalysis).ToString());
			x.WriteElementString("CompletionSuggestionMode", EnableSuggestionMode.ToString());
			x.WriteElementString("HideDeprecatedNodes", HideDeprecatedNodes.ToString());
		}
	}
}
