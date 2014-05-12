using System;
using D_Parser.Resolver;
using D_Parser.Dom;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public interface ISymbolValue : IEquatable<ISymbolValue>, ISemantic, IVisitable<ISymbolValueVisitor>
	{
		AbstractType RepresentedType { get; }
		R Accept<R>(ISymbolValueVisitor<R> v);
	}

	public abstract class ExpressionValue : ISymbolValue
	{
		protected ExpressionValue(AbstractType RepresentedType)
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

		public abstract void Accept(ISymbolValueVisitor vis);
		public abstract R Accept<R>(ISymbolValueVisitor<R> vis);
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

		public void Accept(ISymbolValueVisitor vis)
		{
			vis.VisitErrorValue(this);
		}

		public R Accept<R>(ISymbolValueVisitor<R> vis)
		{
			return vis.VisitErrorValue(this);
		}
	}
}
