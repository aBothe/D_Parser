using System;
using System.Collections.Generic;
using System.Linq;

using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver
{
	public class DParserException {
		public readonly string Message;
		
		public readonly string Module;
		public readonly ISyntaxRegion TargetSite;
		
		public DParserException(ISyntaxRegion x,string Msg) {
			TargetSite = x;
			Message = Msg;
		}
	}

	public class ResolutionException : DParserException
	{
		public ISemantic[] LastSubResults { get; protected set; }
		
		public ISymbolValue[] Arguments
		{
			get
			{
				if(LastSubResults==null)
					return null;
				
				var l = new List<ISymbolValue>();
				foreach(var a in LastSubResults)
					l.Add(a as ISymbolValue);
				return l.ToArray();
			}
		}

		public ResolutionException(ISyntaxRegion ObjToResolve, string Message, IEnumerable<ISemantic> LastSubresults)
			: base(ObjToResolve,Message)
		{
			this.LastSubResults = LastSubresults.ToArray();
		}

		public ResolutionException(ISyntaxRegion ObjToResolve, string Message, params ISemantic[] LastSubresult)
			: base(ObjToResolve,Message)
		{
			this.LastSubResults = LastSubresult;
		}
	}

	public class EvaluationException : ResolutionException
	{
		public IExpression EvaluatedExpression
		{
			get { return TargetSite as IExpression; }
		}
		
		public EvaluationException(IExpression x,string Message, params ISemantic[] LastSubresults)
			: base(x, Message, LastSubresults)
		{ }

		public EvaluationException(string Message, IEnumerable<ISemantic> LastSubresults)
			: base(null, Message, LastSubresults) { }

		public EvaluationException(string Message, params ISemantic[] LastSubresults)
			: base(null, Message, LastSubresults)
		{ }
	}

	public class NoConstException : EvaluationException
	{
		public NoConstException(IExpression x) : base(x, "Expression must resolve to constant value") { }
	}

	public class InvalidStringException : EvaluationException
	{
		public InvalidStringException(IExpression x) : base(x, "Expression must be a valid string") { }
	}

	public class AssertException : EvaluationException
	{
		public AssertException(AssertExpression ae, string optAssertMessage="") : base(ae, "Assert returned false. "+optAssertMessage) { }
	}

	public class WrongEvaluationArgException : Exception
	{
		public WrongEvaluationArgException() : base("Wrong argument type for expression evaluation given") {}
	}
}
