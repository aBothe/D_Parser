using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	public class ScalarConstantExpression : PrimaryExpression
	{
		public readonly object Value;
		public readonly LiteralFormat Format;
		public readonly LiteralSubformat Subformat;
		public readonly int EscapeStringHash;

		public ScalarConstantExpression(object value,
			LiteralFormat literalFormat,
			LiteralSubformat subformat = LiteralSubformat.None,
			string escapeString = null)
		{
			Value = value;
			Format = literalFormat;
			Subformat = subformat;

			if (escapeString != null)
			{
				Strings.Add(escapeString);
				EscapeStringHash = escapeString.GetHashCode();
			}
		}

		public override string ToString()
		{
			if (Format == LiteralFormat.CharLiteral)
				return "'" + (EscapeStringHash != 0 ? ("\\" + Strings.TryGet(EscapeStringHash)) : ((char)Value).ToString()) + "'";

			if(Value is decimal)
				return ((decimal)Value).ToString(System.Globalization.CultureInfo.InvariantCulture);

			return Value != null ? Value.ToString() : "";
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
			vis.VisitScalarConstantExpression(this);
		}

		public R Accept<R>(ExpressionVisitor<R> vis)
		{
			return vis.VisitScalarConstantExpression(this);
		}
	}
}
