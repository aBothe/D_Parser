using System;
using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Resolver.ExpressionSemantics.Exceptions;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public class StatefulEvaluationContext
	{
		private int callStackDepth;
		const int MAX_CALLSTACK_DEPTH = 30;
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
			if (_locals.TryGetValue(variable, out var content) && content != null)
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

		private class CallStackDisposable : IDisposable
		{
			private readonly StatefulEvaluationContext _state;

			public CallStackDisposable(StatefulEvaluationContext state) => this._state = state;
			public void Dispose() => _state.callStackDepth--;
		}

		public IDisposable PushCallStack()
		{
			if (callStackDepth >= MAX_CALLSTACK_DEPTH)
				throw new EvaluationStackOverflowException("Stack overflow");

			callStackDepth++;
			return new CallStackDisposable(this);
		}
	}
}
