
namespace D_Parser.Dom
{
	public abstract class AttributeMetaDeclaration : ISyntaxRegion
	{
		public DAttribute AttributeOrCondition
		{
			get;
			set;
		}

		public CodeLocation Location
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

	/// <summary>
	/// Describes a meta block that begins with a colon. 'Ends' right after the colon.
	/// </summary>
	public class AttributeMetaDeclarationSection : AttributeMetaDeclaration
	{
		
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
	public class AttributeMetaDeclarationBlock : AttributeMetaDeclaration
	{
		public CodeLocation BlockStartLocation
		{
			get;
			set;
		}
	}
}
