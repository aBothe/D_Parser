using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Dom.Expressions;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public abstract class DType : ISemantic
	{
		public readonly ISyntaxRegion DeclarationOrExpressionBase;

		public DType() { }
		public DType(ISyntaxRegion DeclarationOrExpressionBase)
		{
			this.DeclarationOrExpressionBase = DeclarationOrExpressionBase;
		}

		public abstract string ToCode();

		public override string ToString()
		{
			return ToCode();
		}
	}

	public class PrimitiveType : DType
	{
		public readonly int TypeToken;

		/// <summary>
		/// e.g. const, immutable
		/// </summary>
		public readonly int Modifier=0;

		public PrimitiveType(int TypeToken, int Modifier)
		{
			this.TypeToken = TypeToken;
			this.Modifier = Modifier;
		}

		public PrimitiveType(int TypeToken, int Modifier, ISyntaxRegion td)
			: base(td)
		{
			this.TypeToken = TypeToken;
			this.Modifier = Modifier;
		}

		public override string ToCode()
		{
			if(Modifier!=0)
				return DTokens.GetTokenString(Modifier)+"("+DTokens.GetTokenString(TypeToken)+")";

			return DTokens.GetTokenString(TypeToken);
		}
	}

	#region Derived data types
	public abstract class DerivedDataType : DType
	{
		public readonly DType Base;

		public DerivedDataType(DType Base, ISyntaxRegion td) : base(td)
		{
			this.Base = Base;
		}
	}

	public class PointerType : DerivedDataType
	{
		public PointerType(DType Base, ISyntaxRegion td) : base(Base, td) { }

		public override string ToCode()
		{
			return (Base != null ? Base.ToCode() : "") + "*";
		}
	}

	public class ArrayType : AssocArrayType
	{
		public ArrayType(DType ValueType, ISyntaxRegion td)
			: base(ValueType, new PrimitiveType(DTokens.Int, 0), td) { }

		public override string ToCode()
		{
			return (Base != null ? Base.ToCode() : "") + "[]";
		}
	}

	public class AssocArrayType : DerivedDataType
	{
		public readonly DType KeyType;

		/// <summary>
		/// Aliases <see cref="Base"/>
		/// </summary>
		public DType ValueType { get { return Base; } }

		public AssocArrayType(DType ValueType, DType KeyType, ISyntaxRegion td)
			: base(ValueType, td)
		{
			this.KeyType = KeyType;
		}

		public override string ToCode()
		{
			return (Base!=null ? Base.ToCode():"") + "[" + (KeyType!=null ? KeyType.ToCode() : "" )+ "]";
		}
	}

	public class DelegateType : DerivedDataType
	{
		public readonly bool IsFunction;
		public bool IsFunctionLiteral { get { return DeclarationOrExpressionBase is FunctionLiteral; } }
		public readonly DType[] Parameters;

		public DelegateType(DType ReturnType,DelegateDeclaration Declaration, IEnumerable<DType> Parameters) : base(ReturnType, Declaration)
		{
			this.IsFunction = Declaration.IsFunction;

			if (Parameters is DType[])
				this.Parameters = (DType[])Parameters;
			else if(Parameters!=null)
				this.Parameters = Parameters.ToArray();
		}

		public DelegateType(DType ReturnType, FunctionLiteral Literal, IEnumerable<DType> Parameters)
			: base(ReturnType, Literal)
		{
			this.IsFunction = Literal.LiteralToken == DTokens.Function;
			
			if (Parameters is DType[])
				this.Parameters = (DType[])Parameters;
			else if (Parameters != null)
				this.Parameters = Parameters.ToArray();
		}

		public override string ToCode()
		{
			var c = (Base != null ? Base.ToCode() : "") + " " + (IsFunction ? "function" : "delegate") + " (";

			if (Parameters != null)
				foreach (var p in Parameters)
					c += p.ToCode() + ",";

			return c.TrimEnd(',') + ")";
		}
	}
	#endregion

	#region User-defined types
	public abstract class UserDefinedType : DerivedDataType
	{
		public DNode Definition { get; private set; }

		public string Name
		{
			get
			{
				if(Definition!=null)
					return Definition.Name;
				return null;
			}
		}

		public UserDefinedType(DNode Node,DType BaseType, ISyntaxRegion td) : base(BaseType, td) {
			this.Definition = Node;
		}

		public override string ToCode()
		{
			return Definition.ToString(false, false);
		}
	}

	public class AliasedType : UserDefinedType
	{
		public new DVariable Definition { get { return base.Definition as DVariable; } }

		public AliasedType(DVariable AliasDefinition, DType Type, ISyntaxRegion td)
			: base(AliasDefinition,Type, td) {}
	}

	public class EnumType : UserDefinedType
	{
		public new DEnum Definition { get { return base.Definition as DEnum; } }

		public EnumType(DEnum Enum, DType BaseType, ISyntaxRegion td) : base(Enum, BaseType, td) { }
		public EnumType(DEnum Enum, ISyntaxRegion td) : base(Enum, new PrimitiveType(DTokens.Int, 0), td) { }
	}

	public class StructType : UserDefinedType
	{
		public new DClassLike Definition { get { return base.Definition as DClassLike; } }

		public StructType(DClassLike dc, ISyntaxRegion td) : base(dc, null, td) { }
	}

	public class UnionType : UserDefinedType
	{
		public new DClassLike Definition { get { return base.Definition as DClassLike; } }

		public UnionType(DClassLike dc, ISyntaxRegion td) : base(dc, null, td) { }
	}

	public class ClassType : TemplateIntermediateType
	{

	}
	#endregion

	#region Intermediate types - not directly possible for them to build a value basis
	public class InterfaceIntermediateType : UserDefinedType
	{

	}

	public class TemplateIntermediateType : UserDefinedType
	{

	}

	// Member 'types' -- that store the member definitions and their base types (like aliases)

	// Module/Package symbols

	// ===>> Rename everything to DSymbol etc. ?
	#endregion
}
