using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Resolver;

namespace D_Parser.Evaluation
{
	public interface IExpressionValue
	{
		ExpressionValueType Type { get; }
		ResolveResult RepresentedType { get; }
		object Value { get; }
		IExpression BaseExpression { get; }
	}

	public abstract class ExpressionValue : IExpressionValue
	{
		IExpression _baseExpression;

		public ExpressionValue(ExpressionValueType Type,
			ResolveResult RepresentedType,
			object Value)
		{
			this.Type = Type;
			this.RepresentedType = RepresentedType;
			this.Value = Value;
		}

		public ExpressionValue(ExpressionValueType Type,
			ResolveResult RepresentedType,
			object Value,
			IExpression BaseExpression) : this(Type, RepresentedType, Value)
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

		public object Value
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
