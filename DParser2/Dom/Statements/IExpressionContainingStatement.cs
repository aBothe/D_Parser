using System;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public interface IExpressionContainingStatement : IStatement
	{
		IExpression[] SubExpressions { get; }
	}
}

