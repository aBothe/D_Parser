using System;
using System.Collections.Generic;
using System.Text;

using D_Parser.Dom;

namespace D_Parser.Formatting
{
	public partial class DFormattingVisitor : DefaultDepthFirstVisitor
	{
		public override void Visit(DClassLike n)
		{
			base.Visit(n);
		}
		
		public override void VisitBlock(DBlockNode block)
		{
			if(block is DModule)
			{
				base.VisitBlock(block);
				return;
			}
			
			
			
			EnforceBraceStyle(policy.TypeBlockBraces, block.BlockStartLocation, block.EndLocation.Line, block.EndLocation.Column-1);
			
			curIndent.Push(IndentType.Block);
			
			base.VisitBlock(block);
			
			curIndent.Pop();
		}
	}
}
