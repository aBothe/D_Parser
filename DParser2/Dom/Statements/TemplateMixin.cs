using System;
using D_Parser.Dom.Expressions;
using System.Collections.Generic;

namespace D_Parser.Dom.Statements
{
	public class TemplateMixin : AbstractStatement, StaticStatement
	{
		public ITypeDeclaration Qualifier;
		public string MixinId;
		public CodeLocation IdLocation;

		public override string ToCode()
		{
			var r = "mixin";

			if (Qualifier != null)
				r += " " + Qualifier.ToString();

			if (!string.IsNullOrEmpty(MixinId))
				r += ' ' + MixinId;

			return r + ';';
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

