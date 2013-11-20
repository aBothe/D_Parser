using System;
using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using System.Text;

namespace D_Parser.Dom.Statements
{
	#region Generics
	public interface IStatement : ISyntaxRegion, IVisitable<StatementVisitor>
	{
		new CodeLocation Location { get; set; }
		new CodeLocation EndLocation { get; set; }
		IStatement Parent { get; set; }
		INode ParentNode { get; set; }

		string ToCode();

		R Accept<R>(StatementVisitor<R> vis);
	}

	public interface IExpressionContainingStatement : IStatement
	{
		IExpression[] SubExpressions { get; }
	}

	public interface IDeclarationContainingStatement : IStatement
	{
		INode[] Declarations { get; }
	}

	public abstract class AbstractStatement:IStatement
	{
		public virtual CodeLocation Location { get; set; }
		public virtual CodeLocation EndLocation { get; set; }
		
		readonly WeakReference parentStmt = new WeakReference(null);
		readonly WeakReference parentNode = new WeakReference(null);
		
		public IStatement Parent { get
			{
				return parentStmt.IsAlive ? parentStmt.Target as IStatement : null;
			}
			set{
				parentStmt.Target = value;
			}
		}

		public INode ParentNode {
			get
			{
				return parentNode.IsAlive ? parentNode.Target as INode : 
					(parentStmt.IsAlive ? (parentStmt.Target as IStatement).ParentNode : null);
			}
			set
			{
				if (parentStmt.IsAlive)
					(parentStmt.Target as IStatement).ParentNode = value;
				else
					parentNode.Target = value;
			}
		}

		public abstract string ToCode();

		public override string ToString()
		{
			return ToCode();
		}

		public abstract void Accept(StatementVisitor vis);
		public abstract R Accept<R>(StatementVisitor<R> vis);
	}

	/// <summary>
	/// Represents a statement that can contain other statements, which may become scoped.
	/// </summary>
	public abstract class StatementContainingStatement : AbstractStatement
	{
		public virtual IStatement ScopedStatement { get; set; }

		public virtual IEnumerable<IStatement> SubStatements { get { return new[] { ScopedStatement }; } }

		public IStatement SearchStatement(CodeLocation Where)
		{
			// First check if one sub-statement is located at the code location
			var ss = SubStatements;

			if(ss!=null)
				foreach (var s in ss)
					if (s != null && Where >= s.Location && Where <= s.EndLocation)
						return s;

			// If nothing was found, check if this statement fits to the coordinates
			if (Where >= Location && Where <= EndLocation)
				return this;

			// If not, return null
			return null;
		}

		/// <summary>
		/// Scans the current scope. If a scoping statement was found, also these ones get searched then recursively.
		/// </summary>
		/// <param name="Where"></param>
		public IStatement SearchStatementDeeply(CodeLocation Where)
		{
			var lastS = this;
			IStatement ret = null;

			while (lastS != null && (ret = lastS.SearchStatement (Where)) != lastS)
				lastS = ret as StatementContainingStatement;

			return ret;
		}
	}
	#endregion

	public class BlockStatement : StatementContainingStatement, IEnumerable<IStatement>, IDeclarationContainingStatement
	{
		readonly List<IStatement> _Statements = new List<IStatement>();

		public IEnumerator<IStatement>  GetEnumerator()
		{
 			return _Statements.GetEnumerator();
		}

		System.Collections.IEnumerator  System.Collections.IEnumerable.GetEnumerator()
		{
 			return _Statements.GetEnumerator();
		}

		public override string ToCode()
		{
			var ret = "{"+Environment.NewLine;

			foreach (var s in _Statements)
				ret += s.ToCode()+Environment.NewLine;

			return ret + "}";
		}

		public void Add(IStatement s)
		{
			if (s == null)
				return;
			s.Parent = this;
			_Statements.Add(s);
		}

		public override IEnumerable<IStatement> SubStatements
		{
			get
			{
				return _Statements;
			}
		}
		
