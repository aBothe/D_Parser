using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Refactoring
{
	public interface ITextDocument
	{
		int LocationToOffset(int line, int col);
		int OffsetToLineNumber(int offset);
		string EolMarker { get; }
		string GetLineIndent(int line);

		int Length { get; }
		char GetCharAt(int offset);
		void Remove(int offset, int length);
		void Insert(int offset, string text);
	}
}
