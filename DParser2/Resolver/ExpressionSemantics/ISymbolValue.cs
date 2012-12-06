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
}
