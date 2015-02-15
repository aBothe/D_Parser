using System;
using System.Text;
using D_Parser.Parser;
using D_Parser.Dom.Statements;

namespace D_Parser.Dom.Expressions
{
	public class FunctionLiteral : PrimaryExpression
	{
		public byte LiteralToken = DTokens.Delegate;
		public readonly bool IsLambda;
		public readonly DMethod AnonymousMethod = new DMethod(DMethod.MethodType.AnonymousDelegate);

		public FunctionLiteral(bool lambda = false)
		{
			IsLambda = lambda;
			if (lambda)
				AnonymousMethod.SpecialType |= DMethod.MethodType.Lambda;
		}

		public FunctionLiteral(byte InitialLiteral)
		{
			LiteralToken = InitialLiteral;
		}

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

					if (AnonymousMethod.Parameters.Count > 0)
						sb.Remove(sb.Length - 1, 1);
					sb.Append(')');
				}

				sb.Append(" => ");

				if (AnonymousMethod.Body != null)
				{
					var en = AnonymousMethod.Body.SubStatements.GetEnumerator();
					if (en.MoveNext() && en.Current is ReturnStatement)
						sb.Append((en.Current as ReturnStatement).ReturnExpression.ToString());
					else
						sb.Append(AnonymousMethod.Body.ToCode());
					en.Dispose();
				}
				else
					sb.Append("{}");

				return sb.ToString();
			}

			return DTokens.GetTokenString(LiteralToken) + (AnonymousMethod.NameHash == 0 ? "" : " ") + AnonymousMethod.ToString();
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
	}
}

