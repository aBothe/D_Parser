using System.IO;

namespace D_Parser.Misc
{
	/// <summary>
	/// Read substrings out of a string without copying it.
	/// </summary>
	public class StringView : TextReader
	{
		string s;
		int i;
		int end;

		public StringView(string code, int begin, int length)
		{
			s = code;
			i = begin;
			end = begin + length;
		}

		public override int Peek()
		{
			if (i >= end)
				return -1;
			return s[i];
		}

		public override int Read()
		{
			if (i >= end)
				return -1;
			return s[i++];
		}

		public override int ReadBlock(char[] buffer, int index, int count)
		{
			int copied = System.Math.Min(s.Length - i, count - index); //TODO: Is this correct?
			s.CopyTo(i, buffer, index, copied);
			return copied;
		}

		public override int Read(char[] buffer, int index, int count)
		{
			return this.ReadBlock(buffer, index, count);
		}

		public override string ReadLine()
		{
			int start = i;

			while (i <= end && s[i] != '\n')
				i++;

			if (i <= end) // There had to be a \n
				i++;

			return s.Substring(start, end - i);
		}

		public override string ReadToEnd()
		{
			var s_ = s.Substring(i);
			i = end;
			return s_;
		}
	}
}
