using System;
using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver
{
	public static class ConditionalCompilation
	{
		public class ConditionSet
		{
			public readonly ConditionalCompilationFlags GlobalFlags;
			public ConditionalCompilationFlags LocalFlags;
			List<DeclarationCondition> conditionsBeingChecked = new List<DeclarationCondition>();

			public ConditionSet(ConditionalCompilationFlags gFLags, ConditionalCompilationFlags lFlags = null)
			{
				// Make a default global environment for test resolutions etc.
				GlobalFlags = gFLags ?? new ConditionalCompilationFlags(null,0,false);
				LocalFlags = lFlags;
			}

			public bool IsMatching(IEnumerable<DAttribute> conditions, ResolutionContext ctxt)
			{
				if (conditions != null)
					foreach (var c in conditions)
						if (c is DeclarationCondition)
							if(!IsMatching((DeclarationCondition)c,ctxt))
							   return false;
				return true;
			}
			
			public bool IsMatching(DeclarationCondition dc, ResolutionContext ctxt)
			{
				if (ctxt.CancelOperation)
					return true;
				var r = true;

				if(dc is NegatedDeclarationCondition)
				{
					var ng = (NegatedDeclarationCondition)dc;
					if(ng.FirstCondition is StaticIfCondition){
						if(!conditionsBeingChecked.Contains(ng.FirstCondition))
							conditionsBeingChecked.Add(ng.FirstCondition);
						else
							return false;
						
						r = !GlobalFlags.IsMatching((StaticIfCondition)ng.FirstCondition, ctxt);
						
						conditionsBeingChecked.Remove(ng.FirstCondition);
					}
					else
						r = ((GlobalFlags == null || GlobalFlags.IsMatching(dc,ctxt)) && 
							(LocalFlags == null || LocalFlags.IsMatching(dc,ctxt)));
				}
				else {
					if(dc is StaticIfCondition){
						if(!conditionsBeingChecked.Contains(dc))
							conditionsBeingChecked.Add(dc);
						else
							return false;
						
						r = GlobalFlags.IsMatching((StaticIfCondition)dc, ctxt);
						
						conditionsBeingChecked.Remove(dc);
					}
					else
						r = (GlobalFlags.IsMatching(dc,ctxt) || LocalFlags.IsMatching(dc,ctxt));
				}
				return r;
			}
		}

		class ConditionVisitor : DefaultDepthFirstVisitor
		{
			public readonly ConditionSet cs;
			public readonly MutableConditionFlagSet l;
			public readonly ResolutionContext ctxt;
			public readonly CodeLocation caret;

			public ConditionVisitor(ConditionSet cs,ResolutionContext ctxt, CodeLocation caret)
			{
				this.caret = caret;
				this.cs = cs;
				this.ctxt = ctxt;
				this.l = new MutableConditionFlagSet();
				cs.LocalFlags = l;
			}

			public override void VisitBlock(DBlockNode block)
			{
				VisitChildren(block);
				VisitDNode(block);

				if (block.StaticStatements.Count != 0)
					foreach (var s in block.StaticStatements)
					{
						if (s.Location > caret)
							break;

						s.Accept(this);
					}

				if (block.MetaBlocks.Count != 0)
					foreach (var mb in block.MetaBlocks)
					{
						if (mb.Location > caret)
							break;
						mb.Accept(this);
					}
			}

			public override void VisitAttributeMetaDeclaration(AttributeMetaDeclaration amd)
			{
				if (amd == null || amd.AttributeOrCondition == null || amd.AttributeOrCondition.Length == 0)
					return;

				if (caret > amd.Location && (!(amd is AttributeMetaDeclarationBlock) || amd.EndLocation > caret))
				{
					foreach (var attr in amd.AttributeOrCondition)
						if (attr is DeclarationCondition)
							l.Add((DeclarationCondition)attr);
				}
				else if(amd.OptionalElseBlock != null && 
					caret > amd.OptionalElseBlock.Location && amd.OptionalElseBlock.EndLocation > caret)
				{
					foreach (var attr in amd.AttributeOrCondition)
						if (attr is DeclarationCondition)
							l.Add(new NegatedDeclarationCondition((DeclarationCondition)attr));
				}
			}

			bool ignoreBounds = false;
			public override void Visit(StatementCondition s)
			{
				if (s.Location > caret)
					return;

				// If the caret is inside this condition block, assume its condition as fulfilled
				if (s.EndLocation > caret)
				{
					if (s.Condition != null)
						l.Add(s.Condition);
				}
				else // otherwise check before walking through its child statements
				{
					//FIXME: Don't check static if's for now..too slow
					if (!(s.Condition is StaticIfCondition) && !cs.IsMatching(s.Condition, ctxt))
						return; // and break if e.g. the version is not matching
				}

				ignoreBounds = s.ScopedStatement is BlockStatement;

				if (s.ScopedStatement != null)
					s.ScopedStatement.Accept(this);

				//TODO: ElseBlock
			}

			public override void Visit(BlockStatement s)
			{
				if (ignoreBounds || (caret >= s.Location && caret <= s.EndLocation))
				{
					ignoreBounds = false;
					base.Visit(s);
				}
			}

			
			public override void VisitAttribute(DebugCondition debugCondition)
			{
				base.VisitAttribute(debugCondition);
			}

			public override void VisitChildren(IBlockNode block)
			{
				var ch = DResolver.SearchRegionAt<INode>(block.Children, caret);
				if (ch != null && ch != block)
					ch.Accept(this);
			}

			public override void VisitDNode(DNode n)
			{
				if (n.Attributes != null && caret >= n.Location && caret <= n.EndLocation)
				{
					foreach (var attr in n.Attributes)
						if (attr is DeclarationCondition)
							l.Add(((DeclarationCondition)attr));
				}
			}

			public override void Visit(DMethod n)
			{
				base.Visit(n);
				VisitChildren(n);
			}

			public override void VisitAttribute(NegatedDeclarationCondition a)
			{
				
			}

			public override void VisitAttribute(StaticIfCondition a)
			{
				
			}

			public override void VisitAttribute(VersionCondition vis)
			{
				
			}

			public override void Visit(VersionSpecification vs)
			{
				if (_checkForMatchinSpecConditions(vs))
					l.AddVersionCondition (vs);
			}

			public override void Visit(DebugSpecification ds)
			{
				if (_checkForMatchinSpecConditions (ds))
					l.AddDebugCondition (ds);
			}

			bool _checkForMatchinSpecConditions(StaticStatement ss)
			{
				return ss.Location < caret && (ss.Attributes == null || cs.IsMatching(ss.Attributes, ctxt));
			}

			public override void VisitChildren(Dom.Expressions.ContainerExpression x){}
			public override void Visit(ExpressionStatement s){}
			public override void VisitOpBasedExpression(Dom.Expressions.OperatorBasedExpression ox){}
			public override void VisitPostfixExpression(Dom.Expressions.PostfixExpression x){}
		}

		public static void EnumConditions(ConditionSet cs,IBlockNode block, ResolutionContext ctxt, CodeLocation caret)
		{
			if(block != null && block.NodeRoot != null)
				block.NodeRoot.Accept(new ConditionVisitor(cs, ctxt, caret));
			return;
		}
	}
}
