using System;

namespace D_Parser.Dom.Expressions
{
	public abstract class AbstractVariableInitializer : IVariableInitializer,IExpression
	{
		public const int AbstractInitializerHash = 1234;

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public abstract void Accept(ExpressionVisitor vis);

		public abstract R Accept<R>(ExpressionVisitor<R> vis);
	}
}

