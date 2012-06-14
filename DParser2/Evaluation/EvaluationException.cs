using System;
using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver;

namespace D_Parser.Evaluation
{
	public class DParserException : Exception {
		public DParserException() { }
		public DParserException(string Msg) : base(Msg) { }
	}

	public class ResolutionException : DParserException
	{
		public ISyntaxRegion ObjectToResolve { get; protected set; }
		public ResolveResult[] LastSubResults { get; protected set; }

		public ResolutionException(ISyntaxRegion ObjToResolve, string Message, IEnumerable<ResolveResult> LastSubresults)
			: base(Message)
		{
			this.ObjectToResolve=ObjToResolve;
			this.LastSubResults = LastSubresults.ToArray();
		}

		public ResolutionException(ISyntaxRegion ObjToResolve, string Message, params ResolveResult[] LastSubresult)
			: base(Message)
		{
			this.ObjectToResolve=ObjToResolve;
			this.LastSubResults = LastSubresult;
		}
	}

	public class EvaluationException : ResolutionException
	{
		public IExpression EvaluatedExpression
		{
			get { return ObjectToResolve as IExpression; }
		}

		public EvaluationException(IExpression EvaluatedExpression, string Message, IEnumerable<ResolveResult> LastSubresults)
			: base(EvaluatedExpression, Message, LastSubresults) { }

		public EvaluationException(IExpression EvaluatedExpression, string Message, params ResolveResult[] LastSubresults)
			: base(EvaluatedExpression, Message, LastSubresults)
		{ }
	}
}
