using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public interface DValue : DType
	{
	}

	public class PrimitiveValue : PrimitiveType, DValue
	{

	}

	public interface DerivedDataValue : DValue {}

	public class PointerValue : PointerType, DerivedDataValue
	{

	}

	public class ArrayValue : ArrayType, DerivedDataValue
	{

	}

	public class AssocArrayValue : AssocArrayType, DerivedDataValue
	{

	}

	public class DelegateValue : DelegateType, DerivedDataValue
	{

	}

	public interface UserDefinedTypeValue : DValue { }

	// Are dedicated instance objects really required? TODO rethink all this stuff!
}
