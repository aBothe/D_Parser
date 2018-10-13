using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.ExpressionSemantics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace D_Parser.Resolver.ExpressionSemantics.CTFE
{
	internal class CtfeException : EvaluationException
	{
		public CtfeException(IExpression x, string Message, params ISemantic[] LastSubresults)
			: base(x, Message, LastSubresults)
		{
		}

		public CtfeException(string Message, params ISemantic[] LastSubresults)
			: base(Message, LastSubresults)
		{
		}
	}

	internal class FunctionEvaluation : StatementVisitor
	{
		class ReturnInterrupt : Exception
		{
			public readonly ISymbolValue returnedValue;

			public ReturnInterrupt(ISymbolValue returnedValue)
			{
				this.returnedValue = returnedValue;
			}
		}

		readonly StatefulEvaluationContext _statefulEvaluationContext;
		private ResolutionContext ResolutionContext => _statefulEvaluationContext.ResolutionContext;

		#region Constructor/IO
		FunctionEvaluation(MemberSymbol method, ResolutionContext ctxt, Dictionary<DVariable, ISymbolValue> args)
		{
			_statefulEvaluationContext = new StatefulEvaluationContext(ctxt);

			foreach (var kv in args)
				_statefulEvaluationContext.SetLocalValue(kv.Key, kv.Value);
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="dm"></param>
		/// <param name="callArguments"></param>
		/// <param name="baseValueProvider">Required for evaluating missing default parameters.</param>
		public static bool AssignCallArgumentsToIC<T>(MemberSymbol mr, IEnumerable<T> callArguments,
			StatefulEvaluationContext baseValueProvider,
			out Dictionary<DVariable, T> targetArgs, ResolutionContext ctxt = null) where T : class, ISemantic
		{
			var dm = mr.Definition as DMethod;

			if (callArguments == null)
				callArguments = Enumerable.Empty<T>();

			ISemantic firstArg;
			if (TypeResolution.UFCSResolver.IsUfcsResult(mr, out firstArg) && firstArg is T)
			{
				var ufcsPrependedArgList = new List<T>();
				ufcsPrependedArgList.Add((T) firstArg);
				ufcsPrependedArgList.AddRange(callArguments);
				callArguments = ufcsPrependedArgList;
			}

			targetArgs = new Dictionary<DVariable, T>();

			var argEnumerator = callArguments.GetEnumerator();

			for (int para = 0; para < dm.Parameters.Count; para++)
			{
				var par = dm.Parameters[para] as DVariable;

				bool hasNext = argEnumerator.MoveNext();

				if (par.Type is VarArgDecl && hasNext)
				{
					var va_args = new List<T>();
					do va_args.Add(argEnumerator.Current);
					while (argEnumerator.MoveNext());

					//TODO: Assign a value tuple to par
					if (++para < dm.Parameters.Count)
						return false;
				}

				if (hasNext)
				{
					targetArgs[par] = argEnumerator.Current;
				}
				else if (par.Initializer != null)
				{
					if (typeof(T) == typeof(AbstractType))
						targetArgs[par] = ExpressionTypeEvaluation.EvaluateType(par.Initializer, ctxt) as T;
					else if (typeof(T) == typeof(ISymbolValue))
						targetArgs[par] = Evaluation.EvaluateValue(par.Initializer, baseValueProvider) as T;
				}
				else
					return false;
			}

			return !argEnumerator.MoveNext();
		}

		public static ISymbolValue Execute(MemberSymbol method, Dictionary<DVariable, ISymbolValue> arguments,
			ResolutionContext ctxt)
		{
			if (ctxt.CancellationToken.IsCancellationRequested)
				return null;

			if (!(method.Definition is DMethod dm) || dm.BlockStartLocation.IsEmpty)
				return new ErrorValue(new EvaluationException("Method either not declared or undefined", method));

			using (ctxt.Push(method))
			{
				try
				{
					dm.Body.Accept(new FunctionEvaluation(method, ctxt, arguments));
					return new VoidValue();
				}
				catch (ReturnInterrupt returnInterrupt)
				{
					return returnInterrupt.returnedValue;
				}
				catch (EvaluationException ex)
				{
					ctxt.LogError(dm, "Can't execute function at precompile time: " + ex.Message);
					return new ErrorValue(ex);
				}
			}
		}

		#endregion

		ISymbolValue EvaluateExpression(IExpression x)
		{
			return Evaluation.EvaluateValue(x, _statefulEvaluationContext);
		}

		void CheckTimeout()
		{
			ResolutionContext.CancellationToken.ThrowIfCancellationRequested();
		}

		public void Visit(ModuleStatement moduleStatement)
		{
		}

		public void Visit(ImportStatement importStatement)
		{
		}

		public void VisitImport(ImportStatement.Import import)
		{
		}

		public void VisitImport(ImportStatement.ImportBinding importBinding)
		{
		}

		public void VisitImport(ImportStatement.ImportBindings bindings)
		{
		}

		public void Visit(BlockStatement blockStatement)
		{
			foreach (var stmt in blockStatement)
			{
				CheckTimeout();
				stmt.Accept(this);
			}
		}

		public void Visit(LabeledStatement labeledStatement)
		{
		}

		public void Visit(IfStatement ifStatement)
		{
			if(IsTruthy(EvaluateExpression(ifStatement.IfCondition)))
				ifStatement.ThenStatement?.Accept(this);
			else
				ifStatement.ElseStatement?.Accept(this);
		}

		private static bool IsTruthy(ISymbolValue v)
		{
			switch (v)
			{
				case PrimitiveValue pv:
					return pv.Value != 0m;
				/*case ReferenceValue referenceValue:
					if (referenceValue.ReferencedNode is DVariable variable)
						return IsTruthy(_statefulEvaluationContext.GetLocalValue(variable));
					throw new CtfeException("INVALID_REFERNCE", v);*/
				default:
					throw new CtfeException("NOT_IMPLEMENTED", v);
			}
		}

		public void Visit(WhileStatement whileStatement)
		{
			while (IsTruthy(EvaluateExpression(whileStatement.Condition)))
			{
				CheckTimeout();
				whileStatement.ScopedStatement?.Accept(this);
			}
		}

		public void Visit(ForStatement forStatement)
		{
		}

		public void Visit(ForeachStatement foreachStatement)
		{
		}

		public void Visit(SwitchStatement switchStatement)
		{
		}

		public void Visit(SwitchStatement.CaseStatement caseStatement)
		{
		}

		public void Visit(SwitchStatement.DefaultStatement defaultStatement)
		{
		}

		public void Visit(ContinueStatement continueStatement)
		{
		}

		public void Visit(BreakStatement breakStatement)
		{
		}

		public void Visit(ReturnStatement returnStatement)
		{
			var returnValue = returnStatement.ReturnExpression != null
				? EvaluateExpression(returnStatement.ReturnExpression)
				: new VoidValue();

			if (returnValue is VariableValue variableValue)
				returnValue = _statefulEvaluationContext.GetLocalValue(variableValue.Variable);

			throw new ReturnInterrupt(returnValue);
		}

		public void Visit(GotoStatement gotoStatement)
		{
		}

		public void Visit(WithStatement withStatement)
		{
		}

		public void Visit(SynchronizedStatement synchronizedStatement)
		{
		}

		public void Visit(TryStatement tryStatement)
		{
		}

		public void Visit(TryStatement.CatchStatement catchStatement)
		{
		}

		public void Visit(TryStatement.FinallyStatement finallyStatement)
		{
		}

		public void Visit(ThrowStatement throwStatement)
		{
		}

		public void Visit(ScopeGuardStatement scopeGuardStatement)
		{
		}

		public void VisitAsmStatement(AsmStatement asmStatement)
		{
		}

		public void Visit(PragmaStatement pragmaStatement)
		{
		}

		public void Visit(StatementCondition condition)
		{
		}

		public void Visit(VolatileStatement volatileStatement)
		{
		}

		public void Visit(ExpressionStatement expressionStatement)
		{
			EvaluateExpression(expressionStatement.Expression);
		}

		public void Visit(DeclarationStatement declarationStatement)
		{
		}

		public void Visit(TemplateMixin templateMixin)
		{
		}

		public void Visit(DebugSpecification versionSpecification)
		{
		}

		public void Visit(VersionSpecification versionSpecification)
		{
		}

		public void Visit(StaticAssertStatement s)
		{
		}

		public void Visit(StaticForeachStatement foreachStatement)
		{
		}

		public void VisitAsmInstructionStatement(AsmInstructionStatement instrStatement)
		{
		}

		public void VisitAsmRawDataStatement(AsmRawDataStatement dataStatement)
		{
		}

		public void VisitAsmAlignStatement(AsmAlignStatement alignStatement)
		{
		}

		public void VisitMixinStatement(MixinStatement s)
		{
		}
	}
}