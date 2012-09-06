
namespace D_Parser.Dom
{
	public abstract class AbstractMetaDeclaration : ISyntaxRegion
	{
		public abstract CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}
	}

	public interface IMetaDeclarationBlock : ISyntaxRegion
	{
		public CodeLocation BlockStartLocation { get; set; }
	}

	public abstract class AttributeMetaDeclaration : AbstractMetaDeclaration
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
		public override CodeLocation Location
		{
			get
			{
				return AttributeOrCondition[0].Location;
			}
			set
			{
				AttributeOrCondition[0].Location = value;
			}
		}
	}

	public class ElseMetaDeclaration : AbstractMetaDeclaration
	{
		public override CodeLocation Location
		{
			get;
			set;
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
		public AttributeMetaDeclarationSection(DAttribute attr) : base(attr){}
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
		public AttributeMetaDeclarationBlock(DAttribute attr) : base(attr) {}

		public CodeLocation BlockStartLocation
		{
			get;
			set;
		}
	}
}
