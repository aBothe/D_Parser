using D_Parser.Dom;

namespace D_Parser.Resolver.ExpressionSemantics
{
	/// <summary>
	/// An expression value that is allowed to have a new value assigned to as in 'a = b;'
	/// </summary>
	public abstract class LValue : ReferenceValue
	{
		protected LValue(MemberSymbol nodeType) : base(nodeType) { }
	}

	/// <summary>
	/// Contains a reference to a DVariable node.
	/// To get the actual value of the variable, use the value provider.
	/// </summary>
	public class VariableValue : LValue
	{
		public DVariable Variable {get{ return ReferencedNode as DVariable; }}

		public VariableValue(MemberSymbol mr) : base(mr)
		{ }

		public override string ToCode()
		{
			return ReferencedNode == null ? "null" : ReferencedNode.ToString(false);
		}

		public override void Accept(ISymbolValueVisitor vis)
		{
			vis.VisitVariableValue(this);
		}
		public override R Accept<R>(ISymbolValueVisitor<R> vis)
		{
			return vis.VisitVariableValue(this);
		}
	}

	/// <summary>
	/// Used for accessing entries from an array.
	/// </summary>
	public class ArrayPointer : VariableValue
	{
		/// <summary>
		/// Used when accessing normal arrays.
		/// If -1, a item passed to Set() will be added instead of replaced.
		/// </summary>
		public readonly int ItemNumber;

		public ArrayPointer(MemberSymbol arrayVariable, int accessedItem)
			: base(arrayVariable)
		{
			ItemNumber = accessedItem;
		}
		
		/// <summary>
		/// Array ctor.
		/// </summary>
		/// <param name="accessedItem">0 - the array's length-1; -1 when adding the item is wished.</param>
		public ArrayPointer(DVariable accessedArray, ArrayType arrayType, int accessedItem)
			: base(new MemberSymbol(accessedArray, arrayType, null))
		{
			ItemNumber = accessedItem;
		}
	}

	public class AssocArrayPointer : VariableValue
	{
		/// <summary>
		/// Used to identify the accessed item.
		/// </summary>
		public readonly ISymbolValue Key;
		
		public AssocArrayPointer(MemberSymbol assocArrayVariable, ISymbolValue accessedItemKey)
			: base(assocArrayVariable)
		{
			Key = accessedItemKey;
		}
		
		public AssocArrayPointer(DVariable accessedArray, AssocArrayType arrayType, ISymbolValue accessedItemKey)
			: base(new MemberSymbol(accessedArray, arrayType,null))
		{
			Key = accessedItemKey;
		}
	}
}
