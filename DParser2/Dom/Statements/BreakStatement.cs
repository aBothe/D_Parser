using System;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public class BreakStatement : AbstractStatement,IExpressionContainingStatement
	{
		public int IdentifierHash;

		public string Identifier
		{
			get{ return Strings.TryGet(IdentifierHash); } 
			set
			{
				Strings.Add(value);
				IdentifierHash = value != null ? value.GetHashCode() : 0;
			}
		}

		public override string ToCode()
		{
			return "break" + (string.IsNullOrEmpty(Identifier) ? "" : (' ' + Identifier)) + ';';
		}

		public IExpression[] SubExpressions
		{
			get { return string.IsNullOrEmpty(Identifier) ? null : new[] { new IdentifierExpression(Identifier) }; }
		}

		public override void Accept(IStatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
}

