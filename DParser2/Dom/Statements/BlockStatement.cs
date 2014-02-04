using System;
using System.Collections.Generic;

namespace D_Parser.Dom.Statements
{
	public class BlockStatement : StatementContainingStatement, IEnumerable<IStatement>, IDeclarationContainingStatement
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

		public static List<INode> GetDeclarations(IEnumerable<IStatement> stmts)
		{
			var l = new List<INode>();

			foreach (var s in stmts)
				if (s is BlockStatement ||
					s is DeclarationStatement ||
					s is ImportStatement)
				{
					var decls = (s as IDeclarationContainingStatement).Declarations;
					if (decls != null && decls.Length > 0)
						l.AddRange(decls);
				}

			return l;
		}

		/// <summary>
		/// Returns all child nodes inside the current scope.
		/// Includes nodes from direct block statement substatements and alias nodes from import statements.
		/// Condition
		/// </summary>
		public INode[] Declarations
		{
			get
			{
				return GetDeclarations(_Statements).ToArray();
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

