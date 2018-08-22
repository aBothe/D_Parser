using D_Parser.Dom;

namespace D_Parser.Parser
{
	public sealed class DToken
	{
		public int Line;
		internal int Column;
		internal ushort EndLineDifference; // A token shouldn't be greater than 65536, right?
		internal int EndColumn;
		public CodeLocation Location
		{
			get { return new CodeLocation(Column, Line); }
		}
		public CodeLocation EndLocation
		{
			get { return new CodeLocation(EndColumn, unchecked(Line + EndLineDifference)); }
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
		public string Value { get { return LiteralValue as string; } }
		internal string RawCodeRepresentation;
		internal DToken next;

		public DToken Next
		{
			get { return next; }
		}

		public override string ToString()
		{
			if (Kind == DTokens.Identifier || Kind == DTokens.Literal)
				return LiteralValue is string ? LiteralValue as string : LiteralValue.ToString();
			return DTokens.GetTokenString(Kind);
		}
	}
}