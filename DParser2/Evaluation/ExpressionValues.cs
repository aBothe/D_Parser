using System;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver;

namespace D_Parser.Evaluation
{
	public class PrimitiveValue : ExpressionValue
	{
		public readonly int BaseTypeToken;

		public PrimitiveValue(int BaseTypeToken, object Value, IExpression Expression)
			: base(ExpressionValueType.Primitive, new StaticTypeResult{ BaseTypeToken=BaseTypeToken, DeclarationOrExpressionBase=Expression }, Value)
		{
			this.BaseTypeToken = BaseTypeToken;
		}

		/// <summary>
		/// Returns true if the represented value is either null (ref type), 0 (int/float), false (bool) or empty (string)
		/// </summary>
		public bool IsNullFalseOrEmpty
		{
			get {
				if (Value == null)
					return true;

				try
				{
					switch (BaseTypeToken)
					{
						case DTokens.Bool:
							return !Convert.ToBoolean(Value);
						case DTokens.Char:
							var c = Convert.ToChar(Value);

							return c == '\0';
					}
				}
				catch {}
				return false;
			}
		}
	}

	public class InstanceReference //: ExpressionValue
	{

	}
}
