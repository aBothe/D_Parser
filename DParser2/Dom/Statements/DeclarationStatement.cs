using System;
using D_Parser.Dom.Expressions;
using System.Collections.Generic;

namespace D_Parser.Dom.Statements
{
	public class DeclarationStatement : AbstractStatement,IDeclarationContainingStatement, IExpressionContainingStatement
	{
		/// <summary>
		/// Declarations done by this statement. Contains more than one item e.g. on int a,b,c;
		/// </summary>
		//public INode[] Declaration;

		public override string ToCode()
		{
			if (Declarations == null || Declarations.Length < 1)
				return ";";

			var r = Declarations[0].ToString();

			for (int i = 1; i < Declarations.Length; i++)
			{
				var d = Declarations[i];
				r += ',' + d.Name;

				var dv = d as DVariable;
				if (dv != null && dv.Initializer != null)
					r += '=' + dv.Initializer.ToString();
			}

			return r + ';';
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

				if (Declarations != null)
					foreach (var decl in Declarations)
						if (decl is DVariable && (decl as DVariable).Initializer != null)
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
}

