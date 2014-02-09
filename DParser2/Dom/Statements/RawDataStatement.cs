using System;
using System.Text;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom.Statements
{
	public partial class AsmStatement
	{
		public sealed class RawDataStatement : AbstractStatement
		{
			public DataType TypeOfData { get; set; }
			public IExpression[] Data { get; set; }

			public enum DataType
			{
				__UNKNOWN__,

				Byte,
				Word,
				DWord,
				QWord,
				Single,
				Double,
				Real,
			}

			public static bool TryParseDataType(string str, out DataType tp)
			{
				switch (str.ToLower())
				{
					case "db":
						tp = DataType.Byte;
						return true;
					case "ds":
						tp = DataType.Word;
						return true;
					case "di":
						tp = DataType.DWord;
						return true;
					case "dl":
						tp = DataType.QWord;
						return true;
					case "df":
						tp = DataType.Single;
						return true;
					case "dd":
						tp = DataType.Double;
						return true;
					case "de":
						tp = DataType.Real;
						return true;
					default:
						tp = DataType.__UNKNOWN__;
						return false;
				}
			}

			public override string ToCode()
			{
				var sb = new StringBuilder(Data.Length * 4);
				switch (TypeOfData)
				{
					case DataType.Byte:
						sb.Append("db");
						break;
					case DataType.Word:
						sb.Append("ds");
						break;
					case DataType.DWord:
						sb.Append("di");
						break;
					case DataType.QWord:
						sb.Append("dl");
						break;
					case DataType.Single:
						sb.Append("df");
						break;
					case DataType.Double:
						sb.Append("dd");
						break;
					case DataType.Real:
						sb.Append("de");
						break;
					case DataType.__UNKNOWN__:
						sb.Append("<UNKNOWN>");
						break;
					default:
						throw new NotSupportedException();
				}

				for (int i = 0; i < Data.Length; i++)
				{
					if (i > 0)
						sb.Append(',');
					sb.Append(' ');
					sb.Append(Data[i].ToString());
				}
				return sb.ToString();
			}

			public override void Accept(StatementVisitor vis) { vis.Visit(this); }
			public override R Accept<R>(StatementVisitor<R> vis) { return vis.Visit(this); }
		}
	}
}

