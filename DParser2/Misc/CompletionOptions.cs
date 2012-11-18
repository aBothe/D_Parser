using System.Xml;

namespace D_Parser.Misc
{
	public struct CompletionOptions
	{
		public readonly static CompletionOptions Default = new CompletionOptions
		{
			ShowUFCSItems = true,
			EnableDeclarationConstraints = true
		};


		public bool ShowUFCSItems;
		/// <summary>
		/// If true, type & expression resolution will happen including checking of existing declaration constraints.
		/// It might be disabled because of huge performance tear-downs.
		/// </summary>
		public bool EnableDeclarationConstraints;


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
				}
			}
		}

		public void Save(XmlWriter x)
		{
			x.WriteElementString("EnableUFCSCompletion", ShowUFCSItems.ToString());
			x.WriteElementString("EnableDeclarationConstraints", EnableDeclarationConstraints.ToString());
		}
	}
}
