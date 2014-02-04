using System;
using D_Parser.Parser;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public class GotoStatement : AbstractStatement, IExpressionContainingStatement
	{
		public enum GotoStmtType : byte
		{
			Identifier = DTokens.Identifier,
			Case = DTokens.Case,
			Default = DTokens.Default
		}

		public int LabelIdentifierHash;

		public string LabelIdentifier
		{
			get{ return Strings.TryGet(LabelIdentifierHash); } 
			set
			{
				Strings.Add(value);
				LabelIdentifierHash = value != null ? value.GetHashCode() : 0;
			}
		}

		public IExpression CaseExpression;
		public GotoStmtType StmtType = GotoStmtType.Identifier;

		public override string ToCode()
		{
			switch (StmtType)
			{
				case GotoStmtType.Identifier:
					return "goto " + LabelIdentifier + ';';
				case GotoStmtType.Default:
					return "goto default;";
				case GotoStmtType.Case:
					return "goto" + (CaseExpression == null ? "" : (' ' + CaseExpression.ToString())) + ';';
			}

			return null;
		}

		public IExpression[] SubExpressions
		{
			get { return CaseExpression != null ? new[] { CaseExpression } : null; }
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

