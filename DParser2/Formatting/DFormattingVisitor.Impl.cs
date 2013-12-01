using System;
using System.Collections.Generic;
using System.Text;

using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Dom.Expressions;

namespace D_Parser.Formatting
{
	public partial class DFormattingVisitor : DefaultDepthFirstVisitor
	{
		List<DAttribute> AlreadyHandledAttributes = new List<DAttribute>();
		
		#region Nodes
		public override void VisitChildren(IBlockNode block)
		{
			var children = new List<INode>(block.Children);
			var staticStmts = new List<StaticStatement>();
			if(block is DBlockNode)
				staticStmts.AddRange((block as DBlockNode).StaticStatements);
			
			var dbn = block as DBlockNode;
			if(dbn != null && dbn.MetaBlocks.Count != 0)
			{
				var mbStack = new Stack<IMetaDeclaration>();
				
				for(int i = 0; i < dbn.MetaBlocks.Count; i++)
				{
					var mb = dbn.MetaBlocks[i];
				handleElse:
					
					// If mb is inside the peek meta decl of mbStack, push mb on top of it
					IMetaDeclaration peekMb; 
					if(mbStack.Count == 0 || (mb.Location > (peekMb = mbStack.Peek()).Location && 
					                          mb.EndLocation < peekMb.EndLocation && 
					                          !(peekMb is AttributeMetaDeclarationSection))){
						mbStack.Push(mb);
					}
					else
					{
						while(mbStack.Count > 1 && curIndent.Peek == IndentType.Label)
						{
							VisitMetaBlockChildren(children, staticStmts, mbStack.Pop());
							curIndent.Pop();
						}
						
						VisitMetaBlockChildren(children, staticStmts, mbStack.Pop());
						curIndent.Pop();
					}
					
					// Format the header
					
					// private: or @asdf():
					if(mb is AttributeMetaDeclarationSection)
					{
						if(curIndent.Peek == IndentType.Label)
							curIndent.Pop();
						
						var mds = mb as AttributeMetaDeclarationSection;
						switch(policy.LabelIndentStyle)
						{
							case GotoLabelIndentStyle.OneLess:
								// Apply a negative indent to make it stick out one tab
								curIndent.Push(IndentType.Negative);
								FixIndentationForceNewLine(mds.Location);
								curIndent.Pop();
								break;
							case GotoLabelIndentStyle.LeftJustify:
								int originalIndent = curIndent.StackCount;
								for(int k = originalIndent; k != 0; k--)
									curIndent.Push(IndentType.Negative);
								FixIndentationForceNewLine(mds.Location);
								for(int k = originalIndent; k != 0; k--)
									curIndent.Pop();
								break;
							default:
								FixIndentationForceNewLine(mds.Location);
								break;
						}
						
						// Push the colon back to the last attribute's end location
						ForceSpacesAfterRemoveLines(mds.AttributeOrCondition[mds.AttributeOrCondition.Length-1].EndLocation,false);
						curIndent.Push(IndentType.Label);
					}
					else
					{
						FixIndentationForceNewLine(mb.Location);
						
						// Format braces for non-section meta blocks
						
						if(mb is IMetaDeclarationBlock)
						{
							var mdb = mb as IMetaDeclarationBlock;
							if(mdb is MetaDeclarationBlock)
							{
								// Do not format the opener bracket
								FixIndentationForceNewLine(mdb.EndLocation);
							}
							else
							{
								EnforceBraceStyle(policy.TypeBlockBraces, mdb.BlockStartLocation, mdb.EndLocation.Line, mdb.EndLocation.Column-1);
							}
						}
						
						curIndent.Push(IndentType.Block);
					}
					
					if(mb is AttributeMetaDeclaration && (mb as AttributeMetaDeclaration).OptionalElseBlock != null)
					{
						mb = (mb as AttributeMetaDeclaration).OptionalElseBlock;
						goto handleElse;
					}
				}
				
				while(mbStack.Count > 0)
				{
					VisitMetaBlockChildren(children, staticStmts, mbStack.Peek());
					mbStack.Pop();
					curIndent.Pop();
				}
			}
			
			// Handle all children that have not been handled yet.
			foreach (INode child in children)
				child.Accept(this);
		}
		
