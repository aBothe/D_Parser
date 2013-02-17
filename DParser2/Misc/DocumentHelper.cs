using D_Parser.Dom;

namespace D_Parser
{
	public class DocumentHelper
	{
		public static CodeLocation OffsetToLocation(string Text, int Offset)
		{
			int line = 1;
			int col = 1;

			char c = '\0';
			for (int i = 0; i < Offset; i++)
			{
				c = Text[i];

				col++;

				if (c == '\n')
				{
					line++;
					col = 1;
				}
			}

			return new CodeLocation(col, line);
		}
		
		public static int LocationToOffset(string Text, CodeLocation Location)
		{
			return LocationToOffset(Text, Location.Line, Location.Column);
		}

		public static int LocationToOffset(string Text, int line, int column)
		{
			int curline = 1;
			int col = 1;

			int i = 0;
			for (; i < Text.Length && !(curline >= line && col >= column); i++)
			{
				col++;

				if (Text[i] == '\n')
				{
					curline++;
					col = 1;
				}
			}

			return i;
		}

		public static int GetLineEndOffset(string Text, int line)
		{
			int curline = 1;
			
			int i = 0;
			for (; i < Text.Length && curline <= line; i++)
				if (Text[i] == '\n')
				{
					curline++;

					if (curline > line)
					{
						if (i > 0 && Text[i - 1] == '\r')
							return i-1;

						return i;
					}
				}

			return i;
		}

		public static int GetOffsetByRelativeLocation(string Text, CodeLocation caret, int caretOffset, CodeLocation target)
		{
			if (string.IsNullOrEmpty(Text))
				return 0;

			int line = caret.Line;

			if (caretOffset >= Text.Length)
				caretOffset = Text.Length - 1;

			if (caret > target)
			{
				if (caret.Column > 1 && Text[caretOffset] == '\n') // Won't occur on windows -- at a line end there will only be \r (and afterwards \n)
					caretOffset--;

				if (target.Line == 1)
					return target.Column - 1;

				for (; caretOffset >= 0; caretOffset--)
				{
					if (Text[caretOffset] == '\n')
					{
						line--;

						if (line < target.Line)
							return caretOffset + target.Column;
					}
				}
			}
			else if (caret < target)
			{
				int len = Text.Length;
				for (; caretOffset < len; caretOffset++)
				{
					if (Text[caretOffset] == '\n')
					{
						line++;
						
						if (line >= target.Line)
							return caretOffset + target.Column;
					}
				}
			}

			return caretOffset;
		}
	}
}
