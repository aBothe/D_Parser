using System;
using System.Text;
using D_Parser.Parser;

namespace D_Parser.Formatting.Indent
{
	[Flags]
	public enum Inside
	{
		Empty                  = 0,
		
		PreProcessor           = (1 << 0),
		Shebang                = (1 << 1),
		
		BlockComment           = (1 << 2),
		NestedComment          = (1 << 3),
		LineComment            = (1 << 4),
		DocComment             = (1 << 5),
		Comment                = (BlockComment | NestedComment | LineComment | DocComment),
		
		VerbatimString          = (1 << 6),
		AlternateVerbatimString = (1 << 14),
		StringLiteral           = (1 << 7),
		CharLiteral             = (1 << 8),
		String                  = (VerbatimString | StringLiteral | AlternateVerbatimString),
		StringOrChar            = (String | CharLiteral),
		
		Attribute               = (1 << 9),
		ParenList               = (1 << 10),
		SquareBracketList       = (1 << 15),
		
		FoldedStatement         = (1 << 11),
		Block                   = (1 << 12),
		Case                    = (1 << 13),
		
		FoldedOrBlock           = (FoldedStatement | Block | SquareBracketList),
		FoldedBlockOrCase       = (FoldedStatement | Block | SquareBracketList | Case)
	}
	
	/// <summary>
	/// Description of IndentStack.
	/// </summary>
	public class IndentStack: ICloneable {
		const int INITIAL_CAPACITY = 16;
		
		struct Node {
			public Inside inside;
			public byte keyword;
			public string indent;
			public int nSpaces;
			public int lineNr;
			
			public override string ToString ()
			{
				return string.Format ("[Node: inside={0}, keyword={1}, indent={2}, nSpaces={3}, lineNr={4}]", inside, DTokens.GetTokenString(keyword), indent, nSpaces, lineNr);
			}
		};
		
		Node[] stack;
		int size;
		IndentEngine ie;
		
		public IndentStack (IndentEngine ie,int capacity = INITIAL_CAPACITY)
		{
			this.ie = ie;
			if (capacity < INITIAL_CAPACITY)
				capacity = INITIAL_CAPACITY;
			
			this.stack = new Node [capacity];
			this.size = 0;
		}
		
		public bool IsEmpty {
			get { return size == 0; }
		}
		
		public int Count {
			get { return size; }
		}
		
		public object Clone ()
		{
			var clone = new IndentStack (ie,stack.Length);
			
			clone.stack = (Node[]) stack.Clone ();
			clone.size = size;
			
			return clone;
		}
		
		public void Reset ()
		{
			for (int i = 0; i < size; i++) {
				stack[i].keyword = DTokens.INVALID;
				stack[i].indent = null;
			}
			
			size = 0;
		}
		
