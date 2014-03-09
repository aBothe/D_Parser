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
	}
}

