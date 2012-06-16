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

	public class VoidValue : PrimitiveValue
	{
		public VoidValue(IExpression x)
			: base(DTokens.Void, null, x)
		{ }
	}

	#region Derived data types
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
		public ArrayValue(ResolveResult stringLiteralResult, IdentifierExpression stringLiteral=null)
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
		public ArrayValue(ResolveResult stringTypeResult, IExpression baseExpression, string content)
			: base(ExpressionValueType.Array, stringTypeResult, baseExpression)
		{
			StringValue = content;
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

	/// <summary>
	/// Used for both delegates and function references.
	/// </summary>
	public class DelegateValue : ExpressionValue
	{
		public ResolveResult Definition { get; private set; }
		public bool IsFunction { get { return base.Type == ExpressionValueType.Function; } }

		public DMethod Method
		{
			get
			{
				return Definition is TemplateInstanceResult ? ((TemplateInstanceResult)Definition).Node as DMethod : null;
			}
		}

		public DelegateValue(DelegateResult Dg)
			: base(ExpressionValueType.Delegate, Dg)
		{
			this.Definition = Dg;
		}

		public DelegateValue(ResolveResult Definition, ResolveResult ReturnType, bool IsFunction = false)
			: base(IsFunction ? ExpressionValueType.Function : ExpressionValueType.Delegate, ReturnType, Definition.DeclarationOrExpressionBase as IExpression)
		{
			this.Definition = Definition;
		}
	}
	#endregion

	#region User data types
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

	public class NullValue : ExpressionValue
	{
		public NullValue(IExpression x) : base(ExpressionValueType.Class, null, x) { }
	}
	#endregion
}
