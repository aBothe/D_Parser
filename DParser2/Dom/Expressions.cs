using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using D_Parser.Dom.Statements;
using D_Parser.Parser;
using System.Text;

namespace D_Parser.Dom.Expressions
{
	public interface IExpression : ISyntaxRegion, IVisitable<ExpressionVisitor>
	{
		R Accept<R>(ExpressionVisitor<R> vis);
		
		ulong GetHash();
	}

	/// <summary>
	/// Expressions that contain other sub-expressions somewhere share this interface
	/// </summary>
	public interface ContainerExpression:IExpression
	{
		IExpression[] SubExpressions { get; }
	}

	public abstract class OperatorBasedExpression : IExpression, ContainerExpression
	{
		public virtual IExpression LeftOperand { get; set; }
		public virtual IExpression RightOperand { get; set; }
		public byte OperatorToken { get; protected set; }

		public override string ToString()
		{
			return LeftOperand.ToString() + DTokens.GetTokenString(OperatorToken) + (RightOperand != null ? RightOperand.ToString() : "");
		}

		public CodeLocation Location
		{
			get { return LeftOperand.Location; }
		}

		public CodeLocation EndLocation
		{
			get { return RightOperand.EndLocation; }
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{LeftOperand, RightOperand}; }
		}

		public abstract void Accept(ExpressionVisitor v);
		public abstract R Accept<R>(ExpressionVisitor<R> v);
		
