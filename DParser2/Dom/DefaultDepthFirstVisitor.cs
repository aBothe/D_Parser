using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Statements;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom
{
	public abstract class DefaultDepthFirstVisitor : DVisitor
	{
		public virtual void VisitChildren(IBlockNode block)
		{

		}

		public virtual void VisitChildren(StatementContainingStatement stmtContainer)
		{

		}

		public virtual void VisitChildren(ContainerExpression x)
		{
			
		}

		public virtual void Visit(DEnumValue dEnumValue)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(DVariable dVariable)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(DMethod dMethod)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(DClassLike dClassLike)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(DEnum dEnum)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(DModule dModule)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(DBlockNode dBlockNode)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(TemplateParameterNode templateParameterNode)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(ModuleStatement moduleStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(ImportStatement importStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(BlockStatement blockStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.LabeledStatement labeledStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.IfStatement ifStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.WhileStatement whileStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ForStatement forStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ForeachStatement foreachStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.SwitchStatement switchStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.SwitchStatement.CaseStatement caseStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.SwitchStatement.DefaultStatement defaultStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ContinueStatement continueStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.BreakStatement breakStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ReturnStatement returnStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.GotoStatement gotoStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.WithStatement withStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.SynchronizedStatement synchronizedStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.TryStatement tryStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.TryStatement.CatchStatement catchStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.TryStatement.FinallyStatement finallyStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ThrowStatement throwStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ScopeGuardStatement scopeGuardStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.AsmStatement asmStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.PragmaStatement pragmaStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.AssertStatement assertStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ConditionStatement.DebugStatement debugStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ConditionStatement.VersionStatement versionStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.VolatileStatement volatileStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ExpressionStatement expressionStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.DeclarationStatement declarationStatement)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.TemplateMixin templateMixin)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.VersionDebugSpecification versionDebugSpecification)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.AssignExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.ConditionalExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.OrOrExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.AndAndExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.XorExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.OrExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.AndExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.EqualExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.IdendityExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.RelExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.InExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.ShiftExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.AddExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.MulExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.CatExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.PowExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_And x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_Increment x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_Decrement x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_Mul x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_Add x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_Sub x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_Not x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_Cat x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_Type x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.NewExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.AnonymousClassExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.DeleteExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.CastExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.PostfixExpression_Access x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.PostfixExpression_Increment x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.PostfixExpression_Decrement x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.PostfixExpression_MethodCall x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.PostfixExpression_Index x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.PostfixExpression_Slice x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.TemplateInstanceExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.IdentifierExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.TokenExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.TypeDeclarationExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.ArrayLiteralExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.AssocArrayExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.FunctionLiteral x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.AssertExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.MixinExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.ImportExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.TypeidExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.IsExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.TraitsExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.SurroundingParenthesesExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.VoidInitializer x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.ArrayInitializer x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.StructInitializer x)
		{
			throw new NotImplementedException();
		}
	}
}
