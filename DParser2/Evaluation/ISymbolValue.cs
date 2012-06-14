using System;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver;

namespace D_Parser.Evaluation
{
	public interface ISymbolValue : IEquatable<ISymbolValue>
	{
		ExpressionValueType Type { get; }

		ResolveResult RepresentedType { get; }
		IExpression BaseExpression { get; }
	}

	public abstract class ExpressionValue : ISymbolValue
	{
		IExpression _baseExpression;

		public ExpressionValue(ExpressionValueType Type,
			ResolveResult RepresentedType)
		{
			this.Type = Type;
			this.RepresentedType = RepresentedType;
		}

		public ExpressionValue(ExpressionValueType Type,
			ResolveResult RepresentedType,
			IExpression BaseExpression) : this(Type, RepresentedType)
		{
			this._baseExpression = BaseExpression;
		}

		public ExpressionValueType Type
		{
			get;
			private set;
		}

		public ResolveResult RepresentedType
		{
			get;
			private set;
		}

		public IExpression BaseExpression
		{
			get { return _baseExpression!=null || RepresentedType == null ? _baseExpression : RepresentedType.DeclarationOrExpressionBase as IExpression; }
			private set {
				_baseExpression = value;
			}
		}

		public virtual bool Equals(ISymbolValue other)
		{
			return SymbolValueComparer.IsEqual(this, other);
		}
	}

	public enum ExpressionValueType
	{
		/// <summary>
		/// Represents all Basic Data Types
		/// </summary>
		Primitive,

		// Derived Data Types
		Pointer,
		Array,
		AssocArray,
		Function,
		Delegate,

		// User data types
		Alias,
		Enum,
		Struct,
		Union,
		/// <summary>
		/// The expression returns a class instance
		/// </summary>
		Class
	}
}
