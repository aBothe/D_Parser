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
	
	public class DFormattingOptions : ICloneable
	{
		#region Nodes
		/// <summary>
		/// For classes, struct definitions etc.
		/// </summary>
		public BraceStyle TypeBlockBraces = BraceStyle.EndOfLine;
		public int LinesBeforeNode = 1;
		public int LinesAfterNode = 1;
		
		/// <summary>
		/// int a = 34; // Shall '= 34' be forced to reside on the same line as 'int a'?
		/// </summary>
		public bool ForceVarInitializerOnSameLine = true;
		/// <summary>
		/// Forces a declaration's name to stay on the same line as its type.
		/// int a; // Shall 'a' be placed on the same line or could it stay somewhere else?
		/// </summary>
		public bool ForceNodeNameOnSameLine = true;
		/// <summary>
		/// int a, b, c; -- Should a,b,c be placed on the same line, or on a new line?
		/// </summary>
		public NewLinePlacement MultiVariableDeclPlacement = NewLinePlacement.NewLine;
		#endregion
		
		public bool IndentSwitchBody = true;
		public bool IndentCases = true;
		public GotoLabelIndentStyle LabelIndentStyle = GotoLabelIndentStyle.OneLess;
		
		public static DFormattingOptions CreateDStandard()
		{
			return new DFormattingOptions();
		}

		public object Clone()
		{
			return new DFormattingOptions { 
				TypeBlockBraces = TypeBlockBraces,
				LinesBeforeNode = LinesBeforeNode,
				LinesAfterNode = LinesAfterNode,
				ForceVarInitializerOnSameLine = ForceVarInitializerOnSameLine,
				ForceNodeNameOnSameLine = ForceNodeNameOnSameLine,
				MultiVariableDeclPlacement = MultiVariableDeclPlacement,
				IndentSwitchBody = IndentSwitchBody,
				LabelIndentStyle = LabelIndentStyle,
				IndentCases = IndentCases,
			};
		}
	}
}
