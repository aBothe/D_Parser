using System;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public class ContinueStatement : AbstractStatement, IExpressionContainingStatement
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
			return "continue" + (string.IsNullOrEmpty(Identifier) ? "" : (' ' + Identifier)) + ';';
		}

		public IExpression[] SubExpressions
		{
			get { return IdentifierHash == 0 ? null : new[]{ new IdentifierExpression(Identifier) }; }
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
}