		public void Push (Inside inside, byte keyword, int lineNr, int nSpaces)
		{
			int sp = size - 1;
			Node node;
			int n = 0;
			
			var indentBuilder = new StringBuilder ();
			if ((inside & (Inside.Attribute | Inside.ParenList)) != 0) {
				if (size > 0 && stack[sp].inside == inside) {
					while (sp >= 0) {
						if ((stack[sp].inside & Inside.FoldedOrBlock) != 0)
							break;
						sp--;
					}
					if (sp >= 0) {
						indentBuilder.Append (stack[sp].indent);
						if (stack[sp].lineNr == lineNr)
							n = stack[sp].nSpaces;
					}
				} else {
					while (sp >= 0) {
						if ((stack[sp].inside & Inside.FoldedBlockOrCase) != 0) {
							indentBuilder.Append (stack[sp].indent);
							break;
						}
						
						sp--;
					}
				}
				if (nSpaces - n <= 0) {
					indentBuilder.Append ('\t');
				} else {
					indentBuilder.Append (' ', nSpaces - n);
				}
			} else if ((inside & (Inside.NestedComment | Inside.BlockComment)) != 0) {
				if (size > 0) {
					indentBuilder.Append (stack[sp].indent);
					if (stack[sp].lineNr == lineNr)
						n = stack[sp].nSpaces;
				}
				
				indentBuilder.Append (' ', nSpaces - n);
			} else if (inside == Inside.Case) {
				while (sp >= 0) {
					if ((stack[sp].inside & Inside.FoldedOrBlock) != 0) {
						indentBuilder.Append (stack[sp].indent);
						break;
					}
					
					sp--;
				}
				
				if (ie.Options.IndentSwitchBody)
					indentBuilder.Append ('\t');
				
				nSpaces = 0;
			} else if ((inside & (Inside.FoldedOrBlock)) != 0) {
				while (sp >= 0) {
					if ((stack[sp].inside & Inside.FoldedBlockOrCase) != 0) { // Optional: Check for Inside.ParenList to align the following lines like the previous line
						indentBuilder.Append (stack[sp].indent);
						break;
					}
					
					sp--;
				}
				
				Inside parent = size > 0 ? stack[size - 1].inside : Inside.Empty;
				
				// This is a workaround to make anonymous methods indent nicely
				if (parent == Inside.ParenList)
					stack[size - 1].indent = indentBuilder.ToString ();

				if (inside == Inside.FoldedStatement) {
					indentBuilder.Append ('\t');
				} else if (inside == Inside.Block || inside == Inside.SquareBracketList) {
					if (parent != Inside.Case || nSpaces != -1)
						indentBuilder.Append ('\t');
				}
				
				nSpaces = 0;
			} else if ((inside & (Inside.Shebang | Inside.PreProcessor | Inside.StringOrChar)) != 0) {
				// if these fold, do not indent
				nSpaces = 0;
			} else if (inside == Inside.LineComment || inside == Inside.DocComment) {
				// can't actually fold, but we still want to push it onto the stack
				nSpaces = 0;
			} else {
				// not a valid argument?
				throw new ArgumentOutOfRangeException ();
			}

			// Replace additionally inserted spaces with tabs
			if (!ie.tabsToSpaces && !ie.keepAlignmentSpaces)
			{
				n = 0;
				for(int i = 0; i < indentBuilder.Length; i++)
				{
					if (indentBuilder[i] == ' ')
					{
						n++;
						if (n >= ie.indentWidth)
						{
							// Decrement the space count as we're having tabs now.
							nSpaces -= n;
							i -= n - 1;
							indentBuilder.Remove(i, n);
							indentBuilder.Insert(i, '\t');
							n = 0;
						}
					}
					else
						n = 0;
				}
			}
			node.indent = indentBuilder.ToString ();
			node.keyword = keyword;
			node.nSpaces = nSpaces;
			node.lineNr = lineNr;
			node.inside = inside;
			
			if (size == stack.Length)
				Array.Resize <Node> (ref stack, 2 * size);
			
			stack[size++] = node;
		}
		
		public void Push (Inside inside, byte keyword, int lineNr, int nSpaces, string indent)
		{
			Node node;
			
			node.indent = indent;
			node.keyword = keyword;
			node.nSpaces = nSpaces;
			node.lineNr = lineNr;
			node.inside = inside;
			
			if (size == stack.Length)
				Array.Resize <Node> (ref stack, 2 * size);
			
			stack[size++] = node;
		}
		
		public void Pop ()
		{
			if (size == 0)
				throw new InvalidOperationException ();
			
			int sp = size - 1;
			stack[sp].keyword = DTokens.INVALID;
			stack[sp].indent = null;
			size = sp;
		}
		
		public Inside PeekInside (int up)
		{
			if (up < 0)
				throw new ArgumentOutOfRangeException ();
			
			if (up >= size)
				return Inside.Empty;
			
			return stack[size - up - 1].inside;
		}
		
		public byte PeekKeyword
		{
			get{
				if(size > 0)
					return stack[size-1].keyword;
				return DTokens.INVALID;
			}
		}
		
		public byte GetKeywordInStack (int up)
		{
			if (up < 0)
				throw new ArgumentOutOfRangeException ();
			
			if (up >= size)
				return DTokens.INVALID;
			
			return stack[size - up - 1].keyword;
		}
		
		public string PeekIndent (int up)
		{
			if (up < 0)
				throw new ArgumentOutOfRangeException ();
			
			if (up >= size)
				return String.Empty;
			
			return stack[size - up - 1].indent;
		}
		
		public int PeekLineNr (int up)
		{
			if (up < 0)
				throw new ArgumentOutOfRangeException ();
			
			if (up >= size)
				return -1;
			
			return stack[size - up - 1].lineNr;
		}
	}
}
