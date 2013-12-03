
namespace D_Parser.Dom
{
	public interface ISyntaxRegion
	{
		CodeLocation Location { get; }
		CodeLocation EndLocation { get; }
	}

	public static class SyntaxRegionHelper
	{
		public static ISyntaxRegion First(this ISyntaxRegion a, ISyntaxRegion b)
		{
			return a.Location <= b.Location ? a : b;
		}
	}
}
