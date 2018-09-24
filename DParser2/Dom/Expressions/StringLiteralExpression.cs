using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	public class StringLiteralExpression : PrimaryExpression
	{
		public readonly int ValueStringHash;
		public readonly LiteralFormat Format;
		public readonly LiteralSubformat Subformat;

		public string Value { get { return Strings.TryGet(ValueStringHash); } }

		public StringLiteralExpression(string stringValue, bool isVerbatim = false, LiteralSubformat Subformat = 0)
		{
			Strings.Add(stringValue);
			ValueStringHash = stringValue.GetHashCode();
			Format = isVerbatim ? LiteralFormat.VerbatimStringLiteral : LiteralFormat.StringLiteral;
			this.Subformat = Subformat;
		}

		public override string ToString()
		{
			switch (Format)
			{
				default:
					return "\"" + Value + "\"";
				case LiteralFormat.VerbatimStringLiteral:
					return "r\"" + Value + "\"";
			}
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
			vis.VisitStringLiteralExpression(this);
		}

		public R Accept<R>(ExpressionVisitor<R> vis)
		{
			return vis.VisitStringLiteralExpression(this);
		}
	}
}
