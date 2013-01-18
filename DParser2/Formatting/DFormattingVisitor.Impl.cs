using System;
using System.Collections.Generic;
using System.Text;

using D_Parser.Dom;

namespace D_Parser.Formatting
{
	public partial class DFormattingVisitor : DefaultDepthFirstVisitor
	{
		#region Nodes
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
			
			FormatAttributedNode(block);
			
			EnforceBraceStyle(policy.TypeBlockBraces, block.BlockStartLocation, block.EndLocation.Line, block.EndLocation.Column-1);
			
			curIndent.Push(IndentType.Block);
			
			base.VisitBlock(block);
			
			curIndent.Pop();
		}
		
		/// <summary>
		/// Call before pushing a new indent level and before call base.Visit for the respective node!
		/// </summary>
		void FormatAttributedNode(DNode n)
		{
			if(n.Attributes != null)
			foreach(var a in n.Attributes)
			{
				FixIndentationForceNewLine(a.Location);
			}
			
			FixIndentationForceNewLine(n.Location);
		}
		#endregion
		
		#region Attributes

		#endregion
	}
}
