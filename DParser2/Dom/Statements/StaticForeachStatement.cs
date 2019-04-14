using D_Parser.Dom.Expressions;
using System;

namespace D_Parser.Dom.Statements
{
    public class StaticForeachStatement : ForeachStatement, StaticStatement
    {
        public DAttribute[] Attributes { get; set; }

        public bool inSemanticAnalysis = false;

        public override string ToCode()
        {
            return "static " + base.ToCode();
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

