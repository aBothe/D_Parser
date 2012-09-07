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
		#region Visit children
		public virtual void VisitChildren(IBlockNode block)
		{

		}

		public virtual void VisitChildren(StatementContainingStatement stmtContainer)
		{

		}

		public virtual void VisitChildren(ContainerExpression x)
		{
			
		}

		public virtual void VisitChildren(ITypeDeclaration td)
		{

		}
		#endregion

		#region Nodes
		public virtual void Visit(DEnumValue n)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(DVariable n)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(DMethod n)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(DClassLike n)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(DEnum n)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(DModule n)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(DBlockNode n)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(TemplateParameterNode n)
		{
			throw new NotImplementedException();
		}
		#endregion

		#region Statements
		public virtual void Visit(ModuleStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(ImportStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(BlockStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.LabeledStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.IfStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.WhileStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ForStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ForeachStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.SwitchStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.SwitchStatement.CaseStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.SwitchStatement.DefaultStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ContinueStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.BreakStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ReturnStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(GotoStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.WithStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.SynchronizedStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.TryStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.TryStatement.CatchStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.TryStatement.FinallyStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ThrowStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ScopeGuardStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.AsmStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.PragmaStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.AssertStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ConditionStatement.DebugStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ConditionStatement.VersionStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.VolatileStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ExpressionStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.DeclarationStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.TemplateMixin s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.VersionDebugSpecification s)
		{
			throw new NotImplementedException();
		}
		#endregion

		#region Expressions
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
		#endregion

		#region Decls
		public virtual void Visit(IdentifierDeclaration td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(DTokenDeclaration td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(ArrayDecl td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(DelegateDeclaration td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(PointerDecl td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(MemberFunctionAttributeDecl td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(TypeOfDeclaration td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(VectorDeclaration td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(VarArgDecl td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(ITemplateParameterDeclaration td)
		{
			throw new NotImplementedException();
		}
		#endregion
	}
}