		void VisitMetaBlockChildren(List<INode> children, List<StaticStatement> staticStatements, IMetaDeclaration mb)
		{
			for(int k = 0; k < children.Count; k++)
			{
				var ch = children[k];
				if(ch.Location > mb.Location)
				{
					if(ch.EndLocation < mb.EndLocation || mb is AttributeMetaDeclarationSection)
					{
						ch.Accept(this);
						children.RemoveAt(k--);
					}
					else
						break;
				}
			}
			
			for(int k = 0; k < staticStatements.Count; k++)
			{
				var ss = staticStatements[k];
				if(ss.Location > mb.Location && (ss.EndLocation < mb.EndLocation || mb is AttributeMetaDeclarationSection))
				{
					ss.Accept(this);
					staticStatements.RemoveAt(k--);
				}
			}
		}
		
		#region Meta blocks
		public override void Visit(AttributeMetaDeclaration md)
		{
			base.Visit(md);
		}
		
		public override void Visit(MetaDeclarationBlock metaDeclarationBlock)
		{
			base.Visit(metaDeclarationBlock);
		}
		
		public override void Visit(AttributeMetaDeclarationBlock attributeMetaDeclarationBlock)
		{
			base.Visit(attributeMetaDeclarationBlock);
		}
		
		public override void Visit(AttributeMetaDeclarationSection attributeMetaDeclarationSection)
		{
			base.Visit(attributeMetaDeclarationSection);
		}
		
		public override void Visit(ElseMetaDeclaration elseMetaDeclaration)
		{
			base.Visit(elseMetaDeclaration);
		}
		
		public override void Visit(ElseMetaDeclarationBlock elseMetaDeclarationBlock)
		{
			base.Visit(elseMetaDeclarationBlock);
		}
		#endregion
		
		public override void Visit(DEnum n)
		{
			base.Visit(n);
		}
		
		public override void Visit(DMethod n)
		{
			// Format header
			FormatAttributedNode(n, !n.IsAnonymous && n.Type != null);
			
			VisitDNode(n);
			
			// Stick name to type
			if(n.Type != null)
				ForceSpacesBeforeRemoveNewLines(n.NameLocation, true);
			
			// Put parenthesis '(' token directly after the name
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
			
			// Format parameters
			
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
			
			// Visit body parts
			if (n.In != null)
				n.In.Accept (this);
			if (n.Body != null)
				n.Body.Accept (this);
			if (n.Out != null)
				n.Out.Accept (this);

			if (!n.IsAnonymous)
				EnsureBlankLinesAfter (n.EndLocation, policy.LinesAfterNode);
		}
		
		public override void Visit(DClassLike n)
		{
			base.Visit(n);
		}
		
