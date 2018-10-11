using System.Collections.Generic;
using D_Parser.Dom;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public class StatefulEvaluationContext
	{
		private readonly Dictionary<DVariable, ISymbolValue> _locals = new Dictionary<DVariable, ISymbolValue>();
		public readonly ResolutionContext ResolutionContext;

		public StatefulEvaluationContext(ResolutionContext ctxt)
		{
			ResolutionContext = ctxt;
		}

		public void LogError(ISyntaxRegion involvedSyntaxObject, string msg, params ISemantic[] temporaryResults)
		{
			ResolutionContext.LogError (new EvaluationError(involvedSyntaxObject, msg, temporaryResults));
		}

		public ISymbolValue GetLocalValue(DVariable variable) => _locals[variable];
		public void SetLocalValue(DVariable variable, ISymbolValue value) => _locals[variable] = value;

		public ICollection<KeyValuePair<DVariable, ISymbolValue>> GetAllLocals()
		{
			return _locals;
		}
	}
}
