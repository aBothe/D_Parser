
namespace D_Parser.Dom.Expressions
{
	public interface IntermediateIdType : ISyntaxRegion
	{
		bool ModuleScoped { get; set; }
		int IdHash{get;}
	}

	public class IdentifierExpression : PrimaryExpression, IntermediateIdType
	{
		public bool ModuleScoped {
			get;
			set;
		}

		public int IdHash {
			get;
			private set;
		}

		public string StringValue { get { return Strings.TryGet(IdHash); } }

		public IdentifierExpression(string id)
		{ 
			Strings.Add(id);
			IdHash = id.GetHashCode();
		}

		public override string ToString()
		{
			return (ModuleScoped ? "." : "") + StringValue;
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

		public void Accept(ExpressionVisitor vis)
		{
			vis.Visit(this);
		}

		public R Accept<R>(ExpressionVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
}

