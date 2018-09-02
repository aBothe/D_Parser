using System;
using System.Collections.Generic;

namespace D_Parser.Dom.Expressions
{
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

			if (Type != null)
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

		public IEnumerable<IExpression> SubExpressions
		{
			get
			{
				// In case of a template instance
				if (Type is IExpression)
					yield return Type as IExpression;

				if (NewArguments != null)
					foreach (var arg in NewArguments)
						yield return arg;

				if (Arguments != null)
					foreach (var arg in Arguments)
						yield return arg;
			}
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

