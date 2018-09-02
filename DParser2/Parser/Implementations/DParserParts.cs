namespace D_Parser.Parser.Implementations
{
	class DParserParts
	{
		public readonly DAttributesParser attributesParser;
		public readonly DBodiedSymbolsParser bodiedSymbolsParser;
		public readonly DDeclarationParser declarationParser;
		public readonly DExpressionsParser expressionsParser;
		public readonly DModulesParser modulesParser;
		public readonly DStatementParser statementParser;
		public readonly DTemplatesParser templatesParser;

		public DParserParts(DParserStateContext stateContext)
		{
			attributesParser = new DAttributesParser(stateContext, this);
			bodiedSymbolsParser = new DBodiedSymbolsParser(stateContext, this);
			declarationParser = new DDeclarationParser(stateContext, this);
			modulesParser = new DModulesParser(stateContext, this);
			statementParser = new DStatementParser(stateContext, this);
			expressionsParser = new DExpressionsParser(stateContext, this);
			templatesParser = new DTemplatesParser(stateContext, this);
		}
	}
}
