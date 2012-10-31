using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Resolver
{
	public class ConditionalCompilation
	{
		public static List<DeclarationCondition> EnumConditions(IStatement stmt, IBlockNode block)
		{
			var l = new List<DeclarationCondition>();
			asdasd
			while (stmt != null)
			{
				if (stmt is StatementCondition)
					l.Add(((StatementCondition)stmt).Condition);
				stmt = stmt.Parent;
			}

			var until = CodeLocation.Empty;
			while (block != null)
			{
				if (block is DModule)
				{
					GetDoneVersionDebugSpecs(l, (DModule)block, until);
				}
				else if (block is DNode)
				{
					foreach (var attr in ((DNode)block).Attributes)
						if (attr is DeclarationCondition)
							l.Add(((DeclarationCondition)attr));
				}
				
				if(block.Parent is DModule)
					until = block.Location;
				block = block.Parent as IBlockNode;
			}sads

			asdasd

			return l;
		}

		static void GetDoneVersionDebugSpecs(List<DeclarationCondition> l, DModule m, CodeLocation until)
		{
			//TODO
		}

		public static bool IsMatching(IDeclarationCondition cond, ResolutionContext ctxt)
		{
			return true;
		}
	}
}