		public static List<INode> GetDeclarations(IEnumerable<IStatement> stmts)
		{
			var l = new List<INode>();

			foreach (var s in stmts)
				if (s is BlockStatement ||
					s is DeclarationStatement ||
					s is ImportStatement)
				{
					var decls = (s as IDeclarationContainingStatement).Declarations;
					if (decls != null && decls.Length > 0)
						l.AddRange(decls);
				}

			return l;
		}

		/// <summary>
		/// Returns all child nodes inside the current scope.
		/// Includes nodes from direct block statement substatements and alias nodes from import statements.
		/// Condition
		/// </summary>
		public INode[] Declarations
		{
			get
			{
				return GetDeclarations(_Statements).ToArray();
			}
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public override string ToString()
		{
			return "<block> "+base.ToString();
		}
	}

	public class LabeledStatement : AbstractStatement
	{
		public string Identifier;

		public override string ToCode()
		{
			return Identifier + ":";
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class IfStatement : StatementContainingStatement,IDeclarationContainingStatement,IExpressionContainingStatement
	{
		public IExpression IfCondition;
		public DVariable IfVariable;

		public IStatement ThenStatement
		{
			get { return ScopedStatement; }
			set { ScopedStatement = value; }
		}
		public IStatement ElseStatement;

		public override IEnumerable<IStatement> SubStatements
		{
			get
			{
				if (ThenStatement != null)
					yield return ThenStatement;
				if(ElseStatement != null)
					yield return ElseStatement;
			}
		}

		public override CodeLocation EndLocation
		{
			get
			{
				if (ScopedStatement == null)
					return base.EndLocation;
				return ElseStatement!=null?ElseStatement.EndLocation:ScopedStatement. EndLocation;
			}
			set
			{
				if (ScopedStatement == null)
					base.EndLocation = value;
			}
		}

		public override string ToCode()
		{
			var sb = new StringBuilder("if(");

			if (IfCondition != null)
				sb.Append(IfCondition.ToString());
			else if (IfVariable != null)
				sb.Append(IfVariable.ToString(true, false, true));

			sb.AppendLine(")");

			if (ScopedStatement != null)
				sb.Append(ScopedStatement.ToCode());

			if (ElseStatement != null)
				sb.AppendLine().Append("else ").Append(ElseStatement.ToCode());

			return sb.ToString();
		}

		public IExpression[] SubExpressions
		{
			get {
				return new[] { IfCondition };
			}
		}

		public INode[] Declarations
		{
			get { 
				return IfVariable == null ? null : new[]{IfVariable};
			}
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class WhileStatement : StatementContainingStatement, IExpressionContainingStatement
	{
		public IExpression Condition;

		public override CodeLocation EndLocation
		{
			get
			{
				if (ScopedStatement == null)
					return base.EndLocation;
				return ScopedStatement.EndLocation;
			}
			set
			{
				if (ScopedStatement == null)
					base.EndLocation = value;
			}
		}

		public override string ToCode()
		{
			var ret= "while(";

			if (Condition != null)
				ret += Condition.ToString();

			ret += ") "+Environment.NewLine;

			if (ScopedStatement != null)
				ret += ScopedStatement.ToCode();

			return ret;
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{Condition}; }
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class ForStatement : StatementContainingStatement, IDeclarationContainingStatement, IExpressionContainingStatement
	{
		public IStatement Initialize;
		public IExpression Test;
		public IExpression Increment;

		public IExpression[] SubExpressions
		{
			get { return new[] { Test,Increment }; }
		}

		public override IEnumerable<IStatement> SubStatements
		{
			get
			{
				if(Initialize != null)
					yield return Initialize;
				if(ScopedStatement != null)
					yield return ScopedStatement;
			}
		}

		public override string ToCode()
		{
			var ret = "for(";

			if (Initialize != null)
				ret += Initialize.ToCode();

			ret+=';';

			if (Test != null)
				ret += Test.ToString();

			ret += ';';

			if (Increment != null)
				ret += Increment.ToString();

			ret += ')';

			if (ScopedStatement != null)
				ret += ' '+ScopedStatement.ToCode();

			return ret;
		}

		public INode[] Declarations
		{
			get {
				if (Initialize is DeclarationStatement)
					return (Initialize as DeclarationStatement).Declarations;

				return null;
			}
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class ForeachStatement : StatementContainingStatement, 
		IExpressionContainingStatement,
		IDeclarationContainingStatement
	{
		public bool IsRangeStatement
		{
			get { return UpperAggregate != null; }
		}
		public bool IsReverse = false;
		public DVariable[] ForeachTypeList;

		public INode[] Declarations
		{
			get { return ForeachTypeList; }
		}

		public IExpression Aggregate;

		/// <summary>
		/// Used in ForeachRangeStatements. The Aggregate field will be the lower expression then.
		/// </summary>
		public IExpression UpperAggregate;

		public IExpression[] SubExpressions
		{
			get { return new[]{ Aggregate, UpperAggregate }; }
		}

		public override string ToCode()
		{
			var ret=(IsReverse?"foreach_reverse":"foreach")+'(';

			if(ForeachTypeList != null){
				foreach (var v in ForeachTypeList)
					ret += v.ToString() + ',';

				ret=ret.TrimEnd(',')+';';
			}

			if (Aggregate != null)
				ret += Aggregate.ToString();

			if (UpperAggregate!=null)
				ret += ".." + UpperAggregate.ToString();

			ret += ')';

			if (ScopedStatement != null)
				ret += ' ' + ScopedStatement.ToCode();

			return ret;
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class SwitchStatement : StatementContainingStatement, IExpressionContainingStatement
	{
		public bool IsFinal;
		public IExpression SwitchExpression;

		public IExpression[] SubExpressions
		{
			get { return new[] { SwitchExpression }; }
		}

		public override string ToCode()
		{
			var ret = "switch(";

			if (SwitchExpression != null)
				ret += SwitchExpression.ToString();

			ret+=')';

			if (ScopedStatement != null)
				ret += ' '+ScopedStatement.ToCode();

			return ret;
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public class CaseStatement : StatementContainingStatement, IExpressionContainingStatement, IDeclarationContainingStatement
		{
			public bool IsCaseRange
			{
				get { return LastExpression != null; }
			}

			public IExpression ArgumentList;

			/// <summary>
			/// Used for CaseRangeStatements
			/// </summary>
			public IExpression LastExpression;

			public IStatement[] ScopeStatementList;

			public override string ToCode()
			{
				var ret= "case "+ArgumentList.ToString()+':' + (IsCaseRange?(" .. case "+LastExpression.ToString()+':'):"")+Environment.NewLine;

				foreach (var s in ScopeStatementList)
					ret += s.ToCode()+Environment.NewLine;

				return ret;
			}

			public IExpression[] SubExpressions
			{
				get { return new[]{ArgumentList,LastExpression}; }
			}

			public override IEnumerable<IStatement> SubStatements
			{
				get
				{
					return ScopeStatementList;
				}
			}

			public override void Accept(StatementVisitor vis)
			{
				vis.Visit(this);
			}

			public override R Accept<R>(StatementVisitor<R> vis)
			{
				return vis.Visit(this);
			}
			
			public INode[] Declarations
			{
				get
				{
					return BlockStatement.GetDeclarations(ScopeStatementList).ToArray();
				}
			}
		}

		public class DefaultStatement : StatementContainingStatement, IDeclarationContainingStatement
		{
			public IStatement[] ScopeStatementList;

			public override IEnumerable<IStatement> SubStatements
			{
				get
				{
					return ScopeStatementList;
				}
			}

			public override string ToCode()
			{
				var ret = "default:"+Environment.NewLine;

				foreach (var s in ScopeStatementList)
					ret += s.ToCode() + Environment.NewLine;

				return ret;
			}

			public override void Accept(StatementVisitor vis)
			{
				vis.Visit(this);
			}

			public override R Accept<R>(StatementVisitor<R> vis)
			{
				return vis.Visit(this);
			}
			
			public INode[] Declarations
			{
				get
				{
					return BlockStatement.GetDeclarations(ScopeStatementList).ToArray();
				}
			}
		}
	}

	public class ContinueStatement : AbstractStatement, IExpressionContainingStatement
	{
		public string Identifier;

		public override string ToCode()
		{
			return "continue"+(string.IsNullOrEmpty(Identifier)?"":(' '+Identifier))+';';
		}

		public IExpression[] SubExpressions
		{
			get { return string.IsNullOrEmpty(Identifier)?null:new[]{new IdentifierExpression(Identifier)}; }
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class BreakStatement : AbstractStatement,IExpressionContainingStatement
	{
		public string Identifier;

		public override string ToCode()
		{
			return "break" + (string.IsNullOrEmpty(Identifier) ? "" : (' ' + Identifier)) + ';';
		}

		public IExpression[] SubExpressions
		{
			get { return string.IsNullOrEmpty(Identifier) ? null : new[] { new IdentifierExpression(Identifier) }; }
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class ReturnStatement : AbstractStatement,IExpressionContainingStatement
	{
		public IExpression ReturnExpression;

		public override string ToCode()
		{
			return "return" + (ReturnExpression==null ? "" : (' ' + ReturnExpression.ToString())) + ';';
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{ReturnExpression}; }
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class GotoStatement : AbstractStatement, IExpressionContainingStatement
	{
		public enum GotoStmtType
		{
			Identifier=DTokens.Identifier,
			Case=DTokens.Case,
			Default=DTokens.Default
		}

		public string LabelIdentifier;
		public IExpression CaseExpression;
		public GotoStmtType StmtType = GotoStmtType.Identifier;

		public override string ToCode()
		{
			switch (StmtType)
			{
				case GotoStmtType.Identifier:
					return "goto " + LabelIdentifier+';';
				case GotoStmtType.Default:
					return "goto default;";
				case GotoStmtType.Case:
					return "goto"+(CaseExpression==null?"":(' '+CaseExpression.ToString()))+';';
			}

			return null;
		}

		public IExpression[] SubExpressions
		{
			get { return CaseExpression != null ? new[] { CaseExpression } : null; }
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class WithStatement : StatementContainingStatement, IExpressionContainingStatement
	{
		public IExpression WithExpression;
		public ITypeDeclaration WithSymbol;

		public override string ToCode()
		{
			var ret = "with(";

			if (WithExpression != null)
				ret += WithExpression.ToString();
			else if (WithSymbol != null)
				ret += WithSymbol.ToString();

			ret += ')';

			if (ScopedStatement != null)
				ret += ScopedStatement.ToCode();

			return ret;
		}

		public IExpression[] SubExpressions
		{
			get {
				if (WithExpression != null)
					return new[] { WithExpression};
				if (WithSymbol != null)
					return new[] { new TypeDeclarationExpression(WithSymbol) };
				return null;
			}
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class SynchronizedStatement : StatementContainingStatement,IExpressionContainingStatement
	{
		public IExpression SyncExpression;

		public override string ToCode()
		{
			var ret="synchronized";

			if (SyncExpression != null)
				ret += '(' + SyncExpression.ToString() + ')';

			if (ScopedStatement != null)
				ret += ' ' + ScopedStatement.ToCode();

			return ret;
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{SyncExpression}; }
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class TryStatement : StatementContainingStatement
	{
		public CatchStatement[] Catches;
		public FinallyStatement FinallyStmt;

		public override IEnumerable<IStatement> SubStatements
		{
			get
			{
				if (ScopedStatement != null)
					yield return ScopedStatement;

				if (Catches != null && Catches.Length > 0)
					foreach (var c in Catches)
						yield return c;

				if (FinallyStmt != null)
					yield return FinallyStmt;
			}
		}

		public override string ToCode()
		{
			var ret= "try " + (ScopedStatement!=null? (' '+ScopedStatement.ToCode()):"");

			if (Catches != null && Catches.Length > 0)
				foreach (var c in Catches)
					ret += Environment.NewLine + c.ToCode();

			if (FinallyStmt != null)
				ret += Environment.NewLine + FinallyStmt.ToCode();

			return ret;
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public class CatchStatement : StatementContainingStatement,IDeclarationContainingStatement
		{
			public DVariable CatchParameter;

			public override string ToCode()
			{
				return "catch" + (CatchParameter != null ? ('(' + CatchParameter.ToString() + ')') : "")
					+ (ScopedStatement != null ? (' ' + ScopedStatement.ToCode()) : "");
			}

			public INode[] Declarations
			{
				get {
					if (CatchParameter == null)
						return null;
					return new[]{CatchParameter}; 
				}
			}

			public override void Accept(StatementVisitor vis)
			{
				vis.Visit(this);
			}

			public override R Accept<R>(StatementVisitor<R> vis)
			{
				return vis.Visit(this);
			}
		}

		public class FinallyStatement : StatementContainingStatement
		{
			public override string ToCode()
			{
				return "finally" + (ScopedStatement != null ? (' ' + ScopedStatement.ToCode()) : "");
			}

			public override void Accept(StatementVisitor vis)
			{
				vis.Visit(this);
			}

			public override R Accept<R>(StatementVisitor<R> vis)
			{
				return vis.Visit(this);
			}
		}
	}

	public class ThrowStatement : AbstractStatement,IExpressionContainingStatement
	{
		public IExpression ThrowExpression;

		public override string ToCode()
		{
			return "throw" + (ThrowExpression==null ? "" : (' ' + ThrowExpression.ToString())) + ';';
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{ThrowExpression}; }
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class ScopeGuardStatement : StatementContainingStatement
	{
		public const string ExitScope = "exit";
		public const string SuccessScope = "success";
		public const string FailureScope = "failure";

		public string GuardedScope=ExitScope;

		public override string ToCode()
		{
			return "scope("+GuardedScope+')'+ (ScopedStatement==null?"":ScopedStatement.ToCode());
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class AsmStatement : AbstractStatement
	{
		/// <summary>
		/// TODO: Put the instructions into extra ISyntaxRegions
		/// </summary>
		public string[] Instructions;

		public override string ToCode()
		{
			var ret = "asm {";

			if (Instructions != null && Instructions.Length > 0)
			{
				foreach (var i in Instructions)
					ret += Environment.NewLine + i + ';';
				ret += Environment.NewLine;
			}

			return ret+'}';
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class PragmaStatement : StatementContainingStatement,IExpressionContainingStatement
	{
		public PragmaAttribute Pragma;

		public IExpression[] SubExpressions
		{
			get { return Pragma==null ? null: Pragma.Arguments; }
		}

		public override string ToCode()
		{
			var r = Pragma==null? "" : Pragma.ToString();

			r += ScopedStatement==null? "" : (" " + ScopedStatement.ToCode());

			return r;
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class MixinStatement : AbstractStatement,IExpressionContainingStatement,StaticStatement
	{
		public IExpression MixinExpression;

		public override string ToCode()
		{
			return "mixin("+(MixinExpression==null?"":MixinExpression.ToString())+");";
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{MixinExpression}; }
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.VisitMixinStatement(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.VisitMixinStatement(this);
		}
		
		public DAttribute[] Attributes{get;set;}
	}

	public class StatementCondition : StatementContainingStatement, IExpressionContainingStatement
	{
		public IStatement ElseStatement;
		public DeclarationCondition Condition;

		public override string ToCode ()
		{
			var sb = new StringBuilder ("if(");

			if (Condition != null)
				sb.Append (Condition.ToString ());
			sb.AppendLine (")");

			if (ElseStatement != null)
				sb.Append (ElseStatement);

			return sb.ToString ();
		}

		public override IEnumerable<IStatement> SubStatements
		{
			get
			{
				if (ElseStatement != null)
					yield return ElseStatement;
				if(ScopedStatement != null)
					yield return ScopedStatement;
			}
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public IExpression[] SubExpressions
		{
			get 
			{ 
				if(Condition is StaticIfCondition)
				{
					var sic = (StaticIfCondition)Condition;
					if (sic.Expression != null)
						return new[] { sic.Expression };
				}
				return null;
			}
		}
	}

	public class AssertStatement : AbstractStatement,IExpressionContainingStatement
	{
		public IExpression AssertedExpression;
		public IExpression Message;

		public override string ToCode()
		{
			return "assert("+(AssertedExpression!=null?AssertedExpression.ToString():"")+
				(Message == null?"":(","+Message))+");";
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{ AssertedExpression }; }
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class StaticAssertStatement : AssertStatement, StaticStatement
	{
		public override string ToCode()
		{
			return "static "+base.ToCode();
		}

		public DAttribute[] Attributes
		{
			get;
			set;
		}
		
		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class VolatileStatement : StatementContainingStatement
	{
		public override string ToCode()
		{
			return "volatile " + (ScopedStatement==null?"":ScopedStatement.ToCode());
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class ExpressionStatement : AbstractStatement,IExpressionContainingStatement
	{
		public IExpression Expression;

		public override string ToCode()
		{
			return Expression.ToString()+';';
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{Expression}; }
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class DeclarationStatement : AbstractStatement,IDeclarationContainingStatement, IExpressionContainingStatement
	{
		/// <summary>
		/// Declarations done by this statement. Contains more than one item e.g. on int a,b,c;
		/// </summary>
		//public INode[] Declaration;

		public override string ToCode()
		{
			if (Declarations == null || Declarations.Length < 0)
				return ";";

			var r = Declarations[0].ToString();

			for (int i = 1; i < Declarations.Length; i++)
			{
				var d = Declarations[i];
				r += ',' + d.Name;

				var dv=d as DVariable;
				if (dv != null && dv.Initializer != null)
					r += '=' + dv.Initializer.ToString();
			}

			return r+';';
		}

		public INode[] Declarations
		{
			get;
			set;
		}

		public IExpression[] SubExpressions
		{
			get 
			{
				var l = new List<IExpression>();

				if(Declarations!=null)
					foreach (var decl in Declarations)
						if (decl is DVariable && (decl as DVariable).Initializer!=null)
							l.Add((decl as DVariable).Initializer);

				return l.ToArray();
			}
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class TemplateMixin : AbstractStatement,IExpressionContainingStatement, StaticStatement
	{
		public ITypeDeclaration Qualifier;
		public string MixinId;
		public CodeLocation IdLocation;

		public override string ToCode()
		{
			var r = "mixin";

			if (Qualifier != null)
				r += " " + Qualifier.ToString();

			if(!string.IsNullOrEmpty(MixinId))
				r+=' '+MixinId;

			return r+';';
		}

		public IExpression[] SubExpressions
		{
			get {
				var l=new List<IExpression>();
				var c = Qualifier;

				while (c != null)
				{
					if (c is TemplateInstanceExpression)
						l.Add(c as IExpression);

					c = c.InnerDeclaration;
				}

				return l.ToArray();
			}
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public DAttribute[] Attributes
		{
			get;
			set;
		}
	}

	public class DebugSpecification : AbstractStatement, StaticStatement
	{
		public string SpecifiedId;
		public int SpecifiedDebugLevel;

		public override string ToCode()
		{
			return "debug = "+(SpecifiedId??SpecifiedDebugLevel.ToString())+";";
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public DAttribute[] Attributes
		{
			get;
			set;
		}
	}

	public class VersionSpecification : AbstractStatement, StaticStatement
	{
		public string SpecifiedId;
		public int SpecifiedNumber;

		public override string ToCode()
		{
			return "version = " + (SpecifiedId ?? SpecifiedNumber.ToString()) + ";";
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public DAttribute[] Attributes
		{
			get;
			set;
		}
	}
}
