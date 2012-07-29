using System;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public class PrimitiveValue : ExpressionValue
	{
		public readonly int BaseTypeToken;

		/// <summary>
		/// To make math operations etc. more efficient, use the largest available structure to store scalar values.
		/// Also representing single characters etc.
		/// </summary>
		public readonly decimal Value;
		/// <summary>
		/// (For future use) For complex number handling, there's an extra value for storing the imaginary part of a number.
		/// </summary>
		public readonly decimal ImaginaryPart;

		public PrimitiveValue(bool Value, IExpression Expression)
			: this(DTokens.Bool, Value ? 1 : 0, Expression) { }

		public PrimitiveValue(int BaseTypeToken, decimal Value, IExpression Expression, decimal ImaginaryPart = 0)
			: base(ExpressionValueType.Primitive, new PrimitiveType(BaseTypeToken,0, Expression))
		{
			this.BaseTypeToken = BaseTypeToken;
			this.Value = Value;
			this.ImaginaryPart = ImaginaryPart;
		}

		/// <summary>
		/// NaN constructor
		/// </summary>
		private PrimitiveValue(int baseType,IExpression x)
			: base(ExpressionValueType.Primitive, new PrimitiveType(baseType, 0, x))
		{
			IsNaN = true;
		}

		public readonly bool IsNaN;

		public static PrimitiveValue CreateNaNValue(IExpression x, int baseType = DTokens.Float)
		{
			return new PrimitiveValue(baseType, x);
		}
	}

	public class VoidValue : PrimitiveValue
	{
		public VoidValue(IExpression x)
			: base(DTokens.Void, 0M, x)
		{ }
	}

	#region Derived data types
	public abstract class LValue : ExpressionValue
	{
		public LValue(AbstractType nodeType, IExpression baseExpression)
			: base(ExpressionValueType.None, nodeType, baseExpression) { }

		public abstract void Set(AbstractSymbolValueProvider vp, ISymbolValue value);
	}

	public class StaticVariableValue : VariableValue
	{
		public StaticVariableValue(DVariable artificialVariable, AbstractType propType, IExpression baseExpression)
			: base(artificialVariable, propType, baseExpression) { }
	}

	/// <summary>
	/// Contains a reference to a DVariable node.
	/// To get the actual value of the variable, use the value provider.
	/// </summary>
	public class VariableValue : LValue
	{
		public readonly DVariable Variable;

		public VariableValue(DVariable variable, AbstractType variableType, IExpression baseExpression)
			: base(variableType, baseExpression)
		{
			this.Variable = variable;
		}

		public override void Set(AbstractSymbolValueProvider vp, ISymbolValue value)
		{
			vp[Variable] = value;
		}
	}

	/// <summary>
	/// Used for accessing entries from an array.
	/// </summary>
	public class ArrayPointer : VariableValue
	{
		/// <summary>
		/// Used when accessing associative arrays
		/// </summary>
		public readonly ISymbolValue Key;
		/// <summary>
		/// Used when accessing normal arrays.
		/// If -1, a item passed to Set() will be added instead of replaced.
		/// </summary>
		public readonly int ItemNumber;

		public override void Set(AbstractSymbolValueProvider vp, ISymbolValue value)
		{
			var oldV = vp[Variable];

			if (oldV is AssociativeArrayValue)
			{
				if (Key != null)
				{
					var aa = (AssociativeArrayValue)oldV;
					
					int itemToReplace = -1;

					for(int i=0; i<aa.Elements.Count; i++)
						if (SymbolValueComparer.IsEqual(aa.Elements[i].Key, Key))
						{
							itemToReplace = i;
							break;
						}

					// If we haven't found a matching key, add it to the array
					var newElements = new KeyValuePair<ISymbolValue,ISymbolValue>[aa.Elements.Count + (itemToReplace==-1 ? 1:0)];
					aa.Elements.CopyTo(newElements, 0);

					if (itemToReplace != -1)
						newElements[itemToReplace] = new KeyValuePair<ISymbolValue, ISymbolValue>(newElements[itemToReplace].Key, value);
					else
						newElements[newElements.Length - 1] = new KeyValuePair<ISymbolValue, ISymbolValue>(Key, value);

					// Finally, make a new associative array containing the new elements
					vp[Variable] = new AssociativeArrayValue(aa.RepresentedType as AssocArrayType, aa.BaseExpression, newElements);
				}
				else
					throw new EvaluationException(BaseExpression, "Key expression must not be null", Key);
			}
			else if (oldV is ArrayValue)
			{
				var av = (ArrayValue)oldV;

				//TODO: Immutability checks

				if (av.IsString)
				{

				}
				else
				{

				}
			}
			else
				throw new EvaluationException(BaseExpression, "Type of accessed item must be an [associative] array", oldV);
		}

		/// <summary>
		/// Associative Array ctor
		/// </summary>
		public ArrayPointer(DVariable accessedArray, AssocArrayType arrayType, ISymbolValue accessedItemKey, IExpression baseExpression)
			: base(accessedArray, arrayType, baseExpression)
		{
			Key = accessedItemKey;
		}

		/// <summary>
		/// Array ctor.
		/// </summary>
		/// <param name="accessedArray"></param>
		/// <param name="arrayType"></param>
		/// <param name="accessedItem">0 - the array's length-1; -1 when adding the item is wished.</param>
		/// <param name="baseExpression"></param>
		public ArrayPointer(DVariable accessedArray, ArrayType arrayType, int accessedItem, IExpression baseExpression)
			: base(accessedArray, arrayType, baseExpression)
		{
			ItemNumber = accessedItem;
		}
	}

	/*public class PointerValue : ExpressionValue
	{

	}*/

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
		public ArrayValue(ArrayType stringLiteralResult, IdentifierExpression stringLiteral=null)
			: base(ExpressionValueType.Array, stringLiteralResult, stringLiteral)
		{
			if (stringLiteralResult.DeclarationOrExpressionBase is IdentifierExpression)
				StringValue = ((IdentifierExpression)stringLiteralResult.DeclarationOrExpressionBase).Value as string;
			else
				StringValue = stringLiteral.Value as string;
		}

		/// <summary>
		/// String constructor.
		/// Used for generating string results 'internally'.
		/// </summary>
		public ArrayValue(ArrayType stringTypeResult, IExpression baseExpression, string content)
			: base(ExpressionValueType.Array, stringTypeResult, baseExpression)
		{
			StringValue = content;
		}

		public ArrayValue(ArrayType resolvedArrayType, params ISymbolValue[] elements)
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

		public AssociativeArrayValue(AssocArrayType baseType, IExpression baseExpression,IList<KeyValuePair<ISymbolValue,ISymbolValue>> Elements)
			: base(ExpressionValueType.AssocArray, baseType, baseExpression)
		{
			this.Elements = new ReadOnlyCollection<KeyValuePair<ISymbolValue, ISymbolValue>>(Elements);
		}
	}

	/// <summary>
	/// Used for both delegates and function references.
	/// </summary>
	public class DelegateValue : ExpressionValue
	{
		public AbstractType Definition { get; private set; }
		public bool IsFunction { get { return base.Type == ExpressionValueType.Function; } }

		public DMethod Method
		{
			get
			{
				if (Definition is DelegateType)
				{
					var dg = (DelegateType)Definition;

					if (dg.IsFunctionLiteral)
						return ((FunctionLiteral)dg.DeclarationOrExpressionBase).AnonymousMethod;
				}
				return Definition is DSymbol ? ((DSymbol)Definition).Definition as DMethod : null;
			}
		}

		public DelegateValue(DelegateType Dg)
			: base(ExpressionValueType.Delegate, Dg)
		{
			this.Definition = Dg;
		}

		public DelegateValue(AbstractType Definition, AbstractType ReturnType, bool IsFunction = false)
			: base(IsFunction ? ExpressionValueType.Function : ExpressionValueType.Delegate, ReturnType, Definition.DeclarationOrExpressionBase as IExpression)
		{
			this.Definition = Definition;
		}
	}
	#endregion

	#region User data types

	/// <summary>
	/// Stores a type. Used e.g. as foreexpressions for PostfixExpressions.
	/// </summary>
	public class TypeValue : ExpressionValue
	{
		public TypeValue(AbstractType r, IExpression x)
			: base(ExpressionValueType.Type, r, x) { }
	}

	public abstract class ReferenceValue : ExpressionValue
	{
		INode referencedNode;

		public ReferenceValue(ExpressionValueType vt, AbstractType type, IExpression x)
			: base(vt, type, x)
		{
		}
	}
	/*
	public class AliasValue : ExpressionValue
	{
		public AliasValue(IExpression x, MemberResult AliasResult)
			: base(ExpressionValueType.Alias, AliasResult, x) { }
	}

	public class InstanceValue : ExpressionValue
	{
		
	}

	public class StructInstanceValue : ExpressionValue
	{

	}

	public class UnionInstanceValue : ExpressionValue
	{

	}

	public class EnumInstanceValue : ExpressionValue
	{

	}

	public class ClassInstanceValue : ExpressionValue
	{

	}
	*/

	public class NullValue : ReferenceValue
	{
		public NullValue(IExpression x) : base(ExpressionValueType.Class, null, x) { }
	}
	#endregion

	/// <summary>
	/// Used when passing several function overloads from the inner evaluation function to the outer (i.e. method call) one.
	/// Not intended to be used in any other kind.
	/// </summary>
	public class InternalOverloadValue : ExpressionValue
	{
		public AbstractType[] Overloads { get; private set; }

		public InternalOverloadValue(AbstractType[] overloads, IExpression x)
			: base(ExpressionValueType.None, null, x)
		{
			this.Overloads = overloads;
		}
	}
}
