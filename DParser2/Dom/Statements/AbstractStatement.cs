using System;

namespace D_Parser.Dom.Statements
{
	public abstract class AbstractStatement:IStatement
	{
		public virtual CodeLocation Location { get; set; }

		public virtual CodeLocation EndLocation { get; set; }

		readonly WeakReference parentStmt = new WeakReference(null);
		readonly WeakReference parentNode = new WeakReference(null);

		public IStatement Parent
		{
			get
			{
				return parentStmt.Target as IStatement;
			}
			set
			{
				parentStmt.Target = value;
			}
		}

		public INode ParentNode
		{
			get
			{
				var n = parentNode.Target as INode;
				if (n != null)
					return n;
				var t = parentStmt.Target as IStatement;
				if (t != null)
					return t.ParentNode;
				return null;
			}
			set
			{
				var ps = parentStmt.Target as IStatement;
				if (ps != null)
					ps.ParentNode = value;
				else
					parentNode.Target = value;
			}
		}

		public abstract string ToCode();

		public override string ToString()
		{
			return ToCode();
		}

		public abstract void Accept(IStatementVisitor vis);

		public abstract R Accept<R>(StatementVisitor<R> vis);
	}
}

