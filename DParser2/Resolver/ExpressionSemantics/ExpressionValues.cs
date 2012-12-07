using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Misc;
using D_Parser.Parser;
using D_Parser.Resolver;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public class PrimitiveValue : ExpressionValue
	{
		public readonly byte BaseTypeToken;

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

		public PrimitiveValue(byte BaseTypeToken, decimal Value, IExpression Expression, decimal ImaginaryPart = 0M)
			: base(new PrimitiveType(BaseTypeToken,0, Expression))
		{
			this.BaseTypeToken = BaseTypeToken;
			this.Value = Value;
			this.ImaginaryPart = ImaginaryPart;
		}

		/// <summary>
		/// NaN constructor
		/// </summary>
		private PrimitiveValue(byte baseType,IExpression x)
			: base(new PrimitiveType(baseType, 0, x))
		{
			IsNaN = true;
		}

		public readonly bool IsNaN;

		public static PrimitiveValue CreateNaNValue(IExpression x, byte baseType = DTokens.Float)
		{
			return new PrimitiveValue(baseType, x);
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
		
		public override ulong GetHash()
		{
			unchecked{
			var h = HashPrimes.GetH(1) + (ulong)BaseTypeToken;
			
			h += HashPrimes.GetH(2) * (IsNaN ? 2uL:1uL);
			
			h += HashPrimes.GetH(3) * (ulong)Value;
			
			h += HashPrimes.GetH(4) * (ulong)ImaginaryPart;
			
			return h;
			}
		}
	}

	public class VoidValue : PrimitiveValue
	{
		public VoidValue(IExpression x)
			: base(DTokens.Void, 0M, x)
		{ }
		
		public override ulong GetHash()
		{
			unchecked{
			return base.GetHash()+HashPrimes.GetH(5);
			}
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
		#endregion

		#region Ctor
		/// <summary>
		/// String constructor.
		/// Given result stores both type and idenfitierexpression whose Value is used as content
		/// </summary>
		public ArrayValue(ArrayType stringLiteralResult, IdentifierExpression stringLiteral = null)
			: base(stringLiteralResult)
		{
			StringFormat = LiteralSubformat.Utf8;
			if (stringLiteralResult.DeclarationOrExpressionBase is IdentifierExpression)
			{
				StringFormat = ((IdentifierExpression)stringLiteralResult.DeclarationOrExpressionBase).Subformat;
				StringValue = ((IdentifierExpression)stringLiteralResult.DeclarationOrExpressionBase).Value as string;
			}
			else
				StringValue = stringLiteral.Value as string;
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

			var s = "[";

			if (Elements != null)
				foreach (var e in Elements)
					if (e == null)
						s += "[null], ";
					else
						s += e.ToCode() + ", ";

			return s.TrimEnd(',',' ') + "]";
		}
		
		public override ulong GetHash()
		{
			unchecked{
				var h = HashPrimes.GetH(6) * (IsString ? 2uL : 1uL);
				
				if(IsString){
					h += (ulong)StringValue.GetHashCode();
					h += HashPrimes.GetH(7) * ((ulong)StringFormat);
				}
				else if(Elements != null)
				{
					for(int i = Elements.Length; i!=0;)
						h += HashPrimes.GetH(8+i) * (ulong)i * Elements[--i].GetHash();
				}
				else 
					h += HashPrimes.GetH(9);
				
				return h;
			}
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
			var s = "[";

			if(Elements!=null)
				foreach (var e in Elements)
				{
					var k = e.Key == null ? "[null]" : e.Key.ToCode();
					var v = e.Value == null ? "[null]" : e.Value.ToCode();

					s += k + ":" + v + ", ";
				}

			return s.TrimEnd(',',' ') + "]";
		}
		
		public override ulong GetHash()
		{
			unchecked{
			var h = HashPrimes.GetH(10);
			
			if(Elements == null || Elements.Count == 0)
				h += HashPrimes.GetH(11);
			else for(int i = Elements.Count; i != 0; i--)
			{
				var e = Elements[i];
				h += HashPrimes.GetH(12+i) * (ulong)i * e.Key.GetHash() * e.Value.GetHash();
			}
			
			return h;
			}
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
						return ((FunctionLiteral)dg.DeclarationOrExpressionBase).AnonymousMethod;
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
		
		public override ulong GetHash()
		{
			unchecked{
				return HashPrimes.GetH(13) * (IsFunction ? HashPrimes.GetH(14) : 1uL) +
					(Definition == null ? HashPrimes.Get(15) : (ulong)Definition.ToCode().GetHashCode());
			}
		}
	}
	#endregion

	#region User data types

	public abstract class InstanceValue : ReferenceValue
	{
		public readonly DClassLike Definition;
		public Dictionary<DVariable, ISymbolValue> Members = new Dictionary<DVariable, ISymbolValue>();
		public Dictionary<DVariable, AbstractType> MemberTypes = new Dictionary<DVariable, AbstractType>();

		public InstanceValue(DClassLike Class, AbstractType ClassType)
			: base(Class, ClassType)
		{

		}

		/// <summary>
		/// Initializes all variables that have gotten an explicit initializer.
		/// </summary>
		public void RunInitializers()
		{

		}
		
		public override ulong GetHash()
		{
			throw new NotImplementedException();
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
		
		public override ulong GetHash()
		{
			unchecked{//TODO: Gen hash codes of DTypes
			return HashPrimes.GetH(16) * (ulong)RepresentedType.ToCode().GetHashCode();
			}
		}
	}

	public abstract class ReferenceValue : ExpressionValue
	{
		INode referencedNode;

		public ReferenceValue(INode Node, AbstractType type) : base(type)
		{
		}
		
		public override ulong GetHash()
		{
			throw new NotImplementedException();
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
		public NullValue() : base(null,null) { }

		public override string ToCode()
		{
			return "null";
		}
		
		public override ulong GetHash()
		{
			return HashPrimes.GetH(17);
		}
	}
	#endregion

	/// <summary>
	/// Used when passing several function overloads from the inner evaluation function to the outer (i.e. method call) one.
	/// Not intended to be used in any other kind.
	/// </summary>
	public class InternalOverloadValue : ExpressionValue
	{
		public AbstractType[] Overloads { get; private set; }

		public InternalOverloadValue(AbstractType[] overloads) : base(null)
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
		
		public override ulong GetHash()
		{
			unchecked{
				var h = HashPrimes.GetH(18);
				for(int i = Overloads.Length; i!=0; i--)
					h+= HashPrimes.GetH(19+i) * (ulong)Overloads[i].ToCode().GetHashCode();
				return h;
			}
		}
	}
}
