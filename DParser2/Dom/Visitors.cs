using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;

namespace D_Parser.Dom
{
	public interface DVisitor : NodeVisitor, StatementVisitor, ExpressionVisitor
	{
		
	}

	public interface NodeVisitor
	{
		void Visit(DEnumValue dEnumValue);
		void Visit(DVariable dVariable);
		void Visit(DMethod dMethod);
		void Visit(DClassLike dClassLike);
		void Visit(DEnum dEnum);
		void Visit(DModule dModule);
		void Visit(DBlockNode dBlockNode);

		void Visit(TemplateParameterNode templateParameterNode);
	}

	public interface StatementVisitor
	{
		void Visit(ModuleStatement moduleStatement);
		void Visit(ImportStatement importStatement);
		void Visit(BlockStatement blockStatement);
		void Visit(LabeledStatement labeledStatement);
		void Visit(IfStatement ifStatement);
		void Visit(WhileStatement whileStatement);
		void Visit(ForStatement forStatement);
		void Visit(ForeachStatement foreachStatement);
		void Visit(SwitchStatement switchStatement);
		void Visit(SwitchStatement.CaseStatement caseStatement);
		void Visit(SwitchStatement.DefaultStatement defaultStatement);
		void Visit(ContinueStatement continueStatement);
		void Visit(BreakStatement breakStatement);
		void Visit(ReturnStatement returnStatement);
		void Visit(GotoStatement gotoStatement);
		void Visit(WithStatement withStatement);
		void Visit(SynchronizedStatement synchronizedStatement);
		void Visit(TryStatement tryStatement);
		void Visit(TryStatement.CatchStatement catchStatement);
		void Visit(TryStatement.FinallyStatement finallyStatement);
		void Visit(ThrowStatement throwStatement);
		void Visit(ScopeGuardStatement scopeGuardStatement);
		void Visit(AsmStatement asmStatement);
		void Visit(PragmaStatement pragmaStatement);
		void Visit(AssertStatement assertStatement);
		void Visit(ConditionStatement.DebugStatement debugStatement);
		void Visit(ConditionStatement.VersionStatement versionStatement);
		void Visit(VolatileStatement volatileStatement);
		void Visit(ExpressionStatement expressionStatement);
		void Visit(DeclarationStatement declarationStatement);
		void Visit(TemplateMixin templateMixin);
		void Visit(VersionDebugSpecification versionDebugSpecification);
	}

	public interface ExpressionVisitor
	{
		void Visit(Expression x);
		void Visit(AssignExpression x);
		void Visit(ConditionalExpression x);
		void Visit(OrOrExpression x);
		void Visit(AndAndExpression x);
		void Visit(XorExpression x);
		void Visit(OrExpression x);
		void Visit(AndExpression x);
		void Visit(EqualExpression x);
		void Visit(IdendityExpression x);
		void Visit(RelExpression x);
		void Visit(InExpression x);
		void Visit(ShiftExpression x);
		void Visit(AddExpression x);
		void Visit(MulExpression x);
		void Visit(CatExpression x);
		void Visit(PowExpression x);

		void Visit(UnaryExpression_And x);
		void Visit(UnaryExpression_Increment x);
		void Visit(UnaryExpression_Decrement x);
		void Visit(UnaryExpression_Mul x);
		void Visit(UnaryExpression_Add x);
		void Visit(UnaryExpression_Sub x);
		void Visit(UnaryExpression_Not x);
		void Visit(UnaryExpression_Cat x);
		void Visit(UnaryExpression_Type x);

		void Visit(NewExpression x);
		void Visit(AnonymousClassExpression x);
		void Visit(DeleteExpression x);
		void Visit(CastExpression x);

		void Visit(PostfixExpression_Access x);
		void Visit(PostfixExpression_Increment x);
		void Visit(PostfixExpression_Decrement x);
		void Visit(PostfixExpression_MethodCall x);
		void Visit(PostfixExpression_Index x);
		void Visit(PostfixExpression_Slice x);

		void Visit(TemplateInstanceExpression x);
		void Visit(IdentifierExpression x);
		void Visit(TokenExpression x);
		void Visit(TypeDeclarationExpression x);
		void Visit(ArrayLiteralExpression x);
		void Visit(AssocArrayExpression x);
		void Visit(FunctionLiteral x);
		void Visit(AssertExpression x);
		void Visit(MixinExpression x);
		void Visit(ImportExpression x);
		void Visit(TypeidExpression x);
		void Visit(IsExpression x);
		void Visit(TraitsExpression x);
		void Visit(SurroundingParenthesesExpression x);

		void Visit(VoidInitializer x);
		void Visit(ArrayInitializer x);
		void Visit(StructInitializer x);
	}
}