		public virtual ulong GetHash()
		{
			ulong hashCode = 0uL;
			unchecked {
				if (LeftOperand != null)
					hashCode += 1000000007 * LeftOperand.GetHash();
				if (RightOperand != null)
					hashCode += 1000000009 * RightOperand.GetHash();
				hashCode += 1000000021 * (ulong)OperatorToken;
			}
			return hashCode;
		}
	}

	public class Expression : IExpression, IEnumerable<IExpression>, ContainerExpression
	{
		public List<IExpression> Expressions = new List<IExpression>();

		public void Add(IExpression ex)
		{
			Expressions.Add(ex);
		}

		public IEnumerator<IExpression> GetEnumerator()
		{
			return Expressions.GetEnumerator();
		}

		public override string ToString()
		{
			var s = "";
			if(Expressions!=null)
				foreach (var ex in Expressions)
					s += (ex == null ? string.Empty : ex.ToString()) + ",";
			return s.TrimEnd(',');
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return Expressions.GetEnumerator();
		}

		public CodeLocation Location
		{
			get { return Expressions.Count>0 ? Expressions[0].Location : CodeLocation.Empty; }
		}

		public CodeLocation EndLocation
		{
			get { return Expressions.Count>0 ? Expressions[Expressions.Count - 1].EndLocation : CodeLocation.Empty; }
		}

		public IExpression[] SubExpressions
		{
			get { return Expressions.ToArray(); }
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			ulong hashCode = 0uL;
			unchecked {
				if (Expressions != null && Expressions.Count > 0)
					for(ulong i = (ulong)Expressions.Count; i!=0;)
						hashCode += 1000000007uL * i * Expressions[(int)--i].GetHash();
			}
			return hashCode;
		}

	}

	/// <summary>
	/// a = b;
	/// a += b;
	/// a *= b; etc.
	/// </summary>
	public class AssignExpression : OperatorBasedExpression
	{
		public AssignExpression(byte opToken) { OperatorToken = opToken; }

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	/// <summary>
	/// a ? b : b;
	/// </summary>
	public class ConditionalExpression : IExpression, ContainerExpression
	{
		public IExpression OrOrExpression { get; set; }

		public IExpression TrueCaseExpression { get; set; }
		public IExpression FalseCaseExpression { get; set; }

		public override string ToString()
		{
			return this.OrOrExpression.ToString() + "?" + TrueCaseExpression.ToString() +':' + FalseCaseExpression.ToString();
		}

		public CodeLocation Location
		{
			get { return OrOrExpression.Location; }
		}

		public CodeLocation EndLocation
		{
			get { return (FalseCaseExpression ?? TrueCaseExpression ?? OrOrExpression).EndLocation; }
		}

		public IExpression[] SubExpressions
		{
			get { return new[] {OrOrExpression, TrueCaseExpression, FalseCaseExpression}; }
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			ulong hashCode = 0uL;
			unchecked {
				if (OrOrExpression != null)
					hashCode += 1000000007 * OrOrExpression.GetHash();
				if (TrueCaseExpression != null)
					hashCode += 1000000009 * TrueCaseExpression.GetHash();
				if (FalseCaseExpression != null)
					hashCode += 1000000021 * FalseCaseExpression.GetHash();
			}
			return hashCode;
		}

	}

	/// <summary>
	/// a || b;
	/// </summary>
	public class OrOrExpression : OperatorBasedExpression
	{
		public OrOrExpression() { OperatorToken = DTokens.LogicalOr; }

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	/// <summary>
	/// a && b;
	/// </summary>
	public class AndAndExpression : OperatorBasedExpression
	{
		public AndAndExpression() { OperatorToken = DTokens.LogicalAnd; }

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	/// <summary>
	/// a ^ b;
	/// </summary>
	public class XorExpression : OperatorBasedExpression
	{
		public XorExpression() { OperatorToken = DTokens.Xor; }

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	/// <summary>
	/// a | b;
	/// </summary>
	public class OrExpression : OperatorBasedExpression
	{
		public OrExpression() { OperatorToken = DTokens.BitwiseOr; }

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	/// <summary>
	/// a & b;
	/// </summary>
	public class AndExpression : OperatorBasedExpression
	{
		public AndExpression() { OperatorToken = DTokens.BitwiseAnd; }

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	/// <summary>
	/// a == b; a != b;
	/// </summary>
	public class EqualExpression : OperatorBasedExpression
	{
		public EqualExpression(bool isUnEqual) { OperatorToken = isUnEqual ? DTokens.NotEqual : DTokens.Equal; }

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	/// <summary>
	/// a is b; a !is b;
	/// </summary>
	public class IdendityExpression : OperatorBasedExpression
	{
		public bool Not;

		public IdendityExpression(bool notIs) { Not = notIs; OperatorToken = DTokens.Is; }

		public override string ToString()
		{
			return LeftOperand.ToString() + (Not ? " !" : " ") + "is " + RightOperand.ToString();
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public override ulong GetHash()
		{
			unchecked {
				return base.GetHash() + 1000000007uL * (Not ? 2uL : 1uL);
			}
		}
	}

	/// <summary>
	/// a &lt;&gt;= b etc.
	/// </summary>
	public class RelExpression : OperatorBasedExpression
	{
		public RelExpression(byte relationalOperator) { OperatorToken = relationalOperator; }

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	/// <summary>
	/// a in b; a !in b
	/// </summary>
	public class InExpression : OperatorBasedExpression
	{
		public bool Not;

		public InExpression(bool notIn) { Not = notIn; OperatorToken = DTokens.In; }

		public override string ToString()
		{
			return LeftOperand.ToString() + (Not ? " !" : " ") + "in " + RightOperand.ToString();
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public override ulong GetHash()
		{
			unchecked {
				return base.GetHash() + 1000000007uL * (Not ? 2uL : 1uL);
			}
		}
	}

	/// <summary>
	/// a >> b; a &lt;&lt; b; a >>> b;
	/// </summary>
	public class ShiftExpression : OperatorBasedExpression
	{
		public ShiftExpression(byte shiftOperator) { OperatorToken = shiftOperator; }

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	/// <summary>
	/// a + b; a - b;
	/// </summary>
	public class AddExpression : OperatorBasedExpression
	{
		public AddExpression(bool isMinus) { OperatorToken = isMinus ? DTokens.Minus : DTokens.Plus; }

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	/// <summary>
	/// a * b; a / b; a % b;
	/// </summary>
	public class MulExpression : OperatorBasedExpression
	{
		public MulExpression(byte mulOperator) { OperatorToken = mulOperator; }

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	/// <summary>
	/// a ~ b
	/// </summary>
	public class CatExpression : OperatorBasedExpression
	{
		public CatExpression() { OperatorToken = DTokens.Tilde; }

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	public interface UnaryExpression : IExpression { }

	public class PowExpression : OperatorBasedExpression, UnaryExpression
	{
		public PowExpression() { OperatorToken = DTokens.Pow; }

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	public abstract class SimpleUnaryExpression : UnaryExpression, ContainerExpression
	{
		public abstract byte ForeToken { get; }
		public IExpression UnaryExpression { get; set; }

		public override string ToString()
		{
			return DTokens.GetTokenString(ForeToken) + UnaryExpression.ToString();
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get { return UnaryExpression.EndLocation; }
		}

		public virtual IExpression[] SubExpressions
		{
			get { return new[]{UnaryExpression}; }
		}

		public abstract void Accept(ExpressionVisitor vis);
		public abstract R Accept<R>(ExpressionVisitor<R> vis);
		
		public ulong GetHash()
		{
			ulong hashCode;
			unchecked {
				hashCode = 1000000007 * (ulong)ForeToken;
				if (UnaryExpression != null)
					hashCode += 1000000009 * UnaryExpression.GetHash();
			}
			return hashCode;
		}

	}

	/// <summary>
	/// Creates a pointer from the trailing type
	/// </summary>
	public class UnaryExpression_And : SimpleUnaryExpression
	{
		public override byte ForeToken
		{
			get { return DTokens.BitwiseAnd; }
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	public class UnaryExpression_Increment : SimpleUnaryExpression
	{
		public override byte ForeToken
		{
			get { return DTokens.Increment; }
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	public class UnaryExpression_Decrement : SimpleUnaryExpression
	{
		public override byte ForeToken
		{
			get { return DTokens.Decrement; }
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	/// <summary>
	/// Gets the pointer base type
	/// </summary>
	public class UnaryExpression_Mul : SimpleUnaryExpression
	{
		public override byte ForeToken
		{
			get { return DTokens.Times; }
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	public class UnaryExpression_Add : SimpleUnaryExpression
	{
		public override byte ForeToken
		{
			get { return DTokens.Plus; }
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	public class UnaryExpression_Sub : SimpleUnaryExpression
	{
		public override byte ForeToken
		{
			get { return DTokens.Minus; }
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	public class UnaryExpression_Not : SimpleUnaryExpression
	{
		public override byte ForeToken
		{
			get { return DTokens.Not; }
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	/// <summary>
	/// Bitwise negation operation:
	/// 
	/// int a=56;
	/// int b=~a;
	/// 
	/// b will be -57;
	/// </summary>
	public class UnaryExpression_Cat : SimpleUnaryExpression
	{
		public override byte ForeToken
		{
			get { return DTokens.Tilde; }
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	/// <summary>
	/// (Type).Identifier
	/// </summary>
	public class UnaryExpression_Type : UnaryExpression
	{
		public ITypeDeclaration Type { get; set; }
		public int AccessIdentifierHash;
		public string AccessIdentifier { get { return Strings.TryGet(AccessIdentifierHash); }
			set { AccessIdentifierHash = value != null ? value.GetHashCode() : 0; Strings.Add(value); }
		}

		public override string ToString()
		{
			return "(" + Type.ToString() + ")." + AccessIdentifier;
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			ulong hashCode = 0uL;
			unchecked {
				if (Type != null)
					hashCode += 1000000007 * (ulong)Type.GetHashCode();
				if (AccessIdentifierHash != 0)
					hashCode += 1000000009 * (ulong)AccessIdentifierHash;
			}
			return hashCode;
		}

	}


	/// <summary>
	/// NewExpression:
	///		NewArguments Type [ AssignExpression ]
	///		NewArguments Type ( ArgumentList )
	///		NewArguments Type
	/// </summary>
	public class NewExpression : UnaryExpression, ContainerExpression
	{
		public ITypeDeclaration Type { get; set; }
		public IExpression[] NewArguments { get; set; }
		public IExpression[] Arguments { get; set; }

		public override string ToString()
		{
			var ret = "new";

			if (NewArguments != null)
			{
				ret += "(";
				foreach (var e in NewArguments)
					ret += e.ToString() + ",";
				ret = ret.TrimEnd(',') + ")";
			}

			if(Type!=null)
				ret += " " + Type.ToString();

			if (!(Type is ArrayDecl))
			{
				ret += '(';
				if (Arguments != null)
					foreach (var e in Arguments)
						ret += e.ToString() + ",";

				ret = ret.TrimEnd(',') + ')';
			}

			return ret;
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public IExpression[] SubExpressions
		{
			get {
				var l = new List<IExpression>();
				
				// In case of a template instance
				if(Type is IExpression)
					l.Add(Type as IExpression);

				if (NewArguments != null)
					l.AddRange(NewArguments);

				if (Arguments != null)
					l.AddRange(Arguments);

				if (l.Count > 0)
					return l.ToArray();

				return null;
			}
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			ulong hashCode = 0uL;
			unchecked {
				if (Type != null)
					hashCode += 1000000007 * (ulong)Type.GetHashCode();
				if (NewArguments != null && NewArguments.Length != 0)
					for(int i = NewArguments.Length; i!= 0;)
						hashCode += 1000000009 * (ulong)i * NewArguments[--i].GetHash();
				if (Arguments != null && Arguments.Length != 0)
					for(int i = Arguments.Length; i!= 0;)
						hashCode += 1000000021 * (ulong)i * Arguments[--i].GetHash();
			}
			return hashCode;
		}

	}

	/// <summary>
	/// NewArguments ClassArguments BaseClasslist { DeclDefs } 
	/// new ParenArgumentList_opt class ParenArgumentList_opt SuperClass_opt InterfaceClasses_opt ClassBody
	/// </summary>
	public class AnonymousClassExpression : UnaryExpression, ContainerExpression
	{
		public IExpression[] NewArguments { get; set; }
		public DClassLike AnonymousClass { get; set; }

		public IExpression[] ClassArguments { get; set; }

		public override string ToString()
		{
			var ret = "new";

			if (NewArguments != null)
			{
				ret += "(";
				foreach (var e in NewArguments)
					ret += e.ToString() + ",";
				ret = ret.TrimEnd(',') + ")";
			}

			ret += " class";

			if (ClassArguments != null)
			{
				ret += '(';
				foreach (var e in ClassArguments)
					ret += e.ToString() + ",";

				ret = ret.TrimEnd(',') + ")";
			}

			if (AnonymousClass != null && AnonymousClass.BaseClasses != null)
			{
				ret += ":";

				foreach (var t in AnonymousClass.BaseClasses)
					ret += t.ToString() + ",";

				ret = ret.TrimEnd(',');
			}

			ret += " {...}";

			return ret;
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public IExpression[] SubExpressions
		{
			get
			{
				var l = new List<IExpression>();

				if (NewArguments != null)
					l.AddRange(NewArguments);

				if (ClassArguments != null)
					l.AddRange(ClassArguments);

				//ISSUE: Add the Anonymous class object to the return list somehow?

				if (l.Count > 0)
					return l.ToArray();

				return null;
			}
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			ulong hashCode = 0uL;
			unchecked {
				if (NewArguments != null)
					for(int i = NewArguments.Length; i!= 0;)
						hashCode += 1000000007 * (ulong)i * NewArguments[--i].GetHash();
				if (AnonymousClass != null)
					hashCode += 1000000009 * (ulong)AnonymousClass.GetHashCode();
				for(int i = ClassArguments.Length; i!= 0;)
					hashCode += 1000000021 * (ulong)i * ClassArguments[--i].GetHash();
			}
			return hashCode;
		}

	}

	public class DeleteExpression : SimpleUnaryExpression
	{
		public override byte ForeToken
		{
			get { return DTokens.Delete; }
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
	}

	/// <summary>
	/// CastExpression:
	///		cast ( Type ) UnaryExpression
	///		cast ( CastParam ) UnaryExpression
	/// </summary>
	public class CastExpression : UnaryExpression, ContainerExpression
	{
		public bool IsTypeCast
		{
			get { return Type != null; }
		}
		public IExpression UnaryExpression;

		public ITypeDeclaration Type { get; set; }
		public byte[] CastParamTokens { get; set; }

		public override string ToString()
		{
			var ret = new StringBuilder("cast(");

			if (IsTypeCast)
				ret.Append(Type.ToString());
			else if (CastParamTokens != null && CastParamTokens.Length != 0) 
			{
				foreach (var tk in CastParamTokens)
					ret.Append(DTokens.GetTokenString (tk)).Append(' ');
				ret.Remove (ret.Length - 1, 1);
			}

			ret.Append(')');
			
			if(UnaryExpression!=null)
				ret.Append(' ').Append(UnaryExpression.ToString());

			return ret.ToString();
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{UnaryExpression}; }
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			ulong hashCode = 0uL;
			unchecked {
				if (UnaryExpression != null)
					hashCode += 1000000007 * UnaryExpression.GetHash();
				if (Type != null)
					hashCode += 1000000009 * Type.GetHash();
				if (CastParamTokens != null && CastParamTokens.Length != 0)
					for(int i= CastParamTokens.Length; i!=0;)
						hashCode += 1000000021 * (ulong)i * (ulong)CastParamTokens[--i];
			}
			return hashCode;
		}

	}

	public abstract class PostfixExpression : IExpression, ContainerExpression
	{
		public IExpression PostfixForeExpression { get; set; }

		public CodeLocation Location
		{
			get { return PostfixForeExpression != null ? PostfixForeExpression.Location : CodeLocation.Empty; }
		}

		public abstract CodeLocation EndLocation { get; set; }

		public virtual IExpression[] SubExpressions
		{
			get { return new[]{PostfixForeExpression}; }
		}

		public abstract void Accept(ExpressionVisitor vis);
		public abstract R Accept<R>(ExpressionVisitor<R> vis);
		
		public virtual ulong GetHash()
		{
			ulong hashCode = 0uL;
			unchecked {
				if (PostfixForeExpression != null)
					hashCode += 1000000007 * PostfixForeExpression.GetHash();
			}
			return hashCode;
		}

	}

	/// <summary>
	/// PostfixExpression . Identifier
	/// PostfixExpression . TemplateInstance
	/// PostfixExpression . NewExpression
	/// </summary>
	public class PostfixExpression_Access : PostfixExpression
	{
        /// <summary>
        /// Can be either
        /// 1) An Identifier
        /// 2) A Template Instance
        /// 3) A NewExpression
        /// </summary>
        public IExpression AccessExpression;

		public override string ToString()
		{
			var r = PostfixForeExpression.ToString() + '.';

            if (AccessExpression != null)
                r += AccessExpression.ToString();

			return r;
		}

		public override CodeLocation EndLocation
		{
			get;
			set;
		}

		public override IExpression[] SubExpressions
		{
			get
			{
				return new[]{PostfixForeExpression, AccessExpression};
			}
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public override ulong GetHash()
		{
			ulong hashCode = base.GetHash();
			unchecked {
				if (AccessExpression != null)
					hashCode += 1000000009 * AccessExpression.GetHash();
			}
			return hashCode;
		}
	}

	public class PostfixExpression_Increment : PostfixExpression
	{
		public override string ToString()
		{
			return PostfixForeExpression.ToString() + "++";
		}

		public sealed override CodeLocation EndLocation
		{
			get;
			set;
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public override ulong GetHash()
		{
			var hashCode = base.GetHash();
			unchecked {
				hashCode += 1000000021;
			}
			return hashCode;
		}
	}

	public class PostfixExpression_Decrement : PostfixExpression
	{
		public override string ToString()
		{
			return PostfixForeExpression.ToString() + "--";
		}

		public sealed override CodeLocation EndLocation
		{
			get;
			set;
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public override ulong GetHash()
		{
			var hashCode = base.GetHash();
			unchecked {
				hashCode += 1000000021;
			}
			return hashCode;
		}
	}

	/// <summary>
	/// PostfixExpression ( )
	/// PostfixExpression ( ArgumentList )
	/// </summary>
	public class PostfixExpression_MethodCall : PostfixExpression
	{
		public IExpression[] Arguments;

		public int ArgumentCount
		{
			get { return Arguments == null ? 0 : Arguments.Length; }
		}

		public override string ToString()
		{
			var sb = new StringBuilder (PostfixForeExpression.ToString ());
			sb.Append('(');

			if (Arguments != null)
				foreach (var a in Arguments)
					if(a != null)
						sb.Append(a.ToString()).Append(',');

			if (sb [sb.Length - 1] != '(')
				sb.Remove (sb.Length - 2, 1);

			return sb.Append(')').ToString();
		}

		public sealed override CodeLocation EndLocation
		{
			get;
			set;
		}

		public override IExpression[] SubExpressions
		{
			get
			{
				var l = new List<IExpression>();

				if (Arguments != null)
					l.AddRange(Arguments);

				if (PostfixForeExpression != null)
					l.Add(PostfixForeExpression);

				return l.Count>0? l.ToArray():null;
			}
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public override ulong GetHash()
		{
			var hashCode = base.GetHash();
			unchecked {
				if(Arguments!=null)
					for(ulong i = (ulong)Arguments.Length; i!=0;)
						hashCode += 1000000038 * i * Arguments[(int)--i].GetHash();
			}
			return hashCode;
		}
	}

	/// <summary>
	/// IndexExpression:
	///		PostfixExpression [ ArgumentList ]
	/// </summary>
	public class PostfixExpression_Index : PostfixExpression
	{
		public IExpression[] Arguments;

		public override string ToString()
		{
			var ret = (PostfixForeExpression != null ? PostfixForeExpression.ToString() : "") + "[";

			if (Arguments != null)
				foreach (var a in Arguments)
					if(a!=null)
						ret += a.ToString() + ",";

			return ret.TrimEnd(',') + "]";
		}

		public sealed override CodeLocation EndLocation
		{
			get;
			set;
		}

		public override IExpression[] SubExpressions
		{
			get
			{
				var l = new List<IExpression>();

				if (Arguments != null)
					l.AddRange(Arguments);

				if (PostfixForeExpression != null)
					l.Add(PostfixForeExpression);

				return l.Count > 0 ? l.ToArray() : null;
			}
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public override ulong GetHash()
		{
			var hashCode = base.GetHash();
			unchecked {
				if(Arguments!=null)
					for(ulong i = (ulong)Arguments.Length; i!=0;)
						hashCode += 1000000083 * i * Arguments[(int)--i].GetHash();
			}
			return hashCode;
		}
	}


	/// <summary>
	/// SliceExpression:
	///		PostfixExpression [ ]
	///		PostfixExpression [ AssignExpression .. AssignExpression ]
	/// </summary>
	public class PostfixExpression_Slice : PostfixExpression
	{
		public IExpression FromExpression;
		public IExpression ToExpression;

		public override string ToString()
		{
			var ret = PostfixForeExpression!=null ? PostfixForeExpression.ToString():"";
				
			ret += "[";

			if (FromExpression != null)
				ret += FromExpression.ToString();

			if (FromExpression != null && ToExpression != null)
				ret += "..";

			if (ToExpression != null)
				ret += ToExpression.ToString();

			return ret + "]";
		}

		public override CodeLocation EndLocation
		{
			get;
			set;
		}

		public override IExpression[] SubExpressions
		{
			get
			{
				return new[] { FromExpression, ToExpression};
			}
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public override ulong GetHash()
		{
			ulong hashCode = base.GetHash();
			unchecked {
				if (FromExpression != null)
					hashCode += 1000000007 * FromExpression.GetHash();
				if (ToExpression != null)
					hashCode += 1000000009 * ToExpression.GetHash();
			}
			return hashCode;
		}

	}

	#region Primary Expressions
	public interface PrimaryExpression : IExpression { }

	public class TemplateInstanceExpression : AbstractTypeDeclaration,PrimaryExpression,ContainerExpression
	{
		public readonly int TemplateIdHash;
		public string TemplateId {get{return Strings.TryGet (TemplateIdHash);}}
		public bool ModuleScopedIdentifier;
		public readonly ITypeDeclaration Identifier;
		public IExpression[] Arguments;

		public TemplateInstanceExpression(ITypeDeclaration id)
		{
			this.Identifier = id;

			var curtd = id;
			while (curtd!=null) {
				if (curtd is IdentifierDeclaration)
				{
					var i = curtd as IdentifierDeclaration;
					TemplateIdHash = i.IdHash;
					ModuleScopedIdentifier = i.ModuleScoped;
					break;
				}
				curtd = curtd.InnerDeclaration;
			}
		}

		public override string ToString(bool IncludesBase)
		{
			var ret = IncludesBase && InnerDeclaration != null ? (InnerDeclaration.ToString() + ".") : "";
			
			if(Identifier!=null)
				ret+=Identifier.ToString();

			ret += "!";

			if (Arguments != null)
			{
				if (Arguments.Length > 1)
				{
					ret += '(';
					foreach (var e in Arguments)
						ret += e.ToString() + ",";
					ret = ret.TrimEnd(',') + ")";
				}
				else if(Arguments.Length==1 && Arguments[0] != null)
					ret += Arguments[0].ToString();
			}

			return ret;
		}

		public IExpression[] SubExpressions
		{
			get { return Arguments; }
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		public override void Accept(TypeDeclarationVisitor vis) {	vis.Visit(this); }
		public override R Accept<R>(TypeDeclarationVisitor<R> vis) { return vis.Visit(this); }
		
		public override ulong GetHash()
		{
			ulong hashCode = 0uL;
			unchecked {
				if (Identifier != null)
					hashCode += 1000000007 * Identifier.GetHash();
				hashCode += (ulong)TemplateIdHash;
				if (Arguments != null)
					for(ulong i = (ulong)Arguments.Length; i!=0;)
						hashCode += 1000000009 * i * Arguments[(int)--i].GetHash();
			}
			return hashCode;
		}

	}

	/// <summary>
	/// Identifier as well as literal primary expression
	/// </summary>
	public class IdentifierExpression : PrimaryExpression
	{
		public bool ModuleScoped;
		public bool IsIdentifier { get { return ValueStringHash != 0 && Format==LiteralFormat.None; } }

		public readonly object Value;
		public readonly int ValueStringHash;
		public string StringValue {get{return ValueStringHash != 0 ? Strings.TryGet (ValueStringHash) : Value as string;}}
		public readonly LiteralFormat Format;
		public readonly LiteralSubformat Subformat;

		//public IdentifierExpression() { }
		public IdentifierExpression(object Val) { Value = Val; Format = LiteralFormat.None; }
		public IdentifierExpression(object Val, LiteralFormat LiteralFormat, LiteralSubformat Subformat = 0) { Value = Val; this.Format = LiteralFormat; this.Subformat = Subformat; }
		public IdentifierExpression(string Value, LiteralFormat LiteralFormat = LiteralFormat.None, LiteralSubformat Subformat = 0) 
		{ 
			Strings.Add (Value);
			ValueStringHash = Value.GetHashCode(); 
			this.Format = LiteralFormat; 
			this.Subformat = Subformat;
		}

		public override string ToString()
		{
			if (Format != Parser.LiteralFormat.None)
				switch (Format) {
					case Parser.LiteralFormat.CharLiteral:
						return "'" + (Value ?? "") + "'";
					case Parser.LiteralFormat.StringLiteral:
						return "\"" + StringValue + "\"";
					case Parser.LiteralFormat.VerbatimStringLiteral:
						return "r\"" + StringValue + "\"";
				}
			else if (IsIdentifier) {
				return (ModuleScoped ? ".": "") + StringValue;
			}
			
			if (Value is decimal)
				return ((decimal)Value).ToString(System.Globalization.CultureInfo.InvariantCulture);
			
			return Value==null?null: Value.ToString();
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			ulong hashCode = 0uL;
			unchecked {
				hashCode += 1000000007uL * (ModuleScoped ? 2uL :1uL);
				if (Value != null)
					hashCode += 1000000009 * (ulong)Value.GetHashCode();
				hashCode += 1000000021 * (ulong)Format;
				hashCode += 1000000033 * (ulong)Subformat;
			}
			return hashCode;
		}

	}

	public class TokenExpression : PrimaryExpression
	{
		public byte Token=DTokens.INVALID;

		public TokenExpression() { }
		public TokenExpression(byte T) { Token = T; }

		public override string ToString()
		{
			return DTokens.GetTokenString(Token);
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			unchecked {
				return 1000000007 * (ulong)Token;
			}
		}

	}

	/// <summary>
	/// BasicType . Identifier
	/// </summary>
	public class TypeDeclarationExpression : PrimaryExpression
	{
		public ITypeDeclaration Declaration;

		public TypeDeclarationExpression() { }
		public TypeDeclarationExpression(ITypeDeclaration td) { Declaration = td; }

		public override string ToString()
		{
			return Declaration != null ? Declaration.ToString() : "";
		}

		public CodeLocation Location
		{
			get { return Declaration!=null? Declaration.Location: CodeLocation.Empty; }
		}

		public CodeLocation EndLocation
		{
			get { return Declaration != null ? Declaration.EndLocation : CodeLocation.Empty; }
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			unchecked {
				if (Declaration != null)
					return 1000000007 * Declaration.GetHash();
			}
			return 0;
		}

	}

	/// <summary>
	/// auto arr= [1,2,3,4,5,6];
	/// </summary>
	public class ArrayLiteralExpression : PrimaryExpression,ContainerExpression
	{
		public readonly List<IExpression> Elements = new List<IExpression>();
		
		public ArrayLiteralExpression(List<IExpression> elements)
		{
			Elements = elements;
		}

		public override string ToString()
		{
			var s = "[";
			
			//HACK: To prevent exessive string building flood, limit element count to 100
			if(Elements != null)
				for (int i = 0; i < Elements.Count; i++)
				{
					s += Elements[i].ToString() + ", ";
					if (i == 100)
					{
						s += "...";
						break;
					}
				}
			s = s.TrimEnd(' ', ',') + "]";
			return s;
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public IExpression[] SubExpressions
		{
			get { return Elements!=null && Elements.Count>0? Elements.ToArray() : null; }
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			ulong hashCode = DTokens.OpenSquareBracket; // because it's like an Expression
			unchecked {
				if (Elements != null)
					for(int i = Elements.Count; i!=0;)
						hashCode += 1000000007 * (ulong)i * Elements[--i].GetHash();
			}
			return hashCode;
		}

	}

	/// <summary>
	/// auto arr=['a':0xa, 'b':0xb, 'c':0xc, 'd':0xd, 'e':0xe, 'f':0xf];
	/// </summary>
	public class AssocArrayExpression : PrimaryExpression,ContainerExpression
	{
		public IList<KeyValuePair<IExpression, IExpression>> Elements = new List<KeyValuePair<IExpression, IExpression>>();

		public override string ToString()
		{
			var s = "[";
			foreach (var expr in Elements)
				s += expr.Key.ToString() + ":" + expr.Value.ToString() + ", ";
			s = s.TrimEnd(' ', ',') + "]";
			return s;
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public IExpression[] SubExpressions
		{
			get {
				var l = new List<IExpression>();

				foreach (var kv in Elements)
				{
					if(kv.Key!=null)
						l.Add(kv.Key);
					if(kv.Value!=null)
						l.Add(kv.Value);
				}

				return l.Count > 0 ? l.ToArray() : null;
			}
		}

		public virtual void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public virtual R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public virtual ulong GetHash()
		{
			ulong hashCode = 0uL;
			unchecked {
				if (Elements != null && Elements.Count != 0)
					foreach(var e in Elements)
					{
						hashCode += 1000000007 * e.Key.GetHash();
						hashCode += 1000000009 * e.Value.GetHash();
					}
			}
			return hashCode;
		}

	}

	public class FunctionLiteral : PrimaryExpression
	{
		public byte LiteralToken = DTokens.Delegate;
		public readonly bool IsLambda;

		public readonly DMethod AnonymousMethod = new DMethod(DMethod.MethodType.AnonymousDelegate);

		public FunctionLiteral(bool lambda = false) {
			IsLambda = lambda;
			if (lambda)
				AnonymousMethod.SpecialType |= DMethod.MethodType.Lambda;
		}
		public FunctionLiteral(byte InitialLiteral) { LiteralToken = InitialLiteral; }

		public override string ToString()
		{
			if (IsLambda)
			{
				var sb = new StringBuilder();

				if (AnonymousMethod.Parameters.Count == 1 && AnonymousMethod.Parameters[0].Type == null)
					sb.Append(AnonymousMethod.Parameters[0].Name);
				else
				{
					sb.Append('(');
					foreach (INode param in AnonymousMethod.Parameters)
					{
						sb.Append(param).Append(',');
					}

					if(AnonymousMethod.Parameters.Count > 0)
						sb.Remove(sb.Length -1,1);
					sb.Append(')');
				}

				sb.Append(" => ");

				if(AnonymousMethod.Body != null)
				{
					var en = AnonymousMethod.Body.SubStatements.GetEnumerator();
					if(en.MoveNext() && en.Current is ReturnStatement)
						sb.Append((en.Current as ReturnStatement).ReturnExpression.ToString());
					else
						sb.Append(AnonymousMethod.Body.ToCode());
					en.Dispose ();
				}
				else 
					sb.Append("{}");

				return sb.ToString();
			}

			return DTokens.GetTokenString(LiteralToken) + (AnonymousMethod.NameHash == 0 ?"": " ") + AnonymousMethod.ToString();
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			ulong hashCode = 0uL;
			unchecked {
				hashCode += 1000000007 * (ulong)LiteralToken;
				hashCode += 1000000009uL * (IsLambda ? 2uL : 1uL);
				if (AnonymousMethod != null) //TODO: Hash entire method bodies?
					hashCode += 1000000021 * (ulong)AnonymousMethod.GetHashCode();
			}
			return hashCode;
		}

	}

	public class AssertExpression : PrimaryExpression,ContainerExpression
	{
		public IExpression[] AssignExpressions;

		public override string ToString()
		{
			var ret = "assert(";

			foreach (var e in AssignExpressions)
				ret += e.ToString() + ",";

			return ret.TrimEnd(',') + ")";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public IExpression[] SubExpressions
		{
			get { return AssignExpressions; }
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			ulong hashCode = 0uL;
			unchecked {
				if (AssignExpressions != null)
					for(int i = AssignExpressions.Length; i!=0;)
						hashCode += 1000000007 * (ulong)i * AssignExpressions[i--].GetHash();
			}
			return hashCode;
		}

	}

	public class MixinExpression : PrimaryExpression,ContainerExpression
	{
		public IExpression AssignExpression;

		public override string ToString()
		{
			return "mixin(" + AssignExpression.ToString() + ")";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{AssignExpression}; }
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			ulong hashCode = DTokens.Mixin;
			unchecked {
				if (AssignExpression != null)
					hashCode += 1000000007 * AssignExpression.GetHash();
			}
			return hashCode;
		}

	}

	public class ImportExpression : PrimaryExpression,ContainerExpression
	{
		public IExpression AssignExpression;

		public override string ToString()
		{
			return "import(" + AssignExpression.ToString() + ")";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{AssignExpression}; }
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			ulong hashCode = DTokens.Import;
			unchecked {
				if (AssignExpression != null)
					hashCode += 1000000007 * AssignExpression.GetHash();
			}
			return hashCode;
		}

	}

	public class TypeidExpression : PrimaryExpression,ContainerExpression
	{
		public ITypeDeclaration Type;
		public IExpression Expression;

		public override string ToString()
		{
			return "typeid(" + (Type != null ? Type.ToString() : Expression.ToString()) + ")";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public IExpression[] SubExpressions
		{
			get { 
				if(Expression!=null)
					return new[]{Expression};
				if (Type != null)
					return new[] { new TypeDeclarationExpression(Type)};
				return null;
			}
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			ulong hashCode = DTokens.Typeid;
			unchecked {
				if (Type != null)
					hashCode += 1000000007 * Type.GetHash();
				if (Expression != null)
					hashCode += 1000000009 * Expression.GetHash();
			}
			return hashCode;
		}

	}

	public class IsExpression : PrimaryExpression,ContainerExpression
	{
		public ITypeDeclaration TestedType;
		public int TypeAliasIdentifierHash;
		public string TypeAliasIdentifier {get{ return Strings.TryGet (TypeAliasIdentifierHash); }}
		public CodeLocation TypeAliasIdLocation;

		private TemplateTypeParameter ptp;
		/// <summary>
		/// Persistent parameter object that keeps information about the first specialization 
		/// </summary>
		public TemplateTypeParameter ArtificialFirstSpecParam
		{
			get {
				if (ptp == null)
				{
					return ptp = new TemplateTypeParameter(TypeAliasIdentifierHash, TypeAliasIdLocation, null) { 
						Specialization = TypeSpecialization
					};
				}
				return ptp;
			}
		}

		/// <summary>
		/// True if Type == TypeSpecialization instead of Type : TypeSpecialization
		/// </summary>
		public bool EqualityTest;

		public ITypeDeclaration TypeSpecialization;
		public byte TypeSpecializationToken;

		public TemplateParameter[] TemplateParameterList;

		public override string ToString()
		{
			var ret = "is(";

			if (TestedType != null)
				ret += TestedType.ToString();

			if (TypeAliasIdentifierHash != 0)
				ret += ' ' + TypeAliasIdentifier;

			if (TypeSpecialization != null || TypeSpecializationToken!=0)
				ret +=(EqualityTest ? "==" : ":")+ (TypeSpecialization != null ? 
					TypeSpecialization.ToString() : // Either the specialization declaration
					DTokens.GetTokenString(TypeSpecializationToken)); // or the spec token

			if (TemplateParameterList != null)
			{
				ret += ",";
				foreach (var p in TemplateParameterList)
					ret += p.ToString() + ",";
			}

			return ret.TrimEnd(' ', ',') + ")";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public IExpression[] SubExpressions
		{
			get { 
				if (TestedType != null)
					return new[] { new TypeDeclarationExpression(TestedType)};

				return null;
			}
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			ulong hashCode = DTokens.Is;
			unchecked {
				if (TestedType != null)
					hashCode += 1000000007 * TestedType.GetHash();
				hashCode += 1000000009 * (ulong)TypeAliasIdentifierHash;
				if (ptp != null)//TODO: Create hash functions of template type parameters
					hashCode += 1000000021 * (ulong)ptp.ToString().GetHashCode();
				hashCode += 1000000033uL * (EqualityTest?2uL:1uL);
				if (TypeSpecialization != null)
					hashCode += 1000000087 * TypeSpecialization.GetHash();
				hashCode += 1000000093 * (ulong)TypeSpecializationToken;
				if (TemplateParameterList != null)
					for(int i=TemplateParameterList.Length; i!=0;)
						hashCode += 1000000097 * (ulong)(i * TemplateParameterList[--i].ToString().GetHashCode());
			}
			return hashCode;
		}

	}

	public class TraitsExpression : PrimaryExpression, ContainerExpression
	{
		public string Keyword;

		public TraitsArgument[] Arguments;

		public override string ToString()
		{
			var ret = "__traits(" + Keyword;

			if (Arguments != null)
				foreach (var a in Arguments)
					ret += "," + a.ToString();

			return ret + ")";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			ulong hashCode = DTokens.__traits;
			unchecked {
				if (Keyword != null)
					hashCode += 1000000007 * (ulong)Keyword.GetHashCode();
				if (Arguments != null)
				{
					ulong i=1;
					foreach(var arg in Arguments)
						hashCode += 1000000009 * i++ * arg.GetHash();
				}
			}
			return hashCode;
		}

		
		public IExpression[] SubExpressions {
			get {
				if(Arguments == null || Arguments.Length == 0)
					return null;
				
				var exs = new IExpression[Arguments.Length];
				for(int i = Arguments.Length -1; i>=0; i--)
					exs[i] = Arguments[i].AssignExpression;
				return exs;
			}
		}
	}

	public class TraitsArgument : ISyntaxRegion, IVisitable<ExpressionVisitor>
	{
		public readonly ITypeDeclaration Type;
		public readonly IExpression AssignExpression;

		public TraitsArgument(ITypeDeclaration t)
		{
			this.Type = t;
		}
		
		public TraitsArgument(IExpression x)
		{
			this.AssignExpression = x;
		}
		
		public override string ToString()
		{
			return Type != null ? Type.ToString(false) : AssignExpression.ToString();
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public void Accept(ExpressionVisitor vis)
		{
			vis.Visit(this);
		}

		public R Accept<R>(ExpressionVisitor<R> vis)
		{
			return vis.Visit(this);
		}
		
		public ulong GetHash()
		{
			ulong hashCode = 0uL;
			unchecked {
				if (Type != null)
					hashCode += 1000000007 * Type.GetHash();
				if (AssignExpression != null)
					hashCode += 1000000009 * AssignExpression.GetHash();
			}
			return hashCode;
		}
	}

	/// <summary>
	/// ( Expression )
	/// </summary>
	public class SurroundingParenthesesExpression : PrimaryExpression,ContainerExpression
	{
		public IExpression Expression;

		public override string ToString()
		{
			return "(" + (Expression != null ? Expression.ToString() : string.Empty) + ")";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{Expression}; }
		}

		public void Accept(ExpressionVisitor vis) {	vis.Visit(this); }
		public R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public ulong GetHash()
		{
			ulong hashCode = DTokens.OpenParenthesis;
			unchecked {
				if (Expression != null)
					hashCode += 1000000007 * Expression.GetHash();
			}
			return hashCode;
		}

	}
	#endregion

	#region Initializers

	public interface IVariableInitializer { }

	public abstract class AbstractVariableInitializer : IVariableInitializer,IExpression
	{
		public const int AbstractInitializerHash = 1234;
		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public abstract void Accept(ExpressionVisitor vis);
		public abstract R Accept<R>(ExpressionVisitor<R> vis);
		
		public abstract ulong GetHash();
	}

	public class VoidInitializer : AbstractVariableInitializer
	{
		public VoidInitializer() { }

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public override ulong GetHash()
		{
			return AbstractInitializerHash + DTokens.Void;
		}
	}

	public class ArrayInitializer : AssocArrayExpression,IVariableInitializer {
		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public override ulong GetHash()
		{
			return AbstractVariableInitializer.AbstractInitializerHash + base.GetHash();
		}
	}

	public class StructInitializer : AbstractVariableInitializer, ContainerExpression
	{
		public StructMemberInitializer[] MemberInitializers;

		public sealed override string ToString()
		{
			var ret = "{";

			if (MemberInitializers != null)
				foreach (var i in MemberInitializers)
					ret += i.ToString() + ",";

			return ret.TrimEnd(',') + "}";
		}

		public IExpression[] SubExpressions
		{
			get {
				if (MemberInitializers == null)
					return null;

				var l = new List<IExpression>(MemberInitializers.Length);

				foreach (var mi in MemberInitializers)
					if(mi.Value!=null)
						l.Add(mi.Value);

				return l.ToArray();
			}
		}

		public override void Accept(ExpressionVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(ExpressionVisitor<R> vis) { return vis.Visit(this); }
		
		public override ulong GetHash()
		{
			ulong hashCode = AbstractInitializerHash + DTokens.Struct + DTokens.OpenCurlyBrace;
			unchecked {
				if (MemberInitializers != null)
					for(int i = MemberInitializers.Length; i!=0;)
						hashCode += 1000000007 * (ulong)i * MemberInitializers[--i].GetHash();
			}
			return hashCode;
		}

	}

	public class StructMemberInitializer : ISyntaxRegion, IVisitable<ExpressionVisitor>
	{
		public string MemberName = string.Empty;
		public IExpression Value;

		public sealed override string ToString()
		{
			return (!string.IsNullOrEmpty(MemberName) ? (MemberName + ":") : "") + Value.ToString();
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public void Accept(ExpressionVisitor vis)
		{
			vis.Visit(this);
		}

		public R Accept<R>(ExpressionVisitor<R> vis)
		{
			return vis.Visit(this);
		}
		
		public ulong GetHash()
		{
			ulong hashCode = AbstractVariableInitializer.AbstractInitializerHash;
			unchecked {
				if (MemberName != null)
					hashCode += 1000000007 * (ulong)MemberName.GetHashCode();
				if (Value != null)
					hashCode += 1000000009 * Value.GetHash();
			}
			return hashCode;
		}

	}
	#endregion
}
