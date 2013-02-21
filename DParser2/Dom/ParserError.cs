
namespace D_Parser.Dom
{
	public class ParserError
	{
		public readonly bool IsSemantic;
		public readonly string Message;
		public readonly int Token;
		public readonly CodeLocation Location;

		public ParserError(bool IsSemanticError, string Message, int KeyToken, CodeLocation ErrorLocation)
		{
			IsSemantic = IsSemanticError;
			this.Message = Message;
			this.Token = KeyToken;
			this.Location = ErrorLocation;
		}

		public override string ToString()
		{
			return "[Parse error] " + Message;
		}
	}
}
