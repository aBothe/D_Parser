
namespace D_Parser.Dom
{
	public interface IMetaDeclaration : ISyntaxRegion, IVisitable<MetaDeclarationVisitor>
	{
	}

	public interface IMetaDeclarationBlock : IMetaDeclaration
	{
		CodeLocation BlockStartLocation { get; set; }
		CodeLocation EndLocation {get;set;}
	}

	public class AttributeMetaDeclaration : IMetaDeclaration
	{
		public DAttribute[] AttributeOrCondition;

		public ElseMetaDeclaration OptionalElseBlock;

		public AttributeMetaDeclaration(params DAttribute[] attr)
		{
			this.AttributeOrCondition = attr;
		}

		/// <summary>
		/// The start location of the first given attribute
		/// </summary>
		public CodeLocation Location
		{
			get
			{
				return AttributeOrCondition[0].Location;
			}
			set { throw new System.NotImplementedException (); }
		}
		public CodeLocation EndLocation {
			get;
			set;
		}

		public void Accept(MetaDeclarationVisitor vis)
		{
			vis.Visit(this);
		}
	}

	public class ElseMetaDeclaration : IMetaDeclaration
	{
		public CodeLocation Location	{ get;set; }
		public CodeLocation EndLocation	{ get;set; }

		public void Accept(MetaDeclarationVisitor vis)
		{
			vis.Visit(this);
		}
	}

	public class ElseMetaDeclarationBlock : ElseMetaDeclaration, IMetaDeclarationBlock
	{
		public CodeLocation BlockStartLocation
		{
			get;
			set;
		}
	}

	/// <summary>
	/// Describes a meta block that begins with a colon. 'Ends' right after the colon.
	/// </summary>
	public class AttributeMetaDeclarationSection : AttributeMetaDeclaration
	{
		public AttributeMetaDeclarationSection(DAttribute attr) : base(attr) { }
	}

	/// <summary>
	/// Describes a meta block that is enclosed by curly braces.
	/// Examples are
	/// static if(...){
	/// }
	/// 
	/// @safe{
	/// }
	/// </summary>
	public class AttributeMetaDeclarationBlock : AttributeMetaDeclaration, IMetaDeclarationBlock
	{
		public AttributeMetaDeclarationBlock(params DAttribute[] attr) : base(attr) {}

		public CodeLocation BlockStartLocation
		{
			get;
			set;
		}
	}

	/// <summary>
	/// A simple block that is just used for code alignment but semantically irrelevant elsehow.
	/// {
	///		int cascadedIntDecl;
	/// }
	/// </summary>
	public class MetaDeclarationBlock : IMetaDeclarationBlock
	{
		public CodeLocation BlockStartLocation
		{
			get;
			set;
		}

		public CodeLocation Location
		{
			get { return BlockStartLocation; }
			set { BlockStartLocation = value; }
		}
		public CodeLocation EndLocation	{ get;set; }

		public void Accept(MetaDeclarationVisitor vis)
		{
			vis.Visit(this);
		}
	}
}
