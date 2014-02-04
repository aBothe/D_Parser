using System;
using System.Collections.Generic;

namespace D_Parser.Dom.Statements
{
	/// <summary>
	/// Represents a statement that can contain other statements, which may become scoped.
	/// </summary>
	public abstract class StatementContainingStatement : AbstractStatement
	{
		public virtual IStatement ScopedStatement { get; set; }

		public virtual IEnumerable<IStatement> SubStatements { get { return ScopedStatement != null ? new[] { ScopedStatement } : new IStatement[0]; } }

		public IStatement SearchStatement(CodeLocation Where)
		{
			// First check if one sub-statement is located at the code location
			var ss = SubStatements;

			if (ss != null)
				foreach (var s in ss)
					if (s != null && Where >= s.Location && Where <= s.EndLocation)
						return s;

			// If nothing was found, check if this statement fits to the coordinates
			if (Where >= Location && Where <= EndLocation)
				return this;

			// If not, return null
			return null;
		}

		/// <summary>
		/// Scans the current scope. If a scoping statement was found, also these ones get searched then recursively.
		/// </summary>
		/// <param name="Where"></param>
		public IStatement SearchStatementDeeply(CodeLocation Where)
		{
			var lastS = this;
			IStatement ret = null;

			while (lastS != null && (ret = lastS.SearchStatement(Where)) != lastS)
				lastS = ret as StatementContainingStatement;

			return ret;
		}
	}
}

