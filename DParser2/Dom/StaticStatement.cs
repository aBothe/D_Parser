using D_Parser.Dom.Statements;

namespace D_Parser.Dom
{
	public interface StaticStatement : IStatement
	{
		DeclarationCondition[] Conditions { get; }
	}	
}
