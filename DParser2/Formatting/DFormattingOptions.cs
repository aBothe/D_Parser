using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Formatting
{
	public enum GotoLabelIndentStyle {
		///<summary>Place goto labels in the leftmost column</summary>
		LeftJustify,
		
		/// <summary>
		/// Place goto labels one indent less than current
		/// </summary>
		OneLess,
		
		/// <summary>
		/// Indent goto labels normally
		/// </summary>
		Normal
	}
	
	public interface DFormattingOptionsFactory
	{
		DFormattingOptions Options{get;}
	}
	
	public class DFormattingOptions
	{
		public bool IndentSwitchBody;
		public GotoLabelIndentStyle LabelIndentStyle;
		
		public static DFormattingOptions CreateDStandard()
		{
			return new DFormattingOptions()
			{
				LabelIndentStyle = GotoLabelIndentStyle.OneLess,
				IndentSwitchBody = true
			};
		}
	}
}
