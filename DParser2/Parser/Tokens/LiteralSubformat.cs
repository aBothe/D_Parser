using System;

namespace D_Parser.Parser
{
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

		Utf8 = 128,
		Utf16 = 129,
		Utf32 = 130,
	}
}