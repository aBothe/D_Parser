// 
// Indent.cs
//  
// Author:
//       Mike Krüger <mkrueger@novell.com>
// 
// Copyright (c) 2010 Novell, Inc (http://www.novell.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;

namespace D_Parser.Formatting
{
	public enum IndentType {
		Block,
		Continuation,
		Label,
		/// <summary>
		/// Negative block indent. Used for section attribute.
		/// </summary>
		Negative
	}

	public class FormattingIndentStack
	{
		readonly Stack<IndentType> indentStack = new Stack<IndentType> ();
		readonly ITextEditorOptions options;
		
		int curIndent;

		public FormattingIndentStack(ITextEditorOptions options)
		{
			this.options = options;
		}

		public int StackCount
		{
			get{ return indentStack.Count; }
		}

		public void Push(IndentType type)
		{
			indentStack.Push(type);
			curIndent += GetIndent(type);
			Update();
		}

		public void Pop()
		{
			curIndent -= GetIndent(indentStack.Pop());
			Update();
		}
		
		public IndentType Peek
		{
			get{ return indentStack.Peek(); }
		}

		int GetIndent(IndentType indentType)
		{
			switch (indentType) {
				case IndentType.Block:
					return options.IndentSize;
				case IndentType.Continuation:
					return options.ContinuationIndent;
				case IndentType.Label:
					return options.LabelIndent;
				case IndentType.Negative:
					return -options.IndentSize;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		void Update()
		{
			if (options.TabsToSpaces) {
				indentString = new string(' ', curIndent);
				return;
			}
			indentString = new string('\t', curIndent / options.TabSize) + new string(' ', curIndent % options.TabSize) + new string (' ', ExtraSpaces);
		}

		int extraSpaces;

		public int ExtraSpaces {
			get {
				return extraSpaces;
			}
			set {
				extraSpaces = value;
				Update();
			}
		}

		string indentString;
		public string IndentString {
			get {
				return indentString;
			}
		}

		public override string ToString()
		{
			return string.Format("[Indent: curIndent={0}]", curIndent);
		}
	}
}