		public override void Visit(DVariable n)
		{
			FormatAttributedNode(n, n.NameLocation != n.Location);
			
			if(n.Type != null)
				n.Type.Accept(this);
			
			if(!n.NameLocation.IsEmpty)
			{
				// Check if we're inside a multi-declaration like int a,b,c;	
				var lastNonWs = SearchWhitespaceStart(document.ToOffset(n.NameLocation)) - 1;
				if(lastNonWs > 0 && document[lastNonWs] == ','){
					switch(policy.MultiVariableDeclPlacement)
					{
						case NewLinePlacement.NewLine:
							curIndent.Push(IndentType.Continuation);
							FixIndentationForceNewLine(n.NameLocation);
							curIndent.Pop();
							break;
						case NewLinePlacement.SameLine:
							ForceSpacesBeforeRemoveNewLines(n.NameLocation, true);
							break;
					}
				}
				else{
					if(policy.ForceNodeNameOnSameLine)
						ForceSpacesBeforeRemoveNewLines(n.NameLocation, true);
				}
				
				if(n.Initializer != null)
				{
					// place '=' token
					ForceSpacesAfterRemoveLines(new CodeLocation(n.NameLocation.Column+n.Name.Length,n.NameLocation.Line),true);
					
					if(policy.ForceVarInitializerOnSameLine)
					{
						ForceSpacesBeforeRemoveNewLines(n.Initializer.Location,true);
						n.Initializer.Accept(this);
					}
					else
					{
						curIndent.Push(IndentType.Block);
						if (n.NameLocation.Line != n.Initializer.Location.Line) {
							FixStatementIndentation(n.Initializer.Location);
						}
						n.Initializer.Accept(this);
						curIndent.Pop ();
					}
				}
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
		void FormatAttributedNode(DNode n, bool fmtStartLocation = true)
		{
			bool firstAttr = fmtStartLocation;
			if(n.Attributes != null)
			foreach(var a in n.Attributes)
			{
				if(AlreadyHandledAttributes.Contains(a))
					continue;
				AlreadyHandledAttributes.Add(a);
				
				if(a is AtAttribute || firstAttr){
					FixIndentationForceNewLine(a.Location);
					firstAttr = false;
				}
			}
			
			if(fmtStartLocation)
				FixIndentationForceNewLine(n.Location);
		}
		
		void FormatParameters()
		{
			
		}
		#endregion
		
		#region Attributes

		#endregion
		
		#region Statements
		void EnforceBraceStyle(BlockStatement s)
		{
			EnforceBraceStyle(policy.TypeBlockBraces, s.Location, s.EndLocation.Line, s.EndLocation.Column - 1);
		}

		public override void Visit(BlockStatement s)
		{
			EnforceBraceStyle(s);
			
			curIndent.Push(IndentType.Block);
			base.Visit(s);
			curIndent.Pop();
		}
		
		public override void Visit(DeclarationStatement s)
		{
			base.Visit(s);
			//FixStatementIndentation(s.Location);
			FixSemicolon(s.EndLocation);
			
			
		}
		
		public override void Visit(ExpressionStatement s)
		{
			FixStatementIndentation(s.Location);
			FixSemicolon(s.EndLocation);
			
			base.Visit(s);
		}

		public override void Visit(SwitchStatement s)
		{
			FixStatementIndentation(s.Location);
			var bs = s.ScopedStatement as BlockStatement;
			if (bs == null)
				return;

			EnforceBraceStyle(bs);

			if (policy.IndentSwitchBody)
				curIndent.Push(IndentType.Block);

			foreach (var ss in bs.SubStatements)
			{
				ss.Accept(this);
			}

			if (policy.IndentSwitchBody)
				curIndent.Pop();
		}

		public override void Visit(SwitchStatement.CaseStatement s)
		{
			FixIndentation(s.Location);
			
			if (s.ArgumentList != null)
				s.ArgumentList.Accept(this);
			if (s.LastExpression != null)
				s.LastExpression.Accept(this);
			
			if (policy.IndentCases)
				curIndent.Push(IndentType.Block);

			VisitSubStatements(s);

			if (policy.IndentCases)
				curIndent.Pop();
		}

		public override void Visit(SwitchStatement.DefaultStatement s)
		{
			FixIndentation(s.Location);

			if (policy.IndentCases)
				curIndent.Push(IndentType.Block);

			VisitSubStatements(s);

			if (policy.IndentCases)
				curIndent.Pop();
		}

		public override void Visit(BreakStatement s)
		{
			FixStatementIndentation(s.Location);
			FixSemicolon(s.EndLocation);

			base.Visit(s);
		}

		public override void Visit(ContinueStatement s)
		{
			FixStatementIndentation(s.Location);
			FixSemicolon(s.EndLocation);

			base.Visit(s);
		}

		public override void Visit (ReturnStatement s)
		{
			base.Visit (s);
		}
		#endregion

		#region Expressions
		public override void Visit (FunctionLiteral x)
		{
			var pop = curIndent.Peek == IndentType.Block && curIndent.ExtraSpaces > 0;
			if (pop)
				curIndent.Push (IndentType.Negative);
			//FixIndentation (x.Location);
			base.Visit (x);
			if (pop)
				curIndent.Pop ();
		}
		#endregion
	}
}
