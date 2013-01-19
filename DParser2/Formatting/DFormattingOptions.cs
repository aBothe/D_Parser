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
	
	public enum BraceStyle
	{
		DoNotChange,
		EndOfLine,
		EndOfLineWithoutSpace,
		NextLine,
		NextLineShifted,
		NextLineShifted2
	}

	public enum BraceForcement
	{
		DoNotChange,
		RemoveBraces,
		AddBraces
	}

	public enum PropertyFormatting
	{
		AllowOneLine,
		ForceOneLine,
		ForceNewLine
	}

	public enum Wrapping {
		DoNotChange,
		DoNotWrap,
		WrapAlways,
		WrapIfTooLong
	}

	public enum NewLinePlacement {
		DoNotCare,
		NewLine,
		SameLine
	}
	
	public interface DFormattingOptionsFactory
	{
		DFormattingOptions Options{get;}
	}
	
	public class DFormattingOptions
	{
		#region Nodes
		/// <summary>
		/// For classes, struct definitions etc.
		/// </summary>
		public BraceStyle TypeBlockBraces = BraceStyle.EndOfLine;
		public int LinesBeforeNode = 1;
		public int LinesAfterNode = 1;
		#endregion
		
		public bool IndentSwitchBody = true;
		public GotoLabelIndentStyle LabelIndentStyle = GotoLabelIndentStyle.OneLess;
		
		public static DFormattingOptions CreateDStandard()
		{
			return new DFormattingOptions();
		}
	}
}
