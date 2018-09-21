using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.ExpressionSemantics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace D_Parser.Resolver.ExpressionSemantics.CTFE
{
	public class CtfeException : Exception
	{
		public CtfeException(string msg = null) : base(msg) { }
	}

	public class FunctionEvaluation : StatementVisitor
	{
		#region Properties
		readonly InterpretationContext vp;
		ISymbolValue returnedValue;

		#endregion

		#region Constructor/IO
		FunctionEvaluation(MemberSymbol method, AbstractSymbolValueProvider baseValueProvider, Dictionary<DVariable, ISymbolValue> args)
		{
			vp = new InterpretationContext(baseValueProvider);
			returnedValue = null;

			foreach (var kv in args)
				vp[kv.Key] = kv.Value;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="dm"></param>
		/// <param name="callArguments"></param>
		/// <param name="baseValueProvider">Required for evaluating missing default parameters.</param>
		public static bool AssignCallArgumentsToIC<T>(MemberSymbol mr, IEnumerable<T> callArguments, AbstractSymbolValueProvider baseValueProvider,
			out Dictionary<DVariable,T> targetArgs, ResolutionContext ctxt = null) where T:class,ISemantic
		{
			var dm = mr.Definition as DMethod;

			if (callArguments == null)
				callArguments = Enumerable.Empty<T>();

			ISemantic firstArg;
			if (TypeResolution.UFCSResolver.IsUfcsResult(mr, out firstArg) && firstArg is T)
			{
				var ufcsPrependedArgList = new List<T>();
				ufcsPrependedArgList.Add((T)firstArg);
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
					else if(typeof(T) == typeof(ISymbolValue))
						targetArgs[par] = Evaluation.EvaluateValue(par.Initializer, baseValueProvider) as T;
				}
				else
					return false;
			}

			return !argEnumerator.MoveNext();
		}

		public static ISymbolValue Execute(MemberSymbol method, Dictionary<DVariable, ISymbolValue> arguments, AbstractSymbolValueProvider vp)
		{
			if (vp.ResolutionContext.CancellationToken.IsCancellationRequested)
				return null;

			var dm = method.Definition as DMethod;

			if (dm == null || dm.BlockStartLocation.IsEmpty)
				return new ErrorValue(new EvaluationException("Method either not declared or undefined", method));
			var eval = new FunctionEvaluation(method,vp,arguments);
			ISymbolValue ret;

			using (vp.ResolutionContext.Push(method, dm.BlockStartLocation))
			{
				try
				{
					dm.Body.Accept(eval);
				}
				catch (CtfeException ex)
				{
					vp.LogError(dm, "Can't execute function at precompile time: " + ex.Message);
				}

				ret = Evaluation.GetVariableContents(eval.returnedValue, eval.vp);
			}

			return ret;

			//return new ErrorValue(new EvaluationException("CTFE is not implemented yet."));
		}

		#endregion

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
				if (returnedValue != null || vp.ResolutionContext.CancellationToken.IsCancellationRequested)
					break;
				stmt.Accept(this);
			}
		}

		public void Visit(LabeledStatement labeledStatement)
		{
			
		}

		public void Visit(IfStatement ifStatement)
		{
			
		}

		public void Visit(WhileStatement whileStatement)
		{
			
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
			returnedValue = Evaluation.EvaluateValue(returnStatement.ReturnExpression, vp);
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
