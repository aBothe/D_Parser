using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public interface DType : ISemantic
	{
	}

	public class PrimitiveType : DType
	{

	}

	#region Derived data types
	public abstract class DerivedDataType : DType
	{

	}

	public class PointerType : DerivedDataType
	{

	}

	public class ArrayType : DerivedDataType
	{

	}

	public class AssocArrayType : DerivedDataType
	{

	}

	public class DelegateType : DerivedDataType
	{
		public bool IsFunction { get; private set; }
	}
	#endregion

	#region User-defined types
	public abstract class UserDefinedType : DType
	{

	}

	public class AliasedType : UserDefinedType
	{

	}

	public class EnumType : UserDefinedType
	{

	}

	public class StructType : UserDefinedType
	{

	}

	public class UnionType : UserDefinedType
	{

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
	#endregion
}
