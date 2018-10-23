using System;
using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Resolver.ExpressionSemantics.Exceptions;

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

		public ISymbolValue GetLocalValue(DVariable variable)
		{
			ISymbolValue content;
			if (_locals.TryGetValue(variable, out content) && content != null)
				return content;
			if (variable.IsConst)
				return Evaluation.EvaluateValue(variable.Initializer, ResolutionContext);
			throw new VariableNotInitializedException("Variable " + variable.Name + " not defined");
		}

		public void SetLocalValue(DVariable variable, ISymbolValue value) => _locals[variable] = value;

		public ICollection<KeyValuePair<DVariable, ISymbolValue>> GetAllLocals()
		{
			return _locals;
		}
	}
}
