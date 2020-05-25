using System.Xml;

namespace D_Parser.Misc
{
	public class CompletionOptions
	{
		public static CompletionOptions Instance = new CompletionOptions();

		public bool LimitResolutionErrors = !System.Diagnostics.Debugger.IsAttached;
		public bool DumpResolutionErrors = System.Diagnostics.Debugger.IsAttached;
		/// <summary>
		/// Time a completion request has until it quits silently. Milliseconds. -1 for inifinite time.
		/// </summary>
		public int CompletionTimeout = -1; //100;

		public bool EnableSuggestionMode = true;

		public bool ShowUFCSItems = true;
		/// <summary>
		/// If true, type &amp; expression resolution will happen including checking of existing declaration constraints.
		/// It might be disabled because of huge performance tear-downs.
		/// </summary>
		public bool EnableDeclarationConstraints = true;

		public bool DisableMixinAnalysis = true;
		public bool HideDeprecatedNodes = true;
		public bool HideDisabledNodes = true;
		/// <summary>
		/// If false, all ctrl+space-leveled items will get shown in completion either
		/// </summary>
		public bool ShowStructMembersInStructInitOnly = true;
		public bool EnableResolutionCache = true;

		public void Load(XmlReader x)
		{
			while (x.Read())
			{
				switch (x.LocalName)
				{
					case "EnableResolutionCache":
						EnableResolutionCache = x.ReadString().ToLower() == "true";
					break;
					case "CompletionTimeout":
						int.TryParse(x.ReadString(), out CompletionTimeout);
						break;
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
					case "HideDisabledNodes":
						HideDisabledNodes = x.ReadString().ToLower() == "true";
						break;
					case "ShowStructMembersInStructInitOnly":
						ShowStructMembersInStructInitOnly = x.ReadString().ToLower() == "true";
						break;
				}
			}
		}

		public void Save(XmlWriter x)
		{
			x.WriteElementString("CompletionTimeout", CompletionTimeout.ToString());
			x.WriteElementString("EnableUFCSCompletion", ShowUFCSItems.ToString());
			x.WriteElementString("EnableDeclarationConstraints", EnableDeclarationConstraints.ToString());
			x.WriteElementString("MixinAnalysis", (!DisableMixinAnalysis).ToString());
			x.WriteElementString("CompletionSuggestionMode", EnableSuggestionMode.ToString());
			x.WriteElementString("HideDeprecatedNodes", HideDeprecatedNodes.ToString());
			x.WriteElementString("HideDisabledNodes", HideDisabledNodes.ToString());
			x.WriteElementString("ShowStructMembersInStructInitOnly", ShowStructMembersInStructInitOnly.ToString());
			x.WriteElementString("EnableResolutionCache", EnableResolutionCache.ToString());
		}
	}
}
