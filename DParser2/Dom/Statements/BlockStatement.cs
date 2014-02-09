using System;
using System.Collections.Generic;

namespace D_Parser.Dom.Statements
{
	public class BlockStatement : StatementContainingStatement, IEnumerable<IStatement>
	{
		internal readonly List<IStatement> _Statements = new List<IStatement>();

		public IEnumerator<IStatement>  GetEnumerator()
		{
			return _Statements.GetEnumerator();
		}

		System.Collections.IEnumerator  System.Collections.IEnumerable.GetEnumerator()
		{
			return _Statements.GetEnumerator();
		}

		public override string ToCode()
		{
			var ret = "{" + Environment.NewLine;

			foreach (var s in _Statements)
				ret += s.ToCode() + Environment.NewLine;

			return ret + "}";
		}

		public void Add(IStatement s)
		{
			if (s == null)
				return;
			s.Parent = this;
			_Statements.Add(s);
		}

		public override IEnumerable<IStatement> SubStatements
		{
			get
			{
				return _Statements;
			}
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public override string ToString()
		{
			return "<block> " + base.ToString();
		}
	}
}

