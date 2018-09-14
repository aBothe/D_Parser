using System;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public class PrimitiveValue : ExpressionValue
	{
		public readonly byte BaseTypeToken;
		public readonly byte[] Modifiers;

		/// <summary>
		/// To make math operations etc. more efficient, use the largest available structure to store scalar values.
		/// Also representing single characters etc.
		/// </summary>
		public readonly decimal Value;
		/// <summary>
		/// (For future use) For complex number handling, there's an extra value for storing the imaginary part of a number.
		/// </summary>
		public readonly decimal ImaginaryPart;

		public PrimitiveValue(bool Value)
			: this(DTokens.Bool, Value ? 1 : 0) {
		}

		public PrimitiveValue(int Value)
			: this (DTokens.Int, (decimal)Value) {
		}

		public PrimitiveValue(decimal Value, PrimitiveType pt, decimal ImaginaryPart = 0M)
			: base(pt) {
			this.Modifiers = pt.Modifiers;
			this.BaseTypeToken = pt.TypeToken;
			this.Value = Value;
			this.ImaginaryPart = ImaginaryPart;
		}

		public PrimitiveValue (byte BaseTypeToken, decimal Value, decimal ImaginaryPart = 0M, params byte [] BaseTypeModifier)
			: this (Value, new PrimitiveType (BaseTypeToken, BaseTypeModifier), ImaginaryPart)
		{ }

		/// <summary>
		/// NaN constructor
		/// </summary>
		PrimitiveValue(byte baseType, params byte[] mod)
			: base(new PrimitiveType(baseType, mod))
		{
			Modifiers = mod;
			IsNaN = true;
		}

		public readonly bool IsNaN;

		public static PrimitiveValue CreateNaNValue(byte baseType = DTokens.Float, params byte[] baseTypeMod)
		{
			return new PrimitiveValue(baseType, baseTypeMod);
		}

		public override string ToCode()
		{
			switch (BaseTypeToken)
			{
				case DTokens.Void:
					return "void";
				case DTokens.Bool:
					return Value == 1M ? "true" : "false";
				case DTokens.Char:
				case DTokens.Wchar:
				case DTokens.Dchar:
					return Char.ConvertFromUtf32((int)Value);
			}

			return Value.ToString() + (ImaginaryPart == 0 ? "" : ("+"+ImaginaryPart.ToString()+"i"));
		}

		public override void Accept(ISymbolValueVisitor vis)
		{
			vis.VisitPrimitiveValue(this);
		}
		public override R Accept<R>(ISymbolValueVisitor<R> vis)
		{
			return vis.VisitPrimitiveValue(this);
		}
	}

	public class VoidValue : PrimitiveValue
	{
		public VoidValue()
			: base(DTokens.Void, 0M)
		{ }

		public override void Accept(ISymbolValueVisitor vis)
		{
			vis.VisitVoidValue(this);
		}
		public override R Accept<R>(ISymbolValueVisitor<R> vis)
		{
			return vis.VisitVoidValue(this);
		}
	}

	#region Derived data types
	public class ArrayValue : ExpressionValue
	{
		#region Properties
		public bool IsString { get { return StringValue != null; } }

		/// <summary>
		/// If this represents a string, the string will be returned. Otherwise null.
		/// </summary>
		public string StringValue { get; private set; }
		public readonly LiteralSubformat StringFormat;

		/// <summary>
		/// If not a string, the evaluated elements will be returned. Otherwise null.
		/// </summary>
		public ISymbolValue[] Elements
		{
			get;// { return elements != null ? elements.ToArray() : null; }
			private set;
		}

		public int Length
		{
			get { return StringValue != null ? StringValue.Length : Elements != null ? Elements.Length : 0; }
		}
		#endregion

		#region Ctor
		public ArrayValue(IdentifierExpression stringLiteral, ResolutionContext ctxtToResolveStringType = null)
			: base(Evaluation.GetStringLiteralType(ctxtToResolveStringType, stringLiteral.Subformat))
		{
			StringValue = stringLiteral.StringValue;
			StringFormat = stringLiteral.Subformat;
		}

		/// <summary>
		/// String constructor.
		/// Given result stores both type and idenfitierexpression whose Value is used as content
		/// </summary>
		public ArrayValue(ArrayType stringLiteralResult, IdentifierExpression stringLiteral)
			: base(stringLiteralResult)
		{
			StringValue = stringLiteral.StringValue;
			StringFormat = stringLiteral.Subformat;
		}

		/// <summary>
		/// String constructor.
		/// Used for generating string results 'internally'.
		/// </summary>
		public ArrayValue(ArrayType stringTypeResult, string content) : base(stringTypeResult)
		{
			StringFormat = LiteralSubformat.Utf8;
			StringValue = content;
		}

		public ArrayValue(ArrayType resolvedArrayType, params ISymbolValue[] elements) : base(resolvedArrayType)
		{
			Elements = elements;
		}
		#endregion

		public override string ToCode()
		{
			if (IsString)
			{
				var suff = "";

				if (StringFormat.HasFlag(LiteralSubformat.Utf16))
					suff = "w";
				else if (StringFormat.HasFlag(LiteralSubformat.Utf32))
					suff = "d";

				return "\"" + StringValue + "\"" + suff;
			}

			var sb = new StringBuilder ("[");
			var elements = Elements;
			if (elements != null) {
				foreach (var e in elements)
					if (e == null)
						sb.Append ("[null], ");
					else
						sb.Append (e.ToCode ()).Append (", ");
				if (elements.Length > 0)
					sb.Remove (sb.Length-2, 2);
			}

			return sb.Append(']').ToString();
		}

		public override void Accept(ISymbolValueVisitor vis)
		{
			vis.VisitArrayValue(this);
		}
		public override R Accept<R>(ISymbolValueVisitor<R> vis)
		{
			return vis.VisitArrayValue(this);
		}
	}

	public class AssociativeArrayValue : ExpressionValue
	{
		public ReadOnlyCollection<KeyValuePair<ISymbolValue, ISymbolValue>> Elements
		{
			get;
			private set;
		}

		public AssociativeArrayValue(AssocArrayType baseType, IList<KeyValuePair<ISymbolValue,ISymbolValue>> Elements)
			: base(baseType)
		{
			this.Elements = new ReadOnlyCollection<KeyValuePair<ISymbolValue, ISymbolValue>>(Elements);
		}

		public override string ToCode()
		{
			var sb = new StringBuilder ("[");

			if (Elements != null) {
				foreach (var e in Elements) {
					sb.Append (e.Key == null ? "[null]" : e.Key.ToCode ()).Append (':');
					sb.Append (e.Value == null ? "[null]" : e.Value.ToCode ()).Append (", ");
				}
				sb.Remove (sb.Length - 2, 2);
			}

			return sb.Append(']').ToString();
		}

		public override void Accept(ISymbolValueVisitor vis)
		{
			vis.VisitAssociativeArrayValue(this);
		}
		public override R Accept<R>(ISymbolValueVisitor<R> vis)
		{
			return vis.VisitAssociativeArrayValue(this);
		}
	}

	/// <summary>
	/// Used for both delegates and function references.
	/// </summary>
	public class DelegateValue : ExpressionValue
	{
		public AbstractType Definition { get; private set; }
		public bool IsFunction{get;protected set;}

		public DMethod Method
		{
			get
			{
				if (Definition is DelegateType)
				{
					var dg = (DelegateType)Definition;

					if (dg.IsFunctionLiteral)
						return ((FunctionLiteral)dg.delegateTypeBase).AnonymousMethod;
				}
				return Definition is DSymbol ? ((DSymbol)Definition).Definition as DMethod : null;
			}
		}

		public DelegateValue(DelegateType Dg) : base(Dg)
		{
			this.Definition = Dg;
		}

		public DelegateValue(AbstractType Definition, AbstractType ReturnType, bool IsFunction = false)
			: base(ReturnType)
		{
			this.IsFunction = IsFunction;
			this.Definition = Definition;
		}

		public override string ToCode()
		{
			return Definition == null ? "[null delegate]" : Definition.ToCode();
		}

		public override void Accept(ISymbolValueVisitor vis)
		{
			vis.VisitDelegateValue(this);
		}
		public override R Accept<R>(ISymbolValueVisitor<R> vis)
		{
			return vis.VisitDelegateValue(this);
		}
	}
	#endregion

	#region User data types

	public abstract class InstanceValue : ReferenceValue
	{
		public DClassLike Definition{ get{ return ReferencedNode as DClassLike; }}

		public Dictionary<DVariable, ISymbolValue> Members = new Dictionary<DVariable, ISymbolValue>();
		public Dictionary<DVariable, AbstractType> MemberTypes = new Dictionary<DVariable, AbstractType>();

		protected InstanceValue(TemplateIntermediateType ClassType)
			: base(ClassType)
		{

		}

		/// <summary>
		/// Initializes all variables that have gotten an explicit initializer.
		/// </summary>
		public void RunInitializers()
		{

		}
	}

	/// <summary>
	/// Stores a type. Used e.g. as foreexpressions for PostfixExpressions.
	/// </summary>
	public class TypeValue : ExpressionValue
	{
		public TypeValue(AbstractType r) : base(r) { }

		public override string ToCode()
		{
			return RepresentedType != null ? RepresentedType.ToString() : "null";
		}

		public override void Accept(ISymbolValueVisitor vis)
		{
			vis.VisitTypeValue(this);
		}
		public override R Accept<R>(ISymbolValueVisitor<R> vis)
		{
			return vis.VisitTypeValue(this);
		}
	}

	public abstract class ReferenceValue : ExpressionValue
	{
		public virtual DNode ReferencedNode {get{ return RepresentedType != null ? (base.RepresentedType as DSymbol).Definition : null; }}
		public new DSymbol RepresentedType {get{ return base.RepresentedType as DSymbol; }}
		public AbstractType BaseType { get{ return RepresentedType != null ? RepresentedType.Base : null; } }

		public ReferenceValue(DSymbol symbol) : base(symbol) {}
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
		public NullValue(AbstractType baseType = null) : base(baseType as DSymbol) { }

		public override string ToCode()
		{
			return "null";
		}

		public override void Accept(ISymbolValueVisitor vis)
		{
			vis.VisitNullValue(this);
		}
		public override R Accept<R>(ISymbolValueVisitor<R> vis)
		{
			return vis.VisitNullValue(this);
		}
	}
	#endregion

	/// <summary>
	/// Used when passing several function overloads from the inner evaluation function to the outer (i.e. method call) one.
	/// Not intended to be used in any other kind.
	/// </summary>
	public class InternalOverloadValue : ExpressionValue
	{
		public List<AbstractType> Overloads { get; private set; }

		public InternalOverloadValue(List<AbstractType> overloads) : base(null)
		{
			this.Overloads = overloads;
		}

		public override string ToCode()
		{
			var s = "[Overloads array: ";

			if(Overloads!=null)
				foreach (var o in Overloads)
					s += o.ToCode() + ",";

			return s.TrimEnd(',') + "]";
		}


		public override void Accept(ISymbolValueVisitor vis)
		{
			vis.VisitTypeOverloadValue(this);
		}
		public override R Accept<R>(ISymbolValueVisitor<R> vis)
		{
			return vis.VisitTypeOverloadValue(this);
		}
	}
}
