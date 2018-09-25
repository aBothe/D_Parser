using System;
using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Resolver.TypeResolution
{
	public static class ASTSearchHelper
	{
		/// <summary>
		/// Binary search implementation for ordered syntax region (derivative) lists. 
		/// </summary>
		public static SR SearchRegionAt<SR>(Func<int, SR> childGetter, int childCount, CodeLocation Where) where SR : ISyntaxRegion
		{
			int start = 0;
			SR midElement = default(SR);
			int midIndex = 0;
			int len = childCount;

			while (len > 0)
			{
				midIndex = (len % 2 + len) / 2;

				// Take an element from the middle
				if ((midElement = childGetter(start + midIndex - 1)) == null)
					break;

				// If 'Where' is beyond its start location
				if (Where >= midElement.Location)
				{
					start += midIndex;

					// If we've reached the (temporary) goal, break immediately
					if (Where <= midElement.EndLocation)
						break;
					// If it's the last tested element and if the caret is beyond the end location, 
					// return the Parent instead the last tested child
					else if (midIndex == len)
					{
						midElement = default(SR);
						break;
					}
				}
				else if (midIndex == len)
				{
					midElement = default(SR);
					break;
				}

				len -= midIndex;
			}

			return midElement;
		}

		public static SR SearchRegionAt<SR>(IList<SR> children, CodeLocation Where) where SR : ISyntaxRegion
		{
			int start = 0;
			SR midElement = default(SR);
			int midIndex = 0;
			int len = children.Count;

			while (len > 0)
			{
				midIndex = (len % 2 + len) / 2;

				// Take an element from the middle
				if ((midElement = children[start + midIndex - 1]) == null)
					break;

				// If 'Where' is beyond its start location
				if (Where > midElement.Location)
				{
					start += midIndex;

					// If we've reached the (temporary) goal, break immediately
					if (Where < midElement.EndLocation)
						break;
					// If it's the last tested element and if the caret is beyond the end location, 
					// return the Parent instead the last tested child
					else if (midIndex == len)
					{
						midElement = default(SR);
						break;
					}
				}
				else if (midIndex == len)
				{
					midElement = default(SR);
					break;
				}

				len -= midIndex;
			}

			return midElement;
		}

		public static IBlockNode SearchBlockAt(IBlockNode Parent, CodeLocation Where)
		{
			if (Parent == null)
				return null;

			var pCount = Parent.Count;
			while (pCount != 0)
			{
				var midElement = SearchRegionAt<INode>(Parent.Children, Where);

				if (midElement is IBlockNode)
				{
					Parent = (IBlockNode)midElement;
					pCount = Parent.Count;
				}
				else
					break;
			}

			var dm = Parent as DMethod;
			if (dm != null)
			{
				// Do an extra re-scan for anonymous methods etc.
				var subItem = SearchRegionAt<INode>(dm.Children, Where) as IBlockNode;
				if (subItem != null)
					return SearchBlockAt(subItem, Where); // For e.g. nested nested methods inside anonymous class declarations that occur furtherly inside a method.
			}

			return Parent;
		}

		public static IStatement SearchStatementDeeplyAt(IBlockNode block, CodeLocation Where)
		{
			var dm = block as DMethod;
			if (dm != null)
				return SearchStatementDeeplyAt(dm.GetSubBlockAt(Where), Where);

			var db = block as DBlockNode;
			if (db != null && db.StaticStatements.Count != 0)
				return SearchRegionAt<IStatement>(new List<IStatement>(db.StaticStatements), Where);

			return null;
		}

		public static IStatement SearchStatementDeeplyAt(IStatement stmt, CodeLocation Where)
		{
			while (stmt != null)
			{
				var ss = stmt as StatementContainingStatement;
				if (ss != null)
				{
					var subst = ss.SubStatements;
					if (subst != null)
					{
						stmt = SearchRegionAt<IStatement>(subst as IList<IStatement> ?? new List<IStatement>(subst), Where);
						if (stmt == null || stmt == ss)
							return ss;
						continue;
					}
				}

				break;
			}

			return stmt;
		}

		public static IBlockNode SearchClassLikeAt(IBlockNode Parent, CodeLocation Where)
		{
			if (Parent != null && Parent.Count > 0)
				foreach (var n in Parent)
				{
					var dc = n as DClassLike;
					if (dc == null)
						continue;

					if (Where > dc.BlockStartLocation && Where < dc.EndLocation)
						return SearchClassLikeAt(dc, Where);
				}

			return Parent;
		}

	}
}
