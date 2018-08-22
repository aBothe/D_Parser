using System;

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
}