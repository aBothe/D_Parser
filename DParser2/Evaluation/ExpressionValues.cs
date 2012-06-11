using System;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace D_Parser.Evaluation
{
	public class PrimitiveValue : ExpressionValue
	{
		public readonly int BaseTypeToken;

		public object Value
		{
			get;
			private set;
		}

		/// <summary>
		/// Returns true if the represented value is either null (ref type), 0 (int/float), false (bool) or empty (string)
		/// </summary>
		public bool IsNullFalseOrEmpty
		{
			get
			{
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
				catch { }
				return false;
			}
		}

		public PrimitiveValue(int BaseTypeToken, object Value, IExpression Expression)
			: base(ExpressionValueType.Primitive, new StaticTypeResult{ BaseTypeToken=BaseTypeToken, DeclarationOrExpressionBase=Expression })
		{
			this.BaseTypeToken = BaseTypeToken;
			this.Value = Value;
		}
	}

	public class ArrayValue : ExpressionValue
	{
		#region Properties
		public bool IsString { get { return StringValue != null; } }

		/// <summary>
		/// If this represents a string, the string will be returned. Otherwise null.
		/// </summary>
		public string StringValue { get; private set; }

		/// <summary>
		/// If not a string, the evaluated elements will be returned. Otherwise null.
		/// </summary>
		public ISymbolValue[] Elements
		{
			get;// { return elements != null ? elements.ToArray() : null; }
			private set;
		}
		#endregion

		#region Ctor
		/// <summary>
		/// String constructor.
		/// Given result stores both type and idenfitierexpression whose Value is used as content
		/// </summary>
		public ArrayValue(ResolveResult stringLiteralResult, IdentifierExpression stringLiteral=null)
			: base(ExpressionValueType.Array, stringLiteralResult, stringLiteral)
		{
			if (stringLiteralResult.DeclarationOrExpressionBase is IdentifierExpression)
				StringValue = ((IdentifierExpression)stringLiteralResult.DeclarationOrExpressionBase).Value as string;
			else
				StringValue = stringLiteral.Value as string;
		}

		public ArrayValue(ResolveResult resolvedArrayType, params ISymbolValue[] elements)
			: base(ExpressionValueType.Array, resolvedArrayType)
		{
			Elements = elements;
		}
		#endregion
	}

	public class AssociativeArrayValue : ExpressionValue
	{
		public ReadOnlyCollection<KeyValuePair<ISymbolValue, ISymbolValue>> Elements
		{
			get;
			private set;
		}

		public AssociativeArrayValue(ResolveResult baseType, IExpression baseExpression,IList<KeyValuePair<ISymbolValue,ISymbolValue>> Elements)
			: base(ExpressionValueType.AssocArray, baseType, baseExpression)
		{
			this.Elements = new ReadOnlyCollection<KeyValuePair<ISymbolValue, ISymbolValue>>(Elements);
		}
	}
}
