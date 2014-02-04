using System;

namespace D_Parser.Dom.Expressions
{
	/// <summary>
	/// Expressions that contain other sub-expressions somewhere share this interface
	/// </summary>
	public interface ContainerExpression : IExpression
	{
		IExpression[] SubExpressions { get; }
	}
}

