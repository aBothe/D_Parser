using D_Parser.Dom.Statements;

namespace D_Parser.Dom
{
	public interface StaticStatement : IStatement
	{
		DAttribute[] Attributes { get; set; }
	}	
}
