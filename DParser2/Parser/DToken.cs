using System;
using D_Parser.Dom;

namespace D_Parser.Parser
{
	[Flags]
	public enum LiteralFormat : byte
	{
		None = 0,
		Scalar = 1,
		FloatingPoint = 2,
		StringLiteral = 4,
		VerbatimStringLiteral = 8,
		CharLiteral = 16,
	}

	[Flags]
	public enum LiteralSubformat : byte
	{
		None = 0,

		Integer = 1,
		Unsigned = 2,
		Long = 4,

		Double = 8,
		Float = 16,
		Real = 32,
		Imaginary = 64,

		Utf8=128,
		Utf16=129,
		Utf32=130,
	}

	public class DToken
	{
		#region Properties
		public int Line;
		internal int Column;
		internal ushort EndLineDifference; // A token shouldn't be greater than 65536, right?
		internal int EndColumn;
		public CodeLocation Location
		{
			get{return new CodeLocation(Column, Line);}
		}
		public CodeLocation EndLocation
		{
			get{return new CodeLocation(EndColumn, unchecked(Line+EndLineDifference));}
		}

		public byte Kind;
        public LiteralFormat LiteralFormat;
		/// <summary>
		/// Used for scalar, floating and string literals.
		/// Marks special formats such as explicit unsigned-ness, wide char or dchar-based strings etc.
		/// </summary>
		public LiteralSubformat Subformat;
        public object LiteralValue;
        //public readonly string Value;
        public string Value {get{return LiteralValue as string;}}
        internal string RawCodeRepresentation;
        internal DToken next;

		public DToken Next
		{
			get { return next; }
		}		
		#endregion

		public override string ToString()
        {
            if (Kind == DTokens.Identifier || Kind == DTokens.Literal)
            	return LiteralValue is string ? LiteralValue as string : LiteralValue.ToString();
            return DTokens.GetTokenString(Kind);
        }
    }

    public class Comment
    {
		[Flags]
		public enum Type : byte
        {
            Block=1,
            SingleLine=2,
            Documentation=4
        }

        public readonly Type CommentType;
        public readonly string CommentText;
        public readonly CodeLocation StartPosition;
        public readonly CodeLocation EndPosition;
        
        /// <value>
        /// Is true, when the comment is at line start or only whitespaces
        /// between line and comment start.
        /// </value>
        public readonly bool CommentStartsLine;

        public Comment(Type commentType, string comment, bool commentStartsLine, CodeLocation startPosition, CodeLocation endPosition)
        {
            this.CommentType = commentType;
            this.CommentText = comment;
            this.CommentStartsLine = commentStartsLine;
            this.StartPosition = startPosition;
            this.EndPosition = endPosition;
        }
    }
}