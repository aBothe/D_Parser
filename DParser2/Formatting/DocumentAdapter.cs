using System;
using D_Parser.Dom;
using System.Collections.Generic;

namespace D_Parser.Formatting
{
	public interface IDocumentAdapter
	{
		char this[int offset]{get;}
		int ToOffset(CodeLocation loc);
		int ToOffset(int line, int column);
		CodeLocation ToLocation(int offset);
		int TextLength{get;}
		int LineCount{get;}
		string Text{get;}
	}
	
	public class TextDocument : IDocumentAdapter
	{		
		string text = string.Empty;
		public string Text{get{return text;} set{text = value; UpdateLineInfo();}}
		public char this[int o] { get{ return text[o]; } }
		/// <summary>
		/// Contains the start offsets of each line
		/// </summary>
		int[] lines;
		
		public int LineCount
		{
			get{ return lines != null ? lines.Length : 0; }
		}
		
		public int TextLength {
			get {
				return text == null ? 0 : text.Length;
			}
		}
		
		public int ToOffset(CodeLocation loc)
		{
			return ToOffset(loc.Line, loc.Column);
		}
		
		public int ToOffset(int line, int column)
		{
			if(line <= 0 || line > lines.Length)
				throw new IndexOutOfRangeException("Line must be a value between 1 and "+lines.Length+"; Was "+line);
			
			return lines[line-1] + column - 1;
		}
		
		public CodeLocation ToLocation(int offset)
		{
			if(text.Length == 0 || lines == null)
				return CodeLocation.Empty;
			else if(text.Length == 1)
				return new CodeLocation(offset + 1, 1);
			
			// Primitive binary choice for better performance in huge files
			if(offset < text.Length/2)
			{
				for(int i = 1; i < lines.Length; i++)
				{
					if(lines[i] > offset)
						return new CodeLocation(offset - lines[i-1] + 1, i);
				}
				return new CodeLocation(offset - lines[lines.Length-1] + 1, lines.Length);
			}
			else
			{
				for(int i = lines.Length-1; i > 0; i--)
				{
					if(lines[i] < offset)
						return new CodeLocation(offset - lines[i] + 1, i+1);
				}
				return new CodeLocation(offset + 1, 1);
			}
		}
		
		void UpdateLineInfo()
		{
			var l = new List<int>();
			
			int lastStart = 0;
			int len = text == null ? 0 : text.Length;
			for(int i = 0; i< len; i++)
			{
				if(text[i] == '\r')
				{
					if(i+1 < len && text[i+1] == '\n')
						i++;
					l.Add(lastStart);
					lastStart = i+1;
				}
				else if(text[i] == '\n')
				{
					l.Add(lastStart);
					lastStart = i+1;
				}
			}
			
			if(len > 0)
				l.Add(lastStart);
			
			lines = l.ToArray();
		}
	}
}
