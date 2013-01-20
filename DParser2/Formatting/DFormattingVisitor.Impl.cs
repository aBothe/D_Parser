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
			
			if(n.Type != null)
				ForceSpacesBeforeRemoveNewLines(n.NameLocation, true);
			
			var nameLength = 0;
			
			switch(n.SpecialType)
			{
				case DMethod.MethodType.Destructor:
				case DMethod.MethodType.Constructor:
					nameLength = 4; // this
					break;
				case DMethod.MethodType.Normal:
					nameLength = n.Name.Length;
					break;
			}
			
			if(nameLength > 0)
				ForceSpacesAfterRemoveLines(new CodeLocation(n.NameLocation.Column + nameLength, n.NameLocation.Line),false);
			
			//MakeOnlySpacesToNextNonWs(name + nameLength - 1,0);
			
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
			
			EnsureBlankLinesAfter(n.EndLocation, policy.LinesAfterNode);
		}
		
		public override void Visit(DClassLike n)
		{
			base.Visit(n);
		}
		
		public override void Visit(DVariable n)
		{
			//FormatAttributedNode(n);
			
			// Check if we're inside a multi-declaration like int a,b,c;			
			if(n.Type != null)
				n.Type.Accept(this);
			
			var lastNonWs = SearchWhitespaceStart(document.ToOffset(n.NameLocation)) - 1;
			if(lastNonWs > 0 && document[lastNonWs] == ','){
				switch(policy.MultiVariableDeclPlacement)
				{
					case NewLinePlacement.NewLine:
						FixIndentationForceNewLine(n.NameLocation);
						break;
					case NewLinePlacement.SameLine:
						ForceSpacesBeforeRemoveNewLines(n.NameLocation, true);
						break;
				}
			}
			
			if(n.Initializer != null)
			{
				//MakeOnlySpacesToNextNonWs(nameOffset + n.Name.Length - 1, 1);
				curIndent.Push(IndentType.Block);
				if (n.NameLocation.Line != n.Initializer.Location.Line) {
					FixStatementIndentation(n.Initializer.Location);
				}
				n.Initializer.Accept(this);
				curIndent.Pop ();
			}
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
			
			EnsureBlankLinesAfter(block.EndLocation, policy.LinesAfterNode);
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
				if(a is AtAttribute)
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
		
		public override void Visit(DeclarationStatement s)
		{
			FixStatementIndentation(s.Location);
			FixSemicolon(s.EndLocation);
			
			base.Visit(s);
		}
		
		public override void Visit(ExpressionStatement s)
		{
			FixStatementIndentation(s.Location);
			FixSemicolon(s.EndLocation);
			
			base.Visit(s);
		}
		#endregion
	}
}
