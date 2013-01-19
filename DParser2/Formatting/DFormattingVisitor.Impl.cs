using System;
using System.Collections.Generic;
using System.Text;

using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Formatting
{
	public partial class DFormattingVisitor : DefaultDepthFirstVisitor
	{
		#region Nodes
		public override void Visit(DEnum n)
		{
			base.Visit(n);
		}
		
		public override void Visit(DMethod n)
		{
			FormatAttributedNode(n);
			
			// Find in, out(...) and body tokens
			if(!n.InToken.IsEmpty){
				FixIndentationForceNewLine(n.InToken);
			}
			if(!n.OutToken.IsEmpty){
				FixIndentationForceNewLine(n.OutToken);
			}
			if(!n.BodyToken.IsEmpty){
				FixIndentationForceNewLine(n.BodyToken);
			}
			
			base.Visit(n);
		}
		
		public override void Visit(DClassLike n)
		{
			base.Visit(n);
		}
		
		public override void Visit(DVariable n)
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
		
		public override void VisitDNode(DNode n)
		{
			base.VisitDNode(n);
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
		
		void FormatParameters()
		{
			
		}
		#endregion
		
		#region Attributes

		#endregion
		
		#region Statements
		public override void Visit(BlockStatement s)
		{
			EnforceBraceStyle(policy.TypeBlockBraces, s.Location, s.EndLocation.Line, s.EndLocation.Column-1);
			
			curIndent.Push(IndentType.Block);
			base.Visit(s);
			curIndent.Pop();
		}
		#endregion
	}
}
