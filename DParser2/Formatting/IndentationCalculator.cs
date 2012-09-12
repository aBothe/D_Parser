using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Expressions;

namespace D_Parser.Formatting
{
	public class IndentationCalculator
	{
		public static int Calculate(IAbstractSyntaxTree ast, CodeLocation caret)
		{
			IStatement stmt;
			return Calculate(ast, DResolver.SearchBlockAt(ast, caret, out stmt), stmt, caret);
		}

		/*
		 * Iterate from inner to outer
		 */

		public static int Calculate(IAbstractSyntaxTree ast, IBlockNode currentBlock, IStatement currentStatement, CodeLocation caret)
		{
			int i = 0;

			if (currentStatement != null)
			{

			}


			return 0;
		}

		static int CalculateBackward(IBlockNode n, CodeLocation caret)
		{
			int i = 0;

			while (n != null)
			{
				if (n.Parent != null)
					i++;

				var db = n as DBlockNode;
				if(db!=null)
					i += db.GetMetaBlockStack(caret, true).Length;

				n = n.Parent as IBlockNode;
			}

			return i;
		}

		static int Calculate(IExpression x, CodeLocation caret)
		{

		}
	}
}
