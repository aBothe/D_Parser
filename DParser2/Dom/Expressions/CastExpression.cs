using System;
using System.Text;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
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
					ret.Append(DTokens.GetTokenString(tk)).Append(' ');
				ret.Remove(ret.Length - 1, 1);
			}

			ret.Append(')');

			if (UnaryExpression != null)
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
			get { return new[]{ UnaryExpression }; }
		}

		public void Accept(ExpressionVisitor vis)
		{
			vis.Visit(this);
		}

		public R Accept<R>(ExpressionVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
}

