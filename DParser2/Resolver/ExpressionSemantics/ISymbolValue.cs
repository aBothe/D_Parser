using System;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public interface ISymbolValue : IEquatable<ISymbolValue>, ISemantic
	{
		AbstractType RepresentedType { get; }
	}

	public abstract class ExpressionValue : ISymbolValue
	{
		public ExpressionValue(AbstractType RepresentedType)
		{
			this.RepresentedType = RepresentedType;
		}

		public AbstractType RepresentedType
		{
			get;
			private set;
		}

		public virtual bool Equals(ISymbolValue other)
		{
			return SymbolValueComparer.IsEqual(this, other);
		}

		public abstract string ToCode();

		public override string ToString()
		{
			try
			{
				return ToCode();
			}
			catch
			{ 
				return null; 
			}
		}
	}

	public class ErrorValue : ISymbolValue
	{
		public readonly EvaluationException[] Errors;

		public ErrorValue(params EvaluationException[] errors)
		{
			this.Errors = errors;
		}

		public string ToCode ()
		{
			return "<Evaluation Error>";
		}
		public bool Equals (ISymbolValue other)
		{
			return false;
		}
		public AbstractType RepresentedType {
			get{return null;}
		}

		public override string ToString ()
		{
			var sb = new System.Text.StringBuilder ();

			sb.AppendLine ("Evaluation errors:");

			foreach (var err in Errors) {
				if(err.EvaluatedExpression != null)
					sb.AppendLine(err.EvaluatedExpression.ToString());
				sb.AppendLine(err.Message);
				sb.AppendLine();
			}

			return sb.ToString().TrimEnd();
		}
	}
}
